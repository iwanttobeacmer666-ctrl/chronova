using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ShiSha;

public sealed class Reminder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    // Wall-clock value in the task's own timezone. Old files default to Beijing.
    public DateTime Due { get; set; }
    public string TimeZoneId { get; set; } = "China Standard Time";
    public bool Daily { get; set; }
    public bool Completed { get; set; }
    public DateTime? LastNotified { get; set; }
}

public static class Schedule
{
    public const string BeijingZone = "China Standard Time";
    public static DateTime BeijingNow => DateTime.SpecifyKind(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime, DateTimeKind.Unspecified);
    public static TimeZoneInfo Zone(string id) => TimeZoneInfo.FindSystemTimeZoneById(id);
    public static DateTime Local(DateTimeOffset instant, string zoneId) => TimeZoneInfo.ConvertTime(instant, Zone(zoneId)).DateTime;
    public static DateTimeOffset Instant(DateTime wallTime, string zoneId)
    {
        wallTime = DateTime.SpecifyKind(wallTime, DateTimeKind.Unspecified);
        var zone = Zone(zoneId);
        // Smaller UTC offset means the later instant when the local hour repeats.
        if (zone.IsAmbiguousTime(wallTime)) return new DateTimeOffset(wallTime, zone.GetAmbiguousTimeOffsets(wallTime).Min()).ToUniversalTime();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(wallTime, zone));
    }
    private static DateTimeOffset DailyInstant(DateTime wallTime, string zoneId)
    {
        // A daily reminder inside a DST gap moves to the first valid minute that day.
        while (Zone(zoneId).IsInvalidTime(wallTime)) wallTime = wallTime.AddMinutes(1);
        return Instant(wallTime, zoneId);
    }
    public static bool TryTime(string text, out TimeSpan time)
    {
        time = default;
        return DateTime.TryParseExact(text.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            && (time = parsed.TimeOfDay) >= TimeSpan.Zero;
    }
    public static DateTime? Occurrence(Reminder task, DateTime now)
    {
        var occurrence = OccurrenceInstant(task, Instant(now, BeijingZone));
        return occurrence == null ? null : Local(occurrence.Value, task.TimeZoneId);
    }
    public static DateTime Next(Reminder task, DateTime now)
        => Local(NextInstant(task, Instant(now, BeijingZone)), BeijingZone);
    public static DateTimeOffset? OccurrenceInstant(Reminder task, DateTimeOffset now)
    {
        if (task.Completed) return null;
        var first = Instant(task.Due, task.TimeZoneId);
        if (now < first) return null;
        if (!task.Daily) return task.LastNotified == null ? first : null;
        var wall = Local(now, task.TimeZoneId).Date + task.Due.TimeOfDay;
        var latest = DailyInstant(wall, task.TimeZoneId);
        if (latest > now) latest = DailyInstant(wall.AddDays(-1), task.TimeZoneId);
        if (latest < first || (task.LastNotified != null && Instant(task.LastNotified.Value, task.TimeZoneId) >= latest)) return null;
        return latest;
    }
    public static DateTimeOffset NextInstant(Reminder task, DateTimeOffset now)
    {
        var first = Instant(task.Due, task.TimeZoneId);
        if (!task.Daily) return first;
        var wall = Local(now, task.TimeZoneId).Date + task.Due.TimeOfDay;
        var next = DailyInstant(wall, task.TimeZoneId);
        if (next <= now) next = DailyInstant(wall.AddDays(1), task.TimeZoneId);
        return next < first ? first : next;
    }
}

public sealed class TaskStore
{
    public string FilePath { get; }
    public TaskStore(string filePath) => FilePath = filePath;
    public List<Reminder> Load()
    {
        if (!File.Exists(FilePath)) return new();
        var tasks = JsonSerializer.Deserialize<List<Reminder>>(File.ReadAllText(FilePath))
            ?? throw new InvalidDataException("任务文件为空或格式错误。");
        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Title) || task.Title.Length > 200 || task.Due == default)
                throw new InvalidDataException("任务文件中存在无效记录。");
            if (string.IsNullOrWhiteSpace(task.TimeZoneId)) throw new InvalidDataException("Invalid task timezone");
            _ = Schedule.Instant(task.Due, task.TimeZoneId);
            if (task.LastNotified is DateTime notified) _ = Schedule.Instant(notified, task.TimeZoneId);
        }
        return tasks;
    }
    public void Save(List<Reminder> tasks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
        else File.Move(temp, FilePath);
    }
}

public sealed class UserSettings
{
    public string TimeZoneId { get; set; } = Schedule.BeijingZone;
    public string LanguageCode { get; set; } = "zh-CN";
    public string WeatherCityId { get; set; } = "CN-Beijing";
    public WeatherCity? CustomWeatherCity { get; set; }
    public string ChinaProvinceCode { get; set; } = "110000";
    public string ChinaCityCode { get; set; } = "110000";
    public string ChinaDistrictCode { get; set; } = "110000";
    public string BackgroundFile { get; set; } = "";
    public double BackgroundBrightness { get; set; } = 0.55;
}

public sealed class SettingsStore
{
    private readonly string path;
    public SettingsStore(string path) => this.path = path;
    public UserSettings Load()
    {
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Invalid settings");
        if (string.IsNullOrWhiteSpace(settings.TimeZoneId)) throw new InvalidDataException("Invalid timezone");
        _ = Schedule.Zone(settings.TimeZoneId);
        if (!UiText.Supported(settings.LanguageCode)) throw new InvalidDataException("Invalid language");
        return settings;
    }
    public void Save(UserSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(settings));
        if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak", true);
        else File.Move(path + ".tmp", path);
    }
}

public sealed class Countdown
{
    private DateTimeOffset? deadline;
    private TimeSpan paused;
    public bool Running => deadline != null;
    public bool HasStarted { get; private set; }
    public TimeSpan Remaining(DateTimeOffset now) => deadline is DateTimeOffset end ? (end > now ? end - now : TimeSpan.Zero) : paused;
    public void Start(TimeSpan duration, DateTimeOffset now)
    {
        if (duration <= TimeSpan.Zero || duration.TotalHours > 100) throw new ArgumentOutOfRangeException(nameof(duration));
        paused = duration; deadline = now + duration; HasStarted = true;
    }
    public void Pause(DateTimeOffset now) { paused = Remaining(now); deadline = null; }
    public bool Poll(DateTimeOffset now)
    {
        if (!Running || Remaining(now) > TimeSpan.Zero) return false;
        Reset(); return true;
    }
    public void Reset() { deadline = null; paused = TimeSpan.Zero; HasStarted = false; }
}
