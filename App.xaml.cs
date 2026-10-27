using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace ShiSha;
public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(true, @"Local\ShiSha.MY_TIME", out bool first);
        if (!first)
        {
            var language = "zh-CN";
            try { language = new SettingsStore(Path.Combine(AppContext.BaseDirectory, "data", "settings.json")).Load().LanguageCode; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or TimeZoneNotFoundException or InvalidTimeZoneException) { }
            MessageBox.Show(UiText.Get(language, "时砂已在运行，请双击右下角托盘中的时钟图标打开。", "时砂 is already running. Double-click its tray icon to open."), UiText.Get(language, "时砂", "时砂"));
            Shutdown();
            return;
        }
        var window = new MainWindow(Array.IndexOf(e.Args, "--tray") >= 0);
        MainWindow = window;
        if (Array.IndexOf(e.Args, "--tray") < 0) window.Show();
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
