using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace RSSmote.Mobile.Tests;

/// <summary>Renders a phone root headless at the size of the design canvas (D23) into bin/…/snapshots.</summary>
public static class PhoneRender
{
    public const double Width = 402;
    public const double Height = 874;

    /// <summary>The theme variant for "Sand" (Light) or "Ember" (Dark).</summary>
    public static ThemeVariant Variant(string palette) => palette == "Ember" ? ThemeVariant.Dark : ThemeVariant.Light;

    public static Window Show(Control root, string palette)
    {
        Application.Current!.RequestedThemeVariant = Variant(palette);
        var window = new Window { Width = Width, Height = Height, Content = root };
        window.Show();
        return window;
    }

    public static void Save(Window window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(new PixelSize((int)Width, (int)Height), frame!.PixelSize);
        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
