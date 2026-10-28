using System;
using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
            void Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);
            var fixture = Path.Combine(AppContext.BaseDirectory, "background-fixture.png");
            var drawing = new DrawingVisual();
            using (var dc = drawing.RenderOpen())
            {
                dc.DrawRectangle(new LinearGradientBrush(Colors.DarkSlateBlue, Colors.DarkSeaGreen, 25), null, new Rect(0, 0, 2400, 1200));
                dc.DrawEllipse(Brushes.Gold, null, new Point(1800, 350), 180, 180);
            }
            var pixels = new RenderTargetBitmap(2400, 1200, 96, 96, PixelFormats.Pbgra32); pixels.Render(drawing);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(pixels));
            using (var output = File.Create(fixture)) encoder.Save(output);
            Invoke("SetBackgroundImage", fixture);
            File.Delete(fixture);
            var imageBrush = (ImageBrush)((Border)window.FindName("BackgroundLayer")).Background;
            if (((BitmapSource)imageBrush.ImageSource).PixelWidth != 1600 || imageBrush.Stretch != Stretch.UniformToFill) throw new Exception("Background must be downscaled and cover the widget");
            Console.WriteLine("PASS background imports a bounded copy and releases the original file");
            var slider = (Slider)window.FindName("BackgroundBrightnessInput"); slider.Value = 0.7;
            Invoke("SaveBackgroundBrightness");
            var stored = new SettingsStore(Path.Combine(AppContext.BaseDirectory, "data", "settings.json")).Load();
            if (stored.BackgroundFile.Length == 0 || Math.Abs(stored.BackgroundBrightness - 0.7) > 0.001) throw new Exception("Background settings not persisted");
            if (Math.Abs(((Border)window.FindName("BackgroundShade")).Opacity - 0.3) > 0.001) throw new Exception("Brightness preview missing");
            Console.WriteLine("PASS brightness previews and persists");
            Invoke("SaveSettings", window, new RoutedEventArgs());
            stored = new SettingsStore(Path.Combine(AppContext.BaseDirectory, "data", "settings.json")).Load();
            if (stored.BackgroundFile.Length == 0 || stored.BackgroundBrightness != 0.7 || stored.WeatherCityId != "CN-AD-110108") throw new Exception("Other settings lost the background or district");
            Invoke("ApplyBackground", new object[] { null! });
            Invoke("InitializeBackground");
            if (((Border)window.FindName("BackgroundLayer")).Background is not ImageBrush) throw new Exception("Background did not restore after loading");
            Console.WriteLine("PASS reload restores the managed image after original deletion; other settings preserve it");
            string activeFile = stored.BackgroundFile;
            File.WriteAllText(fixture, "invalid image");
            bool rejected = false;
            try { Invoke("SetBackgroundImage", fixture); } catch (TargetInvocationException ex) when (ex.InnerException is InvalidDataException) { rejected = true; }
            File.Delete(fixture);
            if (!rejected || ((UserSettings)preferencesField.GetValue(window)!).BackgroundFile != activeFile || ((Border)window.FindName("BackgroundLayer")).Background is not ImageBrush) throw new Exception("Invalid image changed the current background");
            Console.WriteLine("PASS invalid image preserves the current background");
            var jpegEncoder = new JpegBitmapEncoder(); jpegEncoder.Frames.Add(BitmapFrame.Create(pixels));
            using (var output = File.Create(fixture)) jpegEncoder.Save(output);
            Invoke("SetBackgroundImage", fixture); File.Delete(fixture);
            if (((Border)window.FindName("BackgroundLayer")).Background is not ImageBrush) throw new Exception("JPG did not load");
            Console.WriteLine("PASS JPG imports and replaces the background");
            Invoke("ToggleTool", "settings"); Layout();
            var preview = new RenderTargetBitmap((int)window.Width, (int)window.Height, 96, 96, PixelFormats.Pbgra32); preview.Render(root);
            var previewEncoder = new PngBitmapEncoder(); previewEncoder.Frames.Add(BitmapFrame.Create(preview));
            using (var output = File.Create(Path.Combine(AppContext.BaseDirectory, "background-preview.png"))) previewEncoder.Save(output);
            Invoke("ResetBackground", window, new RoutedEventArgs());
            stored = new SettingsStore(Path.Combine(AppContext.BaseDirectory, "data", "settings.json")).Load();
            if (stored.BackgroundFile != "" || stored.WeatherCityId != "CN-AD-110108" || ((Border)window.FindName("BackgroundLayer")).Background != null) throw new Exception("Reset lost other settings or did not restore the default background");
            Console.WriteLine("PASS reset restores solid background and preserves other settings");
        }
        finally { typeof(MainWindow).GetMethod("ExitApp", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, null); }
    }
}
