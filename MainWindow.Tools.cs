using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ShiSha;

public partial class MainWindow
{
    private readonly SettingsStore settingsStore = new(Path.Combine(AppContext.BaseDirectory, "data", "settings.json"));
    private UserSettings preferences = new();
    private bool settingsHealthy = true;
    private readonly Stopwatch stopwatch = new();
    private readonly Countdown countdown = new();
    private readonly DispatcherTimer toolsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private string? openTool;
    private bool countdownFinished;
    private bool Chinese => preferences.LanguageCode.StartsWith("zh", StringComparison.Ordinal);
    private string T(string chinese, string english) => UiText.Get(preferences.LanguageCode, chinese, english);
    private DateTime DisplayTime(DateTimeOffset instant) => Schedule.Local(instant, preferences.TimeZoneId);
    private DateTime DisplayNow => DisplayTime(DateTimeOffset.UtcNow);
    private sealed record ZoneChoice(string Id, string Label);

    private void LoadPreferences()
    {
        try { preferences = settingsStore.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            settingsHealthy = false;
            SettingsMessage.Text = T("设置文件读取失败，暂用北京时间和中文。原文件未覆盖。", "Cannot read settings. Using Beijing and Chinese; the original file is kept.");
        }
    }
    private void InitializeTools()
    {
        ApplyLanguage();
        toolsTimer.Tick += (_, _) => UpdateTools();
        toolsTimer.Start();
    }
    private void ToggleStopwatch(object sender, RoutedEventArgs e) => ToggleTool("stopwatch");
    private void ToggleCountdown(object sender, RoutedEventArgs e) => ToggleTool("countdown");
    private void ToggleSettings(object sender, RoutedEventArgs e) => ToggleTool("settings");
    private void ToggleTool(string tool)
    {
        openTool = openTool == tool ? null : tool;
        ToolPanel.Visibility = openTool == null ? Visibility.Collapsed : Visibility.Visible;
        StopwatchPanel.Visibility = openTool == "stopwatch" ? Visibility.Visible : Visibility.Collapsed;
        CountdownPanel.Visibility = openTool == "countdown" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = openTool == "settings" ? Visibility.Visible : Visibility.Collapsed;
        // Visibility switches must also invalidate the outer auto-sized container.
        ToolPanel.InvalidateMeasure();
        StopwatchToggle.Foreground = Brush(openTool == "stopwatch" ? "#9EDDCB" : "#9DAFC5");
        CountdownToggle.Foreground = Brush(openTool == "countdown" ? "#9EDDCB" : "#9DAFC5");
        SettingsToggle.Foreground = Brush(openTool == "settings" ? "#9EDDCB" : "#9DAFC5");
        UpdateWindowSize();
    }
    private void UpdateWindowSize()
    {
        bool memo = MemoPanel.Visibility == Visibility.Visible;
        MinWidth = memo ? 920 : 400;
        MinHeight = memo ? 650 : 370;
        Width = memo ? 960 : 420;
        Height = openTool == "settings" ? 800 : memo ? 680 : openTool == null ? 380 : openTool == "stopwatch" ? 550 : 650;
        var workArea = SystemParameters.WorkArea;
        if (!double.IsNaN(Left)) Left = Math.Max(workArea.Left, Math.Min(Left, workArea.Right - Width));
        if (!double.IsNaN(Top)) Top = Math.Max(workArea.Top, Math.Min(Top, workArea.Bottom - Height));
    }
    private void StartStopwatch(object sender, RoutedEventArgs e)
    {
        if (stopwatch.IsRunning) stopwatch.Stop(); else stopwatch.Start();
        UpdateTools();
    }
    private void ResetStopwatch(object sender, RoutedEventArgs e) { stopwatch.Reset(); UpdateTools(); }
    private static string DurationText(TimeSpan value, bool tenths = false)
        => $"{(int)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}" + (tenths ? $".{value.Milliseconds / 100}" : "");
    private bool TryDuration(out TimeSpan duration)
    {
        duration = default;
        if (!int.TryParse(HoursInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) || hours < 0 || hours > 99
            || !int.TryParse(MinutesInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes < 0 || minutes > 59
            || !int.TryParse(SecondsInput.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds < 0 || seconds > 59) return false;
        duration = new TimeSpan(hours, minutes, seconds);
        return duration > TimeSpan.Zero;
    }
    private void StartCountdown(object sender, RoutedEventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        if (countdown.Poll(now)) { FinishCountdown(); return; }
        if (countdown.Running) countdown.Pause(now);
        else
        {
            var duration = countdown.Remaining(now);
            if (!countdown.HasStarted && !TryDuration(out duration))
            {
                CountdownError.Text = T("请输入有效时长：小时0–99，分和秒0–59，总时长大于0。", "Use hours 0–99, minutes/seconds 0–59; duration must be positive.");
                return;
            }
            countdown.Start(duration, now); countdownFinished = false; CountdownError.Text = "";
        }
        UpdateTools();
    }
    private void ResetCountdown(object sender, RoutedEventArgs e)
    { countdown.Reset(); countdownFinished = false; CountdownError.Text = ""; UpdateTools(); }
    private void UpdateTools()
    {
        StopwatchDisplay.Text = DurationText(stopwatch.Elapsed, true);
        StopwatchStart.Content = stopwatch.IsRunning ? T("暂停", "Pause") : stopwatch.Elapsed > TimeSpan.Zero ? T("继续", "Resume") : T("开始", "Start");
        var now = DateTimeOffset.UtcNow;
        if (countdown.Poll(now)) FinishCountdown();
        var remaining = countdown.Remaining(now);
        if (!countdown.HasStarted && !countdownFinished) TryDuration(out remaining);
        CountdownDisplay.Text = DurationText(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)));
        CountdownStart.Content = countdown.Running ? T("暂停", "Pause") : countdown.HasStarted ? T("继续", "Resume") : T("开始", "Start");
        HoursInput.IsEnabled = MinutesInput.IsEnabled = SecondsInput.IsEnabled = !countdown.HasStarted;
        FooterHint.Text = countdown.Running ? T("倒计时运行中", "Timer running") : stopwatch.IsRunning ? T("秒表运行中", "Stopwatch running") : T("关闭后留在托盘", "Close to tray");
    }
    private void FinishCountdown()
    {
        countdownFinished = true;
        System.Media.SystemSounds.Exclamation.Play();
        var popup = new Window
        {
            Title = T("时砂 · 计时结束", "时砂 · Timer finished"), Width = 340, Height = 190,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, WindowStyle = WindowStyle.None,
            AllowsTransparency = true, Background = System.Windows.Media.Brushes.Transparent,
            Foreground = System.Windows.Media.Brushes.White, FontFamily = FontFamily, Topmost = true, ResizeMode = ResizeMode.NoResize
        };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = T("计时结束", "Time is up"), FontSize = 24, Margin = new Thickness(0, 0, 0, 12) });
        panel.Children.Add(new TextBlock { Text = T("休息一下，或开始下一件事。", "Take a break, or start your next task."), FontSize = 12 });
        var ok = new Button { Content = T("知道了", "OK"), Margin = new Thickness(0, 20, 0, 0), Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Left };
        ok.Click += (_, _) => popup.Close(); panel.Children.Add(ok);
        popup.Content = new Border { Child = panel, Background = Brush("#17263A"), CornerRadius = new CornerRadius(16) };
        popup.Show();
        tray.ShowBalloonTip(4000, T("计时结束", "Time is up"), T("时砂倒计时已结束。", "Your 时砂 timer has finished."), Forms.ToolTipIcon.Info);
    }
    private string ZoneLabel(TimeZoneInfo zone)
    {
        return zone.Id switch
        {
            "China Standard Time" => T("北京", "Beijing"),
            "Pacific Standard Time" => T("洛杉矶 / 美国太平洋时间", "Los Angeles / US Pacific"),
            "Eastern Standard Time" => T("纽约 / 美国东部时间", "New York / US Eastern"),
            "Central Standard Time" => T("芝加哥 / 美国中部时间", "Chicago / US Central"),
            "Mountain Standard Time" => T("丹佛 / 美国山地时间", "Denver / US Mountain"),
            "Tokyo Standard Time" => T("东京", "Tokyo"),
            "GMT Standard Time" => T("伦敦", "London"),
            "UTC" => "UTC",
            _ => Chinese ? zone.DisplayName : zone.Id
        };
    }
    private void SaveSettings(object sender, RoutedEventArgs e)
    {
        if (!settingsHealthy) return;
        if (ZoneInput.SelectedValue is not string zone || LanguageInput.SelectedValue is not string language) return;
        var previousCity = preferences.WeatherCityId;
        var china = WeatherCountryInput.SelectedValue as string == "CN";
        var newChinaSelection = regionSelectionChanged || CurrentWeatherCity.CountryCode != "CN";
        if (china && newChinaSelection && regionLoading) { SettingsMessage.Text = T("地区还在加载，请稍等。", "Regions are still loading. Please wait."); return; }
        var selectedRegion = china && newChinaSelection ? SelectedChinaRegion() : china ? CurrentWeatherCity : null;
        var updated = new UserSettings
        {
            TimeZoneId = zone, LanguageCode = language,
            WeatherCityId = selectedRegion?.Id ?? WeatherCityInput.SelectedValue as string ?? preferences.WeatherCityId,
            CustomWeatherCity = selectedRegion?.Id.StartsWith("CN-AD-", StringComparison.Ordinal) == true ? selectedRegion : null,
            ChinaProvinceCode = china ? ChinaProvinceInput.SelectedValue as string ?? preferences.ChinaProvinceCode : preferences.ChinaProvinceCode,
            ChinaCityCode = china ? ChinaCityInput.SelectedValue as string ?? preferences.ChinaCityCode : preferences.ChinaCityCode,
            ChinaDistrictCode = china ? ChinaDistrictInput.SelectedValue as string ?? preferences.ChinaDistrictCode : preferences.ChinaDistrictCode
        };
        try { settingsStore.Save(updated); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { SettingsMessage.Text = T("保存失败，请检查程序目录写入权限。", "Cannot save. Check write access to the app folder."); return; }
        preferences = updated;
        regionSelectionChanged = false;
        ApplyLanguage();
        if (editing is Guid id && tasks.FirstOrDefault(t => t.Id == id) is Reminder task) FillEditor(task);
        else DateInput.SelectedDate = DisplayNow.Date;
        SettingsMessage.Text = T("已保存。新任务按所选时区设置，已有提醒时刻保持不变。", "Saved. New tasks use this zone; existing reminders keep their scheduled instants.");
        RenderTasks(); Tick();
        if (previousCity != preferences.WeatherCityId) _ = RefreshWeatherAsync();
    }
    private void ApplyLanguage()
    {
        void Translate(DependencyObject node)
        {
            if (node is FrameworkElement element && element.Tag is string text && text.Contains('|'))
            {
                var parts = text.Split('|');
                if (element is TextBlock label) label.Text = T(parts[0], parts[1]);
                else if (element is ContentControl control) control.Content = T(parts[0], parts[1]);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>()) Translate(child);
        }
        Translate(this);
        Language = XmlLanguage.GetLanguage(preferences.LanguageCode);
        var zone = Schedule.Zone(preferences.TimeZoneId);
        Title = T("时砂 · ", "时砂 · ") + ZoneLabel(zone);
        tray.Text = Title.Length > 63 ? Title[..63] : Title;
        tray.ContextMenuStrip!.Items[0].Text = T("打开时砂", "Open 时砂");
        tray.ContextMenuStrip.Items[1].Text = T("退出", "Exit");
        void Tip(Button button, string chinese, string english)
        { button.ToolTip = T(chinese, english); AutomationProperties.SetName(button, T(chinese, english)); }
        Tip(MemoToggle, "备忘录", "Reminders"); Tip(StopwatchToggle, "秒表", "Stopwatch");
        Tip(CountdownToggle, "计时器", "Timer"); Tip(SettingsToggle, "设置", "Settings");
        Tip(MinimizeButton, "最小化", "Minimize"); Tip(CloseButton, "关闭到托盘", "Close to tray");
        HeaderDragArea.ToolTip = T("按住此处拖动窗口", "Drag here to move the window");
        EditorTitle.Text = editing == null ? T("添加一件事", "Add a task") : T("编辑提醒", "Edit reminder");
        SaveButton.Content = editing == null ? T("＋ 添加提醒", "+ Add reminder") : T("保存修改", "Save changes");
        TitleInput.ToolTip = T("任务内容，例如：去健身", "Task, e.g. go to the gym");
        AutomationProperties.SetName(TitleInput, T("任务内容", "Task"));
        AutomationProperties.SetName(DateInput, T("提醒日期", "Reminder date"));
        AutomationProperties.SetName(TimeInput, T("提醒时间", "Reminder time"));
        AutomationProperties.SetName(ZoneInput, T("时区", "Time zone"));
        AutomationProperties.SetName(LanguageInput, T("语言", "Language"));
        AutomationProperties.SetName(HoursInput, T("小时", "Hours"));
        AutomationProperties.SetName(MinutesInput, T("分钟", "Minutes"));
        AutomationProperties.SetName(SecondsInput, T("秒", "Seconds"));
        TaskZoneCaption.Text = ZoneLabel(zone) + T(" · 日期 / 24小时制时间", " · Date / 24-hour time");
        ZoneInput.ItemsSource = TimeZoneInfo.GetSystemTimeZones().Select(z => new ZoneChoice(z.Id, ZoneLabel(z))).ToList();
        ZoneInput.SelectedValue = preferences.TimeZoneId;
        LanguageInput.ItemsSource = UiText.Languages; LanguageInput.SelectedValue = preferences.LanguageCode;
        ApplyWeatherLanguage();
        ToolPanel.InvalidateMeasure();
        if (!settingsHealthy) return;
        SettingsMessage.Text = T("时钟与新任务使用所选时区。", "Clock and new tasks use the selected time zone.");
    }
    private void FillEditor(Reminder task)
    {
        editing = task.Id; TitleInput.Text = task.Title;
        var wall = DisplayTime(Schedule.Instant(task.Due, task.TimeZoneId));
        DateInput.SelectedDate = wall.Date; TimeInput.Text = wall.ToString("HH:mm");
        DailyInput.IsChecked = task.Daily; EditorTitle.Text = T("编辑提醒", "Edit reminder");
        SaveButton.Content = T("保存修改", "Save changes"); CancelButton.Visibility = Visibility.Visible;
    }
}
