using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using RSSmote.App.Services;
using RSSmote.App.Theming;
using RSSmote.App.ViewModels;
using RSSmote.App.Views;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.Tests;

/// <summary>
/// Screenshots of every screen for design work, over a copy of a real library with real favicons and thumbnails.
/// Runs only when RSSMOTE_CAPTURE_DIR names an output folder and RSSMOTE_CAPTURE_DB a library copy (it is changed).
/// </summary>
public class DesignCaptureTests
{
    private const int Width = 1280;
    private const int Height = 800;

    [AvaloniaFact]
    public async Task Capture_every_screen()
    {
        var output = Environment.GetEnvironmentVariable("RSSMOTE_CAPTURE_DIR");
        var library = Environment.GetEnvironmentVariable("RSSMOTE_CAPTURE_DB");
        if (string.IsNullOrEmpty(output) || string.IsNullOrEmpty(library))
            return;
        Directory.CreateDirectory(output);

        using var http = FeedFinder.CreateHttpClient();
        using var store = new Store(library, TimeProvider.System);
        var settings = new SettingsStore(Path.Combine(Path.GetTempPath(), "rssmote-capture-" + Guid.NewGuid().ToString("N") + ".json"));
        var folders = store.Folders();
        settings.Update(s =>
        {
            s.Filter = StreamFilter.Unread;
            s.LastRefresh = DateTimeOffset.Now.AddMinutes(-4);
            s.ExpandedFolders = [.. folders.Take(2).Select(f => f.Id)];
        });
        using var shell = new ShellViewModel(store, settings, refresher: null, new FeedFinder(http), new ShellFixture.FakeFiles(),
            new ImageCache(http), new ShellFixture.RecordingLauncher(), new AvaloniaUiDispatcher(), TimeProvider.System);

        var window = new Window { Width = Width, Height = Height, Content = new ShellView(), DataContext = shell };
        window.Show();

        var feedInFolder = store.Feeds().First(f => f.FolderId is not null);
        var folder = folders.First(f => f.Id == feedInFolder.FolderId);

        foreach (var theme in new[] { "light", "dark", "black" })
        {
            Application.Current!.RequestedThemeVariant = theme switch
            {
                "light" => ThemeVariant.Light,
                "dark" => ThemeVariant.Dark,
                _ => ThemeCatalogue.Black,
            };
            var full = theme != "black";

            Reset(shell, settings, StreamFilter.Unread);
            SelectArticleWithImage(shell);
            await Shot(window, output, theme, "01-reading-unread", settle: 40);

            shell.GoBackCommand.Execute(null);
            await Shot(window, output, theme, "02-list-no-article");

            Reset(shell, settings, StreamFilter.All);
            shell.Sidebar.Select(new ArticleStream.InFolder(folder.Id));
            SelectArticleWithImage(shell);
            await Shot(window, output, theme, "03-folder-all-items", settle: 30);
            if (!full)
                continue;

            Reset(shell, settings, StreamFilter.Starred);
            await Shot(window, output, theme, "04-starred-empty");

            Reset(shell, settings, StreamFilter.Unread);
            shell.List.SearchText = "apple";
            await Shot(window, output, theme, "05-list-filter-box");
            shell.List.EndSearch();

            shell.ShowAddFeedCommand.Execute(null);
            await Shot(window, output, theme, "06-add-feed");
            var add = (AddFeedViewModel)shell.Panel!;
            add.Address = "https://www.theverge.com";
            await add.SearchCommand.ExecuteAsync(null);
            await Shot(window, output, theme, "07-add-feed-results");
            if (add.Page == AddFeedPage.Results)
            {
                add.ShowFoldersCommand.Execute(null);
                await Shot(window, output, theme, "08-add-feed-folder");
            }
            add.CloseCommand.Execute(null);

            shell.EditFeedCommand.Execute(feedInFolder);
            await Shot(window, output, theme, "09-edit-feed");
            shell.EditFolderCommand.Execute(folder.Id);
            await Shot(window, output, theme, "10-edit-folder");
            shell.RemoveFeedCommand.Execute(feedInFolder);
            await Shot(window, output, theme, "11-confirm-remove-feed");
            shell.Panel?.CloseCommand.Execute(null);
        }
        window.Close();
    }

    private static void Reset(ShellViewModel shell, SettingsStore settings, StreamFilter filter)
    {
        shell.Panel?.CloseCommand.Execute(null);
        settings.Update(s => s.Filter = filter);
        shell.FlushPendingReload();
        shell.Sidebar.Select(new ArticleStream.All());
    }

    /// <summary>An article with a JPEG or PNG lead image; the headless article renderer cannot draw every format.</summary>
    private static void SelectArticleWithImage(ShellViewModel shell) =>
        shell.List.SelectedRow = shell.List.Articles.FirstOrDefault(a => a.Item.ImageUrl is { } url
                                     && (url.Contains(".jpg", StringComparison.OrdinalIgnoreCase) || url.Contains(".png", StringComparison.OrdinalIgnoreCase)))
                                 ?? shell.List.Articles.FirstOrDefault();

    /// <summary>Lets images arrive and layout settle, then saves the frame.</summary>
    private static async Task Shot(Window window, string folder, string theme, string name, int settle = 8)
    {
        for (var i = 0; i < settle; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(100);
        }
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame!.Save(Path.Combine(folder, $"{name}-{theme}.png"), PngBitmapEncoderOptions.Default);
    }
}
