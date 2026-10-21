using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using RSSmote.App.Gallery;
using RSSmote.App.Theming;

namespace RSSmote.App.Tests;

public class GalleryRenderTests
{
    public static TheoryData<string> Themes => new(ThemeCatalogue.All.Select(t => t.Name));

    /// <summary>Renders the component gallery in every palette into bin/…/snapshots for visual review.</summary>
    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Gallery_renders_in_every_theme(string theme)
    {
        Application.Current!.RequestedThemeVariant = ThemeCatalogue.All.Single(t => t.Name == theme).Variant;

        var window = new Window { Width = 1100, Height = 1900, Content = new ComponentGallery() };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, $"gallery-{theme}.png"), PngBitmapEncoderOptions.Default);
        window.Close();
    }
}
