using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ShiSha;

public sealed record ChinaRegion(string Code, string Name, string Level, string ParentCode, int Children, double Longitude, double Latitude)
{
    public WeatherCity ToWeatherCity(string path) => new("CN-AD-" + Code, "CN", Name, Name, Latitude, Longitude, "Asia/Shanghai", 0, path);
}

public sealed class ChinaRegionService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 16 * 1024 * 1024 };
    private readonly string cacheFolder;
    public static readonly List<ChinaRegion> Provinces = LoadProvinces();
    public ChinaRegionService(string cacheFolder) => this.cacheFolder = cacheFolder;
    private static List<ChinaRegion> LoadProvinces()
    {
        using var stream = typeof(ChinaRegionService).Assembly.GetManifestResourceStream("ShiSha.ChinaProvinces.json")!;
        return JsonSerializer.Deserialize<List<ChinaRegion>>(stream) ?? throw new InvalidDataException("Province data missing");
    }
    public static bool Valid(ChinaRegion region) => Regex.IsMatch(region.Code, "^[0-9]{6}$") && !string.IsNullOrWhiteSpace(region.Name)
        && region.Level is "province" or "city" or "district" && double.IsFinite(region.Latitude) && double.IsFinite(region.Longitude)
        && region.Latitude is >= -90 and <= 90 && region.Longitude is >= -180 and <= 180 && region.Children >= 0;
    public async Task<List<ChinaRegion>> ChildrenAsync(ChinaRegion parent, CancellationToken token)
    {
        if (!Valid(parent)) throw new InvalidDataException("Invalid administrative region");
        if (parent.Children == 0 || parent.Level == "district") return new();
        var path = Path.Combine(cacheFolder, parent.Code + ".json");
        if (File.Exists(path))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<List<ChinaRegion>>(await File.ReadAllTextAsync(path, token));
                if (cached != null && cached.Count > 0 && cached.All(c => Valid(c) && c.ParentCode == parent.Code)) return cached;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        }
        var rows = Parse(await Client.GetStringAsync("https://geo.datav.aliyun.com/areas_v3/bound/" + parent.Code + "_full.json", token), parent.Code);
        try
        {
            Directory.CreateDirectory(cacheFolder);
            // Cache only the list and representative coordinates, never map geometry.
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(rows), token);
            File.Move(temporary, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return rows;
    }
    public static List<ChinaRegion> Parse(string json, string parentCode)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Region list missing");
        var result = new List<ChinaRegion>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("properties", out var p) || p.ValueKind != JsonValueKind.Object
                || !p.TryGetProperty("level", out var level) || level.ValueKind != JsonValueKind.String) continue;
            var kind = level.GetString()!;
            if (kind is not ("province" or "city" or "district")) continue;
            if (!p.TryGetProperty("adcode", out var code) || !p.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String
                || !p.TryGetProperty("parent", out var parent) || parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty("adcode", out var owner)
                || owner.ToString() != parentCode || !p.TryGetProperty("center", out var center) || center.ValueKind != JsonValueKind.Array || center.GetArrayLength() != 2
                || center[0].ValueKind != JsonValueKind.Number || center[1].ValueKind != JsonValueKind.Number) continue;
            var children = p.TryGetProperty("childrenNum", out var number) && number.ValueKind == JsonValueKind.Number ? number.GetInt32() : 0;
            var region = new ChinaRegion(code.ToString(), name.GetString()!, kind, owner.ToString(), children, center[0].GetDouble(), center[1].GetDouble());
            if (Valid(region)) result.Add(region);
        }
        if (result.Count == 0) throw new InvalidDataException("No selectable administrative regions");
        return result.DistinctBy(r => r.Code).OrderBy(r => r.Code, StringComparer.Ordinal).ToList();
    }
}
