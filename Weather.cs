using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ShiSha;

public sealed record WeatherCity(string Id, string CountryCode, string Name, string ChineseName, double Latitude, double Longitude, string TimeZone, int GeoNamesId, string? AdministrativePath = null);
public sealed record WeatherCountry(string Id, string ChineseName, string EnglishName);

public static class WeatherLocations
{
    public static readonly WeatherCountry[] Countries =
    {
        new("CN", "中国", "China"), new("US", "美国", "United States"), new("GB", "英国", "United Kingdom"),
        new("CA", "加拿大", "Canada"), new("AU", "澳大利亚", "Australia"), new("JP", "日本", "Japan"),
        new("KR", "韩国", "South Korea"), new("FR", "法国", "France"), new("DE", "德国", "Germany"),
        new("IT", "意大利", "Italy"), new("ES", "西班牙", "Spain"), new("PT", "葡萄牙", "Portugal"),
        new("RU", "俄罗斯", "Russia"), new("IN", "印度", "India"), new("BR", "巴西", "Brazil"),
        new("MX", "墨西哥", "Mexico"), new("SG", "新加坡", "Singapore"), new("NZ", "新西兰", "New Zealand"),
        new("SA", "沙特阿拉伯", "Saudi Arabia"), new("AE", "阿联酋", "United Arab Emirates")
    };
    public static readonly List<WeatherCity> Cities = Load();
    private static List<WeatherCity> Load()
    {
        using var stream = typeof(WeatherLocations).Assembly.GetManifestResourceStream("ShiSha.Cities.json")
            ?? throw new InvalidDataException("City catalog is missing.");
        return JsonSerializer.Deserialize<List<WeatherCity>>(stream) ?? throw new InvalidDataException("City catalog is empty.");
    }
    public static WeatherCity Find(string id) => Cities.FirstOrDefault(c => c.Id == id) ?? Cities.First(c => c.Id == "CN-Beijing");
    public static bool ValidCustom(WeatherCity city, string id) => !string.IsNullOrEmpty(id) && city.Id == id && System.Text.RegularExpressions.Regex.IsMatch(id, "^CN-AD-[0-9]{6}$")
        && city.CountryCode == "CN" && !string.IsNullOrWhiteSpace(city.Name) && !string.IsNullOrWhiteSpace(city.ChineseName)
        && double.IsFinite(city.Latitude) && double.IsFinite(city.Longitude) && city.Latitude is >= -90 and <= 90 && city.Longitude is >= -180 and <= 180;
}

public sealed record WeatherForecast(int Code, bool IsDay, double Low, double High, double WindSpeed, double WindDirection, string LocalDate, DateTimeOffset FetchedAt);

public static class WeatherMath
{
    public static int Beaufort(double metresPerSecond)
    {
        if (!double.IsFinite(metresPerSecond) || metresPerSecond < 0) throw new ArgumentOutOfRangeException(nameof(metresPerSecond));
        double[] thresholds = { .3, 1.6, 3.4, 5.5, 8, 10.8, 13.9, 17.2, 20.8, 24.5, 28.5, 32.7 };
        return thresholds.Count(threshold => metresPerSecond >= threshold);
    }
    public static string Direction(double degrees)
    {
        if (!double.IsFinite(degrees)) throw new ArgumentOutOfRangeException(nameof(degrees));
        string[] directions = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return directions[(int)Math.Floor((((degrees % 360) + 360) % 360 + 22.5) / 45) % 8];
    }
    public static (string Chinese, string English, string Icon) Condition(int code, bool day)
        => code switch
        {
            0 => ("晴", "Clear", day ? "☀" : "☾"),
            1 => ("晴间多云", "Mostly clear", day ? "☀" : "☾"),
            2 => ("多云", "Partly cloudy", "⛅"),
            3 => ("阴", "Overcast", "☁"),
            45 or 48 => ("雾", "Fog", "≋"),
            51 or 53 or 55 => ("毛毛雨", "Drizzle", "☂"),
            56 or 57 or 66 or 67 => ("冻雨", "Freezing rain", "☂"),
            61 or 63 or 65 => ("雨", "Rain", "☂"),
            71 or 73 or 75 or 77 => ("雪", "Snow", "❄"),
            80 or 81 or 82 => ("阵雨", "Rain showers", "☂"),
            85 or 86 => ("阵雪", "Snow showers", "❄"),
            95 => ("雷雨", "Thunderstorm", "ϟ"),
            96 or 99 => ("雷雨伴冰雹", "Thunderstorm with hail", "ϟ"),
            _ => ("天气未知", "Unknown weather", "○")
        };
}

public sealed class WeatherService
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 128 * 1024 };
    public static Uri ForecastUri(WeatherCity city)
    {
        var latitude = city.Latitude.ToString(CultureInfo.InvariantCulture);
        var longitude = city.Longitude.ToString(CultureInfo.InvariantCulture);
        return new Uri("https://api.open-meteo.com/v1/forecast?latitude=" + latitude + "&longitude=" + longitude
            + "&current=weather_code,is_day,wind_speed_10m,wind_direction_10m"
            + "&daily=temperature_2m_min,temperature_2m_max&wind_speed_unit=ms&temperature_unit=celsius&timezone=auto&forecast_days=1");
    }
    public async Task<WeatherForecast> FetchAsync(WeatherCity city, CancellationToken token)
        => Parse(await Client.GetStringAsync(ForecastUri(city), token), DateTimeOffset.UtcNow);
    public static WeatherForecast Parse(string json, DateTimeOffset fetchedAt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("daily", out var daily) || daily.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Weather response is incomplete.");
        double Number(JsonElement element, string key)
        {
            if (!element.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var number) || !double.IsFinite(number)) throw new InvalidDataException("Invalid weather value: " + key);
            return number;
        }
        double DailyNumber(string key)
        {
            if (!daily.TryGetProperty(key, out var array) || array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0
                || array[0].ValueKind != JsonValueKind.Number || !array[0].TryGetDouble(out var number) || !double.IsFinite(number)) throw new InvalidDataException("Missing daily weather: " + key);
            return number;
        }
        if (!daily.TryGetProperty("time", out var dates) || dates.ValueKind != JsonValueKind.Array || dates.GetArrayLength() == 0
            || dates[0].ValueKind != JsonValueKind.String || !DateOnly.TryParseExact(dates[0].GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            throw new InvalidDataException("Missing weather date.");
        var low = DailyNumber("temperature_2m_min"); var high = DailyNumber("temperature_2m_max");
        var wind = Number(current, "wind_speed_10m"); var direction = Number(current, "wind_direction_10m");
        var code = Number(current, "weather_code"); var isDay = Number(current, "is_day");
        if (low > high || wind < 0 || direction < 0 || direction > 360 || code < 0 || code > 999 || code != Math.Truncate(code) || (isDay != 0 && isDay != 1))
            throw new InvalidDataException("Weather values are out of range.");
        return new WeatherForecast((int)code, isDay == 1, low, high, wind, direction, date.ToString("yyyy-MM-dd"), fetchedAt);
    }
}
