using ShiSha;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
var due = new DateTime(2026, 10, 27, 9, 0, 0);
var task = new Reminder { Title = "去健身", Due = due };
Check(Schedule.Occurrence(task, due.AddSeconds(-1)) == null, "No early reminder");
Check(Schedule.Occurrence(task, due) == due, "Exact due time");
Check(Schedule.Occurrence(task, due.AddDays(3)) == due, "Catch up after restart");
task.LastNotified = due;
Check(Schedule.Occurrence(task, due.AddDays(3)) == null, "One-time reminder does not repeat");
task.Daily = true;
Check(Schedule.Occurrence(task, due.AddHours(23)) == null, "Daily task does not repeat before next occurrence");
Check(Schedule.Occurrence(task, due.AddDays(1)) == due.AddDays(1), "Daily reminder next day");
Check(Schedule.Occurrence(task, due.AddDays(4).AddHours(-1)) == due.AddDays(3), "Missed daily reminder collapses to latest occurrence");
task.LastNotified = due.AddDays(3);
Check(Schedule.Occurrence(task, due.AddDays(4).AddHours(-1)) == null, "Catch-up only once");
Check(Schedule.Next(task, due.AddHours(23)) == due.AddDays(1), "Next daily time before trigger");
Check(Schedule.Next(task, due.AddDays(1)) == due.AddDays(2), "Next daily time after trigger");
task.Completed = true;
Check(Schedule.Occurrence(task, due.AddDays(20)) == null, "Stopped task never fires");
Check(Schedule.TryTime("00:00", out var midnight) && midnight == TimeSpan.Zero, "Midnight accepted");
Check(Schedule.TryTime("23:59", out _), "Last minute accepted");
Check(!Schedule.TryTime("24:00", out _) && !Schedule.TryTime("9点", out _) && !Schedule.TryTime("09:60", out _), "Invalid time rejected");
Check(Math.Abs((Schedule.BeijingNow - DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(8)).DateTime).TotalSeconds) < 1, "Beijing UTC+8 independent of local zone");
var la = "Pacific Standard Time";
var laTask = new Reminder { Title = "US morning task", Due = new DateTime(2026, 10, 27, 9, 0, 0), TimeZoneId = la };
var laInstant = new DateTimeOffset(2026, 10, 27, 16, 0, 0, TimeSpan.Zero);
Check(Schedule.Instant(laTask.Due, la) == laInstant, "New Los Angeles task uses US local time");
Check(Schedule.OccurrenceInstant(laTask, laInstant.AddSeconds(-1)) == null && Schedule.OccurrenceInstant(laTask, laInstant) == laInstant, "US task triggers at selected-zone time");
Check(Schedule.Local(laInstant, Schedule.BeijingZone) == new DateTime(2026, 10, 28, 0, 0, 0), "Same reminder converts across calendar day");
Check(Schedule.Local(laInstant, "Tokyo Standard Time") == new DateTime(2026, 10, 28, 1, 0, 0) && Schedule.Instant(laTask.Due, laTask.TimeZoneId) == laInstant, "Changing display zone preserves the reminder instant");
Check(Schedule.Local(Schedule.Instant(laTask.Due, la), la) == laTask.Due, "Edit display round trip preserves task time");
Check(Schedule.Instant(new DateTime(2026, 1, 1, 9, 0, 0), la).Hour == 17, "US winter offset follows standard time");
Check(Schedule.Zone(la).IsInvalidTime(new DateTime(2026, 3, 8, 2, 30, 0)), "US spring gap detected");
bool gapRejected = false;
try { Schedule.Instant(new DateTime(2026, 3, 8, 2, 30, 0), la); } catch (ArgumentException) { gapRejected = true; }
Check(gapRejected, "Invalid local task time rejected");
Check(Schedule.Instant(new DateTime(2026, 11, 1, 1, 30, 0), la) == new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.Zero), "Repeated hour resolves to later occurrence");
var spring = new Reminder { Title = "Daily DST", Due = new DateTime(2026, 3, 7, 2, 30, 0), TimeZoneId = la, Daily = true, LastNotified = new DateTime(2026, 3, 7, 2, 30, 0) };
var springTrigger = new DateTimeOffset(2026, 3, 8, 10, 0, 0, TimeSpan.Zero);
Check(Schedule.OccurrenceInstant(spring, springTrigger.AddSeconds(-1)) == null && Schedule.OccurrenceInstant(spring, springTrigger) == springTrigger, "Daily DST gap shifts to first valid minute");
spring.LastNotified = Schedule.Local(springTrigger, la);
Check(Schedule.OccurrenceInstant(spring, springTrigger.AddHours(3)) == null, "Shifted daily DST reminder does not repeat");
var morning = new Reminder { Title = "Daily morning", Due = new DateTime(2026, 3, 7, 9, 0, 0), TimeZoneId = la, Daily = true };
Check((Schedule.NextInstant(morning, Schedule.Instant(morning.Due, la)) - Schedule.Instant(morning.Due, la)).TotalHours == 23, "Daily local time survives 23-hour spring day");
morning.Due = new DateTime(2026, 10, 31, 9, 0, 0);
Check((Schedule.NextInstant(morning, Schedule.Instant(morning.Due, la)) - Schedule.Instant(morning.Due, la)).TotalHours == 25, "Daily local time survives 25-hour fall day");
var countdown = new Countdown();
var timerNow = new DateTimeOffset(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);
countdown.Start(TimeSpan.FromSeconds(10), timerNow);
Check(countdown.Running && countdown.Remaining(timerNow.AddSeconds(4)) == TimeSpan.FromSeconds(6), "Countdown uses deadline instead of UI ticks");
countdown.Pause(timerNow.AddSeconds(4));
Check(!countdown.Running && countdown.Remaining(timerNow.AddHours(1)) == TimeSpan.FromSeconds(6), "Paused countdown remains stable");
countdown.Start(countdown.Remaining(timerNow), timerNow.AddHours(1));
Check(!countdown.Poll(timerNow.AddHours(1).AddSeconds(5)) && countdown.Poll(timerNow.AddHours(1).AddSeconds(6)), "Resumed timer fires exactly at deadline");
Check(!countdown.Poll(timerNow.AddHours(2)) && !countdown.HasStarted, "Countdown completion fires only once");
countdown.Start(TimeSpan.FromMinutes(1), timerNow);
Check(countdown.Poll(timerNow.AddHours(2)), "Timer catches up after long suspension");
countdown.Start(TimeSpan.FromMinutes(5), timerNow); countdown.Reset();
Check(!countdown.Running && !countdown.Poll(timerNow.AddHours(1)), "Reset cancels countdown");
bool zeroRejected = false;
try { countdown.Start(TimeSpan.Zero, timerNow); } catch (ArgumentOutOfRangeException) { zeroRejected = true; }
Check(zeroRejected, "Zero countdown duration rejected");
Check(WeatherLocations.Countries.Length == 20 && WeatherLocations.Cities.Count == 44, "Twenty weather countries and 44 verified cities");
Check(WeatherLocations.Cities.All(c => c.Latitude is >= -90 and <= 90 && c.Longitude is >= -180 and <= 180 && c.GeoNamesId > 0), "Weather city coordinates validated");
Check(WeatherLocations.Find("missing-city").Id == "CN-Beijing", "Unknown city falls back to Beijing");
Check(ChinaRegionService.Provinces.Count == 34 && ChinaRegionService.Provinces.All(ChinaRegionService.Valid), "34 province-level regions have validated coordinates");
const string districtJson = """
{"features":[{"properties":{"adcode":110108,"name":"海淀区","level":"district","parent":{"adcode":110000},"childrenNum":0,"center":[116.310316,39.956074]}},{"properties":{"adcode":110105,"name":"朝阳区","level":"district","parent":{"adcode":110000},"childrenNum":0,"center":[116.486409,39.921489]}},{"properties":{"adcode":"100000_JD","name":"","level":null}}]}
""";
var districts = ChinaRegionService.Parse(districtJson, "110000");
Check(districts.Count == 2 && districts.Any(r => r.Name == "海淀区"), "Municipality districts parse with administrative names");
var selectedDistrict = districts.Single(r => r.Name == "海淀区").ToWeatherCity("北京市 / 海淀区");
Check(WeatherLocations.ValidCustom(selectedDistrict, selectedDistrict.Id) && selectedDistrict.Latitude == 39.956074 && selectedDistrict.AdministrativePath == "北京市 / 海淀区", "Selected district becomes weather query location without geolocation");
Check(!WeatherLocations.ValidCustom(selectedDistrict with { Latitude = double.NaN }, selectedDistrict.Id), "Invalid saved custom coordinates rejected");
bool wrongParentRejected = false;
try { ChinaRegionService.Parse(districtJson, "420100"); } catch (InvalidDataException) { wrongParentRejected = true; }
Check(wrongParentRejected, "Districts from another parent are not offered");
Check(WeatherMath.Beaufort(0) == 0 && WeatherMath.Beaufort(.29) == 0 && WeatherMath.Beaufort(.3) == 1, "Calm and Beaufort first boundary");
Check(WeatherMath.Beaufort(3.39) == 2 && WeatherMath.Beaufort(3.4) == 3 && WeatherMath.Beaufort(32.7) == 12, "Beaufort moderate and hurricane boundaries");
Check(WeatherMath.Direction(0) == "N" && WeatherMath.Direction(360) == "N" && WeatherMath.Direction(-45) == "NW", "Wind direction wraps at north");
Check(WeatherMath.Direction(22.49) == "N" && WeatherMath.Direction(22.5) == "NE" && WeatherMath.Direction(270) == "W", "Wind compass boundaries");
Check(WeatherMath.Condition(0, true).Icon != WeatherMath.Condition(0, false).Icon, "Clear day and night use different icons");
Check(WeatherMath.Condition(95, true).English == "Thunderstorm" && WeatherMath.Condition(999, true).English == "Unknown weather", "Weather code mapping and unknown fallback");
const string weatherJson = """
{"current":{"weather_code":0,"is_day":1,"wind_speed_10m":2.36,"wind_direction_10m":328},"daily":{"time":["2026-10-27"],"temperature_2m_min":[6.9],"temperature_2m_max":[14.0]}}
""";
var weather = WeatherService.Parse(weatherJson, timerNow);
Check(weather.Low == 6.9 && weather.High == 14 && weather.LocalDate == "2026-10-27" && weather.WindSpeed == 2.36, "Weather response parses daily range and current wind");
foreach (var invalidWeather in new[] { "{}", weatherJson.Replace("6.9", "null"), weatherJson.Replace("6.9", "20.0"), weatherJson.Replace("328", "999"), weatherJson.Replace("2026-10-27", "bad-date") })
{
    bool invalidRejected = false;
    try { WeatherService.Parse(invalidWeather, timerNow); } catch (System.IO.InvalidDataException) { invalidRejected = true; }
    Check(invalidRejected, "Incomplete or invalid weather response rejected");
}
Check(WeatherService.ForecastUri(WeatherLocations.Find("CN-Beijing")).Host == "api.open-meteo.com" && WeatherService.ForecastUri(WeatherLocations.Find("US-LosAngeles")).Query.Contains("timezone=auto"), "Weather requests use fixed HTTPS API and city-local date");
Check(UiText.Languages.Length == 10 && UiText.Supported("ja-JP") && !UiText.Supported("xx-XX"), "Ten supported UI languages");
foreach (var language in UiText.Languages)
{
    Check(!string.IsNullOrWhiteSpace(UiText.Get(language.Id, "开始", "Start")), "Translation loads for " + language.Id);
}
var directory = Path.Combine(AppContext.BaseDirectory, "test-data-" + Guid.NewGuid());
try
{
    var store = new TaskStore(Path.Combine(directory, "reminders.json"));
    Check(store.Load().Count == 0, "Fresh store is empty");
    store.Save(new() { task });
    var loaded = store.Load().Single();
    Check(loaded.Title == task.Title && loaded.Due == due && loaded.LastNotified == task.LastNotified && loaded.Completed && loaded.Daily, "Round trip Unicode and schedule state");
    store.Save(new());
    Check(File.Exists(store.FilePath + ".bak") && store.Load().Count == 0, "Atomic save retains backup");
    File.WriteAllText(store.FilePath, "invalid json");
    bool rejected = false;
    try { store.Load(); } catch (System.Text.Json.JsonException) { rejected = true; }
    Check(rejected && File.ReadAllText(store.FilePath) == "invalid json", "Corrupt store rejected without overwrite");
    File.WriteAllText(store.FilePath, "[{\"Title\":\"Legacy Beijing task\",\"Due\":\"2026-10-27T09:00:00\"}]");
    Check(store.Load().Single().TimeZoneId == Schedule.BeijingZone, "Old tasks without timezone remain Beijing tasks");
    store.Save(new() { laTask });
    Check(store.Load().Single().TimeZoneId == la && Schedule.Instant(store.Load().Single().Due, la) == laInstant, "Task timezone survives restart");
    var settingsPath = Path.Combine(directory, "settings.json");
    var settings = new SettingsStore(settingsPath);
    Check(settings.Load().TimeZoneId == Schedule.BeijingZone && settings.Load().LanguageCode == "zh-CN", "Fresh settings default to Beijing and Chinese");
    settings.Save(new UserSettings { TimeZoneId = la, LanguageCode = "en-US" });
    var reopened = new SettingsStore(settingsPath).Load();
    Check(reopened.TimeZoneId == la && reopened.LanguageCode == "en-US", "Timezone and language persist across restart");
    settings.Save(new UserSettings { WeatherCityId = "US-LosAngeles" });
    Check(settings.Load().WeatherCityId == "US-LosAngeles", "Selected weather city survives restart");
    settings.Save(new UserSettings { WeatherCityId = selectedDistrict.Id, CustomWeatherCity = selectedDistrict, ChinaProvinceCode = "110000", ChinaCityCode = "110000", ChinaDistrictCode = "110108" });
    var customReopened = settings.Load();
    Check(customReopened.ChinaDistrictCode == "110108" && customReopened.CustomWeatherCity?.AdministrativePath == "北京市 / 海淀区" && customReopened.CustomWeatherCity.Latitude == selectedDistrict.Latitude, "District choice and representative coordinates survive restart");
    File.WriteAllText(settingsPath, "{\"TimeZoneId\":\"Not a real zone\",\"LanguageCode\":\"en-US\"}");
    bool zoneRejected = false;
    try { settings.Load(); } catch (TimeZoneNotFoundException) { zoneRejected = true; }
    Check(zoneRejected, "Invalid stored timezone rejected");
}
finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
Console.WriteLine($"All {checks} checks passed.");
if (args.Contains("--weather-live"))
{
    var live = new WeatherService().FetchAsync(WeatherLocations.Find("CN-Beijing"), CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"LIVE WEATHER OK: Beijing {live.LocalDate}; {live.Low}–{live.High} C; {live.WindSpeed} m/s; code {live.Code}");
}
if (args.Contains("--china-live"))
{
    var regions = new ChinaRegionService(Path.Combine(AppContext.BaseDirectory, "region-live-cache"));
    var beijingRegions = regions.ChildrenAsync(ChinaRegionService.Provinces.Single(r => r.Code == "110000"), CancellationToken.None).GetAwaiter().GetResult();
    var haidian = beijingRegions.Single(r => r.Name == "海淀区");
    var hubei = regions.ChildrenAsync(ChinaRegionService.Provinces.Single(r => r.Code == "420000"), CancellationToken.None).GetAwaiter().GetResult();
    var wuhan = hubei.Single(r => r.Name == "武汉市");
    var wuhanDistricts = regions.ChildrenAsync(wuhan, CancellationToken.None).GetAwaiter().GetResult();
    if (!wuhanDistricts.Any(r => r.Name == "洪山区")) throw new Exception("Wuhan district missing");
    var live = new WeatherService().FetchAsync(haidian.ToWeatherCity("北京市 / 海淀区"), CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"LIVE CHINA OK: Beijing {beijingRegions.Count} districts; Wuhan {wuhanDistricts.Count} districts; Haidian weather {live.Low}–{live.High} C");
}
