using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace ShiSha;

public partial class MainWindow : Window
{
    private readonly TaskStore store = new(Path.Combine(AppContext.BaseDirectory, "data", "reminders.json"));
    private readonly Forms.NotifyIcon tray;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private List<Reminder> tasks = new();
    private Guid? editing;
    private bool storageHealthy = true;
    private bool exiting;
    private readonly string startupLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "时砂.lnk");

    public MainWindow(bool startInTray, bool fetchWeather = true)
    {
        weatherNetworking = fetchWeather;
        InitializeComponent();
        LoadPreferences();
        ToggleMemo(this, new RoutedEventArgs());
        DateInput.SelectedDate = DisplayNow.Date;
        StartupToggle.IsChecked = File.Exists(startupLink);
        try { tasks = store.Load(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            storageHealthy = false;
            FormError.Text = T("任务文件读取失败，已停止写入以保护原文件。请检查 data/reminders.json 后重启。", "Cannot read reminders. Saving is disabled to protect the original file. Check data/reminders.json and restart.");
        }
        tray = new Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!) ?? System.Drawing.SystemIcons.Information, Text = "时砂 · 北京时间", Visible = true
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开时砂", null, (_, _) => Dispatcher.Invoke(RestoreWindow));
        menu.Items.Add("退出", null, (_, _) => Dispatcher.Invoke(ExitApp));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreWindow);
        tray.BalloonTipClicked += (_, _) => Dispatcher.Invoke(RestoreWindow);
        InitializeTools();
        Closing += OnClosing;
        timer.Tick += (_, _) => Tick();
        RenderTasks();
        Tick();
        timer.Start();
    }

    private void RestoreWindow()
    {
        Show(); WindowState = WindowState.Normal; Activate();
    }
    private void DragHeader(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void HideWindow(object sender, RoutedEventArgs e) => Close();
    private void ToggleMemo(object sender, RoutedEventArgs e)
    {
        bool hide = MemoPanel.Visibility == Visibility.Visible;
        MemoPanel.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        MemoColumn.Width = new GridLength(hide ? 0 : 440);
        ClockPanel.Margin = new Thickness(0, 0, hide ? 0 : 32, 0);
        MemoToggle.Foreground = Brush(hide ? "#9DAFC5" : "#9EDDCB");
        UpdateWindowSize();
    }
    private void ExitApp()
    {
        exiting = true; timer.Stop(); toolsTimer.Stop(); weatherRequest?.Cancel(); regionLifetime.Cancel(); tray.Visible = false; tray.Dispose();
        Close(); Application.Current.Shutdown();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (exiting) return;
        e.Cancel = true; Hide();
        tray.ShowBalloonTip(2500, T("时砂仍在运行", "时砂 is still running"), T("到点会提醒。双击托盘图标打开，右键可退出。", "Reminders stay active. Double-click the tray icon to open; right-click to exit."), Forms.ToolTipIcon.Info);
    }
    private bool Persist()
    {
        if (!storageHealthy) return false;
        try { store.Save(tasks); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            FormError.Text = T("保存失败：请检查程序目录权限和磁盘空间。本次改动暂存在内存。", "Cannot save. Check folder permissions and disk space. Changes are currently in memory only.");
            return false;
        }
    }
    private void Tick()
    {
        if (weatherNetworking && !weatherLoading && DateTimeOffset.UtcNow >= nextWeatherRefresh) _ = RefreshWeatherAsync();
        var now = DateTimeOffset.UtcNow;
        var local = DisplayTime(now);
        Clock.Text = local.ToString("HH:mm"); Seconds.Text = local.ToString("ss") + T(" 秒", " s");
        Calendar.Text = local.ToString("D", CultureInfo.GetCultureInfo(preferences.LanguageCode));
        var zone = Schedule.Zone(preferences.TimeZoneId);
        var offset = TimeZoneInfo.ConvertTime(now, zone).Offset;
        ZoneCaption.Text = "●  " + ZoneLabel(zone) + " / UTC" + (offset < TimeSpan.Zero ? "−" : "+") + offset.Duration().ToString(@"hh\:mm");
        var active = tasks.Where(t => !t.Completed).OrderBy(t => Schedule.NextInstant(t, now)).ToList();
        NextTask.Text = active.Count == 0 ? T("留一点空白，安排你的下一件事。", "Make room for your next task.") : DisplayTime(Schedule.NextInstant(active[0], now)).ToString("MM/dd HH:mm") + "  ·  " + active[0].Title;
        var due = new List<(Reminder task, DateTimeOffset occurrence)>();
        if (storageHealthy)
        {
            foreach (var task in tasks)
                if (Schedule.OccurrenceInstant(task, now) is DateTimeOffset occurrence) due.Add((task, occurrence));
        }
        if (due.Count == 0) return;
        foreach (var item in due) item.task.LastNotified = Schedule.Local(item.occurrence, item.task.TimeZoneId);
        // Persist before showing; prevents duplicate notifications on the next tick or restart.
        Persist(); RenderTasks();
        SystemSounds.Exclamation.Play();
        var popup = new Window
        {
            Title = T("时砂 · 任务提醒", "时砂 · Reminder"), Width = 440, Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterScreen, Topmost = true,
            Background = Brush("#17263A"), Foreground = Brushes.White, FontFamily = FontFamily,
            ResizeMode = ResizeMode.CanMinimize
        };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = T("该做这件事了", "Time for your task"), FontSize = 24, Margin = new Thickness(0, 0, 0, 15) });
        var lines = new StackPanel();
        foreach (var (task, occurrence) in due)
            lines.Children.Add(new TextBlock { Text = $"{DisplayTime(occurrence):MM/dd HH:mm}  {task.Title}" + (now - occurrence > TimeSpan.FromMinutes(1) ? T("（已错过，补提醒）", " (missed reminder)") : ""), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), FontSize = 16 });
        panel.Children.Add(new ScrollViewer { Content = lines, MaxHeight = 165, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 15, 0, 0) };
        var done = new Button { Content = T("标记完成", "Mark done"), Padding = new Thickness(15, 9, 15, 9), Margin = new Thickness(0, 0, 12, 0) };
        done.Click += (_, _) =>
        {
            foreach (var (task, _) in due) if (!task.Daily && tasks.Contains(task)) task.Completed = true;
            Persist(); RenderTasks(); popup.Close();
        };
        var ok = new Button { Content = T("知道了", "OK"), Padding = new Thickness(15, 9, 15, 9) };
        ok.Click += (_, _) => popup.Close(); buttons.Children.Add(done); buttons.Children.Add(ok);
        panel.Children.Add(buttons); panel.Children.Add(new TextBlock { Text = T("每日任务会在次日继续提醒。", "Daily tasks repeat on the next day."), Foreground = Brush("#A0B5CD"), Margin = new Thickness(0, 12, 0, 0), FontSize = 11 });
        popup.Content = panel; popup.Show();
        tray.ShowBalloonTip(5000, T("任务到时间了", "Reminder"), due[0].task.Title, Forms.ToolTipIcon.Info);
    }
    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
    private void SaveTask(object sender, RoutedEventArgs e)
    {
        if (!storageHealthy) return;
        var title = TitleInput.Text.Trim();
        if (title.Length == 0) { FormError.Text = T("先写下要做的事。", "Enter a task first."); return; }
        if (DateInput.SelectedDate == null || !Schedule.TryTime(TimeInput.Text, out var time))
        { FormError.Text = T("请选择日期，时间格式为 HH:mm，例如 09:00 或 21:00。", "Choose a date and use HH:mm, e.g. 09:00 or 21:00."); return; }
        var due = DateTime.SpecifyKind(DateInput.SelectedDate.Value.Date + time, DateTimeKind.Unspecified);
        var daily = DailyInput.IsChecked == true;
        var selectedZone = Schedule.Zone(preferences.TimeZoneId);
        if (selectedZone.IsInvalidTime(due)) { FormError.Text = T("所选时间处于夏令时跳过的时段，请换一个时间。", "This time does not exist due to daylight saving. Choose another time."); return; }
        if (selectedZone.IsAmbiguousTime(due) && MessageBox.Show(T("所选时间因夏令时回拨会出现两次，将使用较晚的一次，是否继续？", "This time occurs twice due to daylight saving. Use the later occurrence?"), T("确认提醒时间", "Confirm reminder time"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        if (Schedule.Instant(due, preferences.TimeZoneId) <= DateTimeOffset.UtcNow && MessageBox.Show(T("所选时区的时间已过去。保存后会立即补提醒，是否继续？", "This time has passed in the selected zone. Save and remind now?"), T("确认提醒时间", "Confirm reminder time"), MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        var task = tasks.FirstOrDefault(t => t.Id == editing);
        if (task == null) { task = new Reminder(); tasks.Add(task); }
        task.Title = title; task.Due = due; task.TimeZoneId = preferences.TimeZoneId; task.Daily = daily; task.Completed = false; task.LastNotified = null;
        if (Persist()) { ResetEditor(); RenderTasks(); Tick(); }
        else RenderTasks();
    }
    private void CancelEdit(object sender, RoutedEventArgs e) => ResetEditor();
    private void ResetEditor()
    {
        editing = null; TitleInput.Clear(); DateInput.SelectedDate = DisplayNow.Date;
        DailyInput.IsChecked = false; EditorTitle.Text = T("添加一件事", "Add a task");
        SaveButton.Content = T("＋ 添加提醒", "+ Add reminder"); CancelButton.Visibility = Visibility.Collapsed;
        if (storageHealthy) FormError.Text = "";
    }
    private void RenderTasks()
    {
        TaskList.Children.Clear();
        TaskCount.Text = tasks.Count(t => !t.Completed) + T(" 项待办", " pending");
        if (tasks.Count == 0)
            TaskList.Children.Add(new TextBlock { Text = T("还没有安排。\n添加一件事，让时砂替你记住。", "Nothing planned yet.\nAdd a task and let 时砂 remember it."), Foreground = Brush("#97ABC3"), Margin = new Thickness(8, 18, 0, 0), LineHeight = 26 });
        foreach (var task in tasks.OrderBy(t => t.Completed).ThenBy(t => Schedule.NextInstant(t, DateTimeOffset.UtcNow)))
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = task.Title, TextWrapping = TextWrapping.Wrap, FontSize = 15, Foreground = Brush(task.Completed ? "#8190A2" : "#F4F6FA") });
            panel.Children.Add(new TextBlock { Text = DisplayTime(Schedule.NextInstant(task, DateTimeOffset.UtcNow)).ToString("MM/dd HH:mm") + (task.Daily ? T(" · 每天", " · Daily") : T(" · 单次", " · Once")) + (task.Completed ? T(" · 已完成", " · Done") : task.LastNotified != null && !task.Daily ? T(" · 已提醒", " · Notified") : T(" · 待提醒", " · Pending")), Foreground = Brush("#9AAEC6"), FontSize = 12, Margin = new Thickness(0, 7, 0, 12) });
            if (task.Daily) panel.Children.Add(new TextBlock { Text = T("重复时区：", "Repeats in: ") + ZoneLabel(Schedule.Zone(task.TimeZoneId)), Foreground = Brush("#9AAEC6"), FontSize = 10, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            var actions = new StackPanel { Orientation = Orientation.Horizontal };
            void Action(string label, Action action)
            {
                var button = new Button { Content = label, Padding = new Thickness(11, 5, 11, 5), FontSize = 12, IsEnabled = storageHealthy };
                button.Click += (_, _) => action(); actions.Children.Add(button);
            }
            if (!task.Completed) Action(task.Daily ? T("停止重复", "Stop repeating") : T("完成", "Done"), () => { task.Completed = true; Persist(); RenderTasks(); });
            Action(T("编辑", "Edit"), () => FillEditor(task));
            Action(T("删除", "Delete"), () =>
            {
                if (MessageBox.Show(T("删除这条提醒？", "Delete this reminder?"), T("确认删除", "Confirm deletion"), MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
                tasks.Remove(task); if (editing == task.Id) ResetEditor(); Persist(); RenderTasks();
            });
            panel.Children.Add(actions);
            TaskList.Children.Add(new Border { Child = panel, Background = Brush("#1A2739"), CornerRadius = new CornerRadius(12), Padding = new Thickness(16), Margin = new Thickness(0, 0, 0, 10) });
        }
    }
    private void ToggleStartup(object sender, RoutedEventArgs e)
    {
        try
        {
            if (StartupToggle.IsChecked == true)
            {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
                try
                {
                    dynamic shortcut = shell.CreateShortcut(startupLink);
                    try
                    {
                        shortcut.TargetPath = Environment.ProcessPath;
                        shortcut.Arguments = "--tray"; shortcut.WorkingDirectory = AppContext.BaseDirectory;
                        shortcut.Description = "时砂 · 北京时间与备忘录"; shortcut.Save();
                    }
                    finally { Marshal.FinalReleaseComObject(shortcut); }
                }
                finally { Marshal.FinalReleaseComObject(shell); }
            }
            else if (File.Exists(startupLink)) File.Delete(startupLink);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException)
        {
            StartupToggle.IsChecked = File.Exists(startupLink);
            MessageBox.Show(T("无法更改开机启动设置。请检查启动文件夹权限。", "Cannot change launch-at-sign-in. Check Startup folder permissions."), T("时砂", "时砂"));
        }
    }
}
