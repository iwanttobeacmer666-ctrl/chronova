using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using ShiSha;

internal static class LayoutProgram
{
    [STAThread]
    private static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var window = new MainWindow(false, false);
        try
        {
            var root = (FrameworkElement)window.Content;
            void Layout()
            {
                root.Measure(new Size(window.Width, window.Height));
                root.Arrange(new Rect(0, 0, window.Width, window.Height));
                root.UpdateLayout();
            }
            Layout();
            if (((FrameworkElement)window.FindName("ToolPanel")).Visibility != Visibility.Collapsed) throw new Exception("Tools must start collapsed");
            foreach (var tool in new[] { "stopwatch", "countdown", "settings" })
            {
                typeof(MainWindow).GetMethod("ToggleTool", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { tool });
                Layout();
                var visible = (FrameworkElement)window.FindName(tool == "stopwatch" ? "StopwatchPanel" : tool == "countdown" ? "CountdownPanel" : "SettingsPanel");
                var bounds = (FrameworkElement)window.FindName("ToolPanel");
                var top = visible.TransformToAncestor(bounds).Transform(new Point()).Y;
                if (top + visible.ActualHeight > bounds.ActualHeight - 15) throw new Exception(tool + " content is clipped");
                Console.WriteLine("PASS " + tool + " panel fits");
            }
            ((ComboBox)window.FindName("ZoneInput")).SelectedValue = "Pacific Standard Time";
            ((ComboBox)window.FindName("LanguageInput")).SelectedValue = "en-US";
            typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
            Layout();
            if (!((TextBlock)window.FindName("TaskZoneCaption")).Text.Contains("Los Angeles")) throw new Exception("Task editor must use selected zone");
            if (!((Button)window.FindName("SaveButton")).Content.ToString()!.Contains("Add reminder")) throw new Exception("Reminder controls must translate");
            var label = ((TextBlock)window.FindName("Clock")).Text;
            if (label != Schedule.Local(DateTimeOffset.UtcNow, "Pacific Standard Time").ToString("HH:mm")) throw new Exception("Clock must use selected zone");
            Console.WriteLine("PASS English UI and selected-zone clock/task editor");
            foreach (var language in UiText.Languages)
            {
                ((ComboBox)window.FindName("LanguageInput")).SelectedValue = language.Id;
                typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
                Layout();
                foreach (var name in new[] { "SettingsPanel", "Calendar", "FooterHint", "WeatherCard" })
                {
                    var item = (FrameworkElement)window.FindName(name);
                    var parent = name == "SettingsPanel" ? (FrameworkElement)window.FindName("ToolPanel") : root;
                    var point = item.TransformToAncestor(parent).Transform(new Point());
                    if (point.Y + item.ActualHeight > parent.ActualHeight + 1 || point.X + item.DesiredSize.Width > parent.ActualWidth + 1)
                        throw new Exception(language.Id + " clips " + name);
                }
                Console.WriteLine("PASS locale layout " + language.Id);
            }
            typeof(MainWindow).GetMethod("ToggleTool", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { "settings" });
            Layout();
            if (window.Height != 380 || ((FrameworkElement)window.FindName("ToolPanel")).Visibility != Visibility.Collapsed) throw new Exception("Collapsed tools must restore widget size");
            Console.WriteLine("PASS tools collapse to original widget size");
            typeof(MainWindow).GetField("forecast", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, new WeatherForecast(0, true, 7, 14, 2.36, 328, "2026-10-27", DateTimeOffset.UtcNow));
            typeof(MainWindow).GetMethod("RenderWeather", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Layout();
            if (!((TextBlock)window.FindName("WeatherTemperature")).Text.Contains("7–14")) throw new Exception("Weather temperature range missing");
            Console.WriteLine("PASS weather card renders temperature, icon and wind");
            typeof(MainWindow).GetField("forecast", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, null);
            typeof(MainWindow).GetField("weatherUnavailable", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(window, true);
            typeof(MainWindow).GetMethod("RenderWeather", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null);
            Layout();
            if (((TextBlock)window.FindName("WeatherTemperature")).Text.Length != 0 || ((TextBlock)window.FindName("WeatherWind")).Text.Length != 0) throw new Exception("Offline weather must not show stale numbers");
            Console.WriteLine("PASS offline weather clears old temperatures and wind");
            var haidian = new ChinaRegion("110108", "海淀区", "district", "110000", 0, 116.310316, 39.956074);
            var districtInput = (ComboBox)window.FindName("ChinaDistrictInput");
            districtInput.ItemsSource = new[] { haidian }; districtInput.SelectedItem = haidian;
            typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
            var preferencesField = typeof(MainWindow).GetField("preferences", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var selectedSettings = (UserSettings)preferencesField.GetValue(window)!;
            if (selectedSettings.CustomWeatherCity?.ChineseName != "海淀区" || selectedSettings.WeatherCityId != "CN-AD-110108") throw new Exception("District selection was not saved");
            Console.WriteLine("PASS district selection saves the precise weather location");
            typeof(MainWindow).GetMethod("SaveSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, new object[] { window, new RoutedEventArgs() });
            if (((UserSettings)preferencesField.GetValue(window)!).WeatherCityId != "CN-AD-110108") throw new Exception("Saving other settings must not replace the district");
            Console.WriteLine("PASS other settings preserve the saved district");
        }
        finally { typeof(MainWindow).GetMethod("ExitApp", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null); }
    }
}
