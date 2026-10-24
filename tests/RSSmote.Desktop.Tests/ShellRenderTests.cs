using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using RSSmote.Desktop.Views;

namespace RSSmote.Desktop.Tests;

public class ShellRenderTests
{
    /// <summary>Renders the real shell over a seeded library into bin/…/snapshots for visual review (D19).</summary>
    [AvaloniaTheory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void Shell_renders_with_an_article_open(string theme)
    {
        Application.Current!.RequestedThemeVariant = theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        using var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        f.Shell.List.SelectedRow = f.Shell.List.Articles.Skip(1).First();

        var window = new Window { Width = 1045, Height = 660, Content = new ShellView(), DataContext = f.Shell };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);

        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, $"shell-{theme}.png"), PngBitmapEncoderOptions.Default);
        window.Close();
    }

    /// <summary>The window carries the app icon (D13), which Windows and Linux show in the title bar and taskbar.</summary>
    [AvaloniaFact]
    public void The_window_has_the_app_icon()
    {
        using var f = new ShellFixture();
        var window = new RSSmote.Desktop.Shell.MainWindow(f.Shell);
        Assert.NotNull(window.Icon);
    }

    /// <summary>Renders the window into bin/…/snapshots/<paramref name="name"/>.png.</summary>
    internal static void Save(Window window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
