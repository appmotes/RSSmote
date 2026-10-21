using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using RSSmote.App.Views;

namespace RSSmote.App.Tests;

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

    [AvaloniaFact]
    public async Task Add_feed_panel_renders_its_pages()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        using var f = new ShellFixture();
        f.Http.Pages["https://blog.example/feed.xml"] =
            "<rss version=\"2.0\"><channel><title>Example Blog</title><item><title>a</title><guid>a</guid></item></channel></rss>";
        f.Shell.ShowAddFeedCommand.Execute(null);
        var window = new Window { Width = 1045, Height = 660, Content = new ShellView(), DataContext = f.Shell };
        window.Show();
        Save(window, "add-feed-search");

        var panel = Assert.IsType<RSSmote.App.ViewModels.AddFeedViewModel>(f.Shell.Panel);
        panel.Address = "https://blog.example/feed.xml";
        await panel.SearchCommand.ExecuteAsync(null);
        Save(window, "add-feed-results");
        window.Close();
    }

    [AvaloniaFact]
    public void Edit_and_confirm_panels_render()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        using var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        var window = new Window { Width = 1045, Height = 660, Content = new ShellView(), DataContext = f.Shell };
        window.Show();

        f.Shell.EditFeedCommand.Execute(f.Daring);
        Save(window, "edit-feed");
        f.Shell.EditFolderCommand.Execute(f.Apple.Id);
        Save(window, "edit-folder");
        f.Shell.RemoveFeedCommand.Execute(f.Daring);
        Save(window, "confirm-remove");
        window.Close();
    }

    private static void Save(Window window, string name)
    {
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var folder = Path.Combine(AppContext.BaseDirectory, "snapshots");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
