using System;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ShiSha;

public partial class MainWindow
{
    private bool backgroundReady;
    private readonly DispatcherTimer backgroundSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private static string BackgroundDirectory => Path.Combine(AppContext.BaseDirectory, "data");
    private static bool ManagedBackground(string name) => name is "background-0.png" or "background-1.png";

    private void InitializeBackground()
    {
        preferences.BackgroundFile ??= "";
        preferences.BackgroundBrightness = double.IsFinite(preferences.BackgroundBrightness)
            ? Math.Clamp(preferences.BackgroundBrightness, 0.15, 1) : 0.55;
        BackgroundBrightnessInput.Value = preferences.BackgroundBrightness;
        backgroundSaveTimer.Tick += (_, _) => SaveBackgroundBrightness();
        if (preferences.BackgroundFile.Length > 0)
        {
            try
            {
                if (!ManagedBackground(preferences.BackgroundFile)) throw new InvalidDataException();
                ApplyBackground(ReadBackgroundImage(Path.Combine(BackgroundDirectory, preferences.BackgroundFile)));
            }
            catch (Exception ex) when (BackgroundError(ex))
            {
                ApplyBackground(null);
                BackgroundMessage.Text = T("背景图片暂时打不开，请重新选择。", "Cannot open the background. Please choose another image.");
            }
        }
        else ApplyBackground(null);
        ChooseBackgroundButton.IsEnabled = ResetBackgroundButton.IsEnabled = settingsHealthy;
        backgroundReady = true;
        Closing += (_, _) => { if (backgroundSaveTimer.IsEnabled) SaveBackgroundBrightness(); };
    }

    private static bool BackgroundError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException
        or NotSupportedException or ArgumentException or InvalidOperationException or FileFormatException or System.Runtime.InteropServices.COMException;

    private static BitmapSource ReadBackgroundImage(string path)
    {
        using var input = File.OpenRead(path);
        if (input.Length > 25 * 1024 * 1024) throw new InvalidDataException("Image exceeds 25 MB");
        Span<byte> header = stackalloc byte[8];
        if (input.Read(header) != 8) throw new InvalidDataException("Incomplete image");
        bool png = header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!png && !jpeg) throw new InvalidDataException("Use JPG or PNG");
        input.Position = 0;
        // Inspect dimensions before requesting pixels, then decode only a widget-sized copy.
        var decoder = BitmapDecoder.Create(input, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        var frame = decoder.Frames[0];
        int width = frame.PixelWidth, height = frame.PixelHeight;
        if (width <= 0 || height <= 0 || width > 40000 || height > 40000 || (long)width * height > 100_000_000)
            throw new InvalidDataException("Image dimensions are too large");
        input.Position = 0;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        if (width >= height) image.DecodePixelWidth = Math.Min(width, 1600);
        else image.DecodePixelHeight = Math.Min(height, 1600);
        image.StreamSource = input;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void ApplyBackground(BitmapSource? image)
    {
        BackgroundLayer.Background = image == null ? null : new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        BackgroundShade.Opacity = image == null ? 0 : 1 - BackgroundBrightnessInput.Value;
        ClockSurface.Opacity = image == null ? 1 : 0.65;
        ToolPanel.Background = Brush(image == null ? "#1C293C" : "#D91C293C");
        MemoPanel.Background = image == null ? Brushes.Transparent : Brush("#B3101827");
        HeaderPanel.Background = FooterPanel.Background = image == null ? Brushes.Transparent : Brush("#B3101827");
        BackgroundBrightnessInput.IsEnabled = image != null && settingsHealthy;
    }

    private void ChooseBackground(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = T("选择背景图", "Choose image"), Filter = "JPG / PNG|*.jpg;*.jpeg;*.png",
            CheckFileExists = true, Multiselect = false
        };
        if (dialog.ShowDialog(this) != true) return;
        try { SetBackgroundImage(dialog.FileName); }
        catch (Exception ex) when (BackgroundError(ex))
        { BackgroundMessage.Text = T("图片未更换。请选择有效的 JPG 或 PNG（不超过25MB），并检查目录写入权限。", "Image unchanged. Choose a valid JPG or PNG (up to 25 MB) and check folder write access."); }
    }

    private void SetBackgroundImage(string source)
    {
        if (!settingsHealthy) return;
        var image = ReadBackgroundImage(source);
        // Two bounded slots protect the active image if importing or saving settings fails.
        string file = preferences.BackgroundFile == "background-0.png" ? "background-1.png" : "background-0.png";
        Directory.CreateDirectory(BackgroundDirectory);
        string path = Path.Combine(BackgroundDirectory, file);
        using (var output = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            encoder.Save(output);
        }
        if (!PersistBackground(file, BackgroundBrightnessInput.Value)) return;
        ApplyBackground(image);
        BackgroundMessage.Text = "";
    }

    private bool PersistBackground(string file, double brightness)
    {
        string oldFile = preferences.BackgroundFile;
        double oldBrightness = preferences.BackgroundBrightness;
        preferences.BackgroundFile = file;
        preferences.BackgroundBrightness = brightness;
        try { settingsStore.Save(preferences); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            preferences.BackgroundFile = oldFile;
            preferences.BackgroundBrightness = oldBrightness;
            BackgroundMessage.Text = T("背景设置未保存，请检查目录写入权限。", "Background settings were not saved. Check folder write access.");
            return false;
        }
    }

    private void ResetBackground(object sender, RoutedEventArgs e)
    {
        if (!settingsHealthy) return;
        backgroundSaveTimer.Stop();
        if (!PersistBackground("", 0.55)) return;
        backgroundReady = false;
        BackgroundBrightnessInput.Value = 0.55;
        backgroundReady = true;
        ApplyBackground(null);
        BackgroundMessage.Text = "";
    }

    private void ChangeBackgroundBrightness(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!backgroundReady || !settingsHealthy || BackgroundLayer.Background == null) return;
        BackgroundShade.Opacity = 1 - e.NewValue;
        backgroundSaveTimer.Stop(); backgroundSaveTimer.Start();
    }

    private void SaveBackgroundBrightness()
    {
        backgroundSaveTimer.Stop();
        if (!settingsHealthy || BackgroundLayer.Background == null) return;
        if (PersistBackground(preferences.BackgroundFile, BackgroundBrightnessInput.Value)) BackgroundMessage.Text = "";
    }
}
