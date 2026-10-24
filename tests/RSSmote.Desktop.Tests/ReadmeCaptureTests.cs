using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using RSSmote.App.Services;
using RSSmote.App.Theming;
using RSSmote.App.ViewModels;
using RSSmote.Desktop.Views;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Data;

namespace RSSmote.Desktop.Tests;

/// <summary>
/// The README's screenshots, over a made-up library: invented feeds and articles, favicons and thumbnails drawn here,
/// so the pictures show no one's subscriptions and no third-party images. Runs only when RSSMOTE_README_DIR names an
/// output folder.
/// </summary>
public class ReadmeCaptureTests
{
    private const int Width = 1280;
    private const int Height = 800;

    [AvaloniaFact]
    public async Task Capture_readme_screens()
    {
        var output = Environment.GetEnvironmentVariable("RSSMOTE_README_DIR");
        if (string.IsNullOrEmpty(output))
            return;
        Directory.CreateDirectory(output);

        var folder = Path.Combine(Path.GetTempPath(), "rssmote-readme-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var store = new Store(Path.Combine(folder, "rssmote.sqlite"), TimeProvider.System);
            var (nightSky, folders) = DemoLibrary.Fill(store, DateTimeOffset.Now);
            var settings = new SettingsStore(Path.Combine(folder, "settings.json"));
            settings.Update(s =>
            {
                s.Filter = StreamFilter.Unread;
                s.LastRefresh = DateTimeOffset.Now.AddMinutes(-4);
                s.ExpandedFolders = [folders[0].Id, folders[1].Id];
                s.GrayscaleFavicons = false;
            });
            using var http = FeedFinder.CreateHttpClient();
            using var shell = new ShellViewModel(store, settings, refresher: null, new FeedFinder(http), new ShellFixture.FakeFiles(),
                new DemoImages(), new ShellFixture.RecordingLauncher(), new AvaloniaUiDispatcher(), TimeProvider.System);

            var window = new Window { Width = Width, Height = Height, Content = new ShellView(), DataContext = shell };
            window.Show();

            void Use(string id) => Application.Current!.RequestedThemeVariant = ThemeCatalogue.All.Single(t => t.Id == id).Variant;
            void Read() => shell.List.SelectedRow = shell.List.Articles.First(a => a.Item.Guid == DemoLibrary.FeatureGuid);

            foreach (var theme in ThemeCatalogue.All)
            {
                Use(theme.Id);
                Read();
                await Shot(window, output, "theme-" + theme.Id);
            }

            Use("sand");
            Read();
            await Shot(window, output, "reading-light");
            Use("ember");
            await Shot(window, output, "reading-dark");

            Use("sand");
            shell.GoBackCommand.Execute(null);
            shell.List.SearchText = "garden";
            await Shot(window, output, "filter-box");
            shell.List.EndSearch();

            Use("fjord");
            await Shot(window, output, "no-article");

            Use("sand");
            shell.OpenSettingsCommand.Execute(SettingsSection.ManageFeeds);
            var managed = ((ManageFeedsViewModel)shell.SettingsMode!.Page!).Groups.SelectMany(g => g.Feeds).First(r => r.Feed.Id == nightSky.Id);
            shell.SettingsMode.EditFeedCommand.Execute(managed);
            await Shot(window, output, "edit-feed");
            shell.CloseSettings();

            Use("ember");
            shell.OpenSettingsCommand.Execute(SettingsSection.AddFeed);
            ((AddFeedViewModel)shell.SettingsMode!.Page!).Address = "https://example.org/blog";
            await Shot(window, output, "add-feed");
            shell.CloseSettings();

            window.Close();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Lets images arrive and layout settle, then saves the frame.</summary>
    private static async Task Shot(Window window, string folder, string name)
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(15);
        }
        using var frame = window.CaptureRenderedFrame();
        frame?.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
