using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Microsoft.Data.Sqlite;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Data;
using RSSmote.Mobile.Pages;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Tests;

/// <summary>
/// The README's phone screenshots at the iPhone size of the design canvas (D23), over the same made-up library as the
/// desktop ones. Runs only when RSSMOTE_README_DIR names an output folder.
/// </summary>
public class ReadmeCaptureTests
{
    [AvaloniaFact]
    public async Task Capture_readme_phone_screens()
    {
        var output = Environment.GetEnvironmentVariable("RSSMOTE_README_DIR");
        if (string.IsNullOrEmpty(output))
            return;
        Directory.CreateDirectory(output);

        var folder = Path.Combine(Path.GetTempPath(), "rssmote-readme-phone-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var store = new Store(Path.Combine(folder, "rssmote.sqlite"), TimeProvider.System);
            var (_, folders) = DemoLibrary.Fill(store, DateTimeOffset.Now);
            var settings = new SettingsStore(Path.Combine(folder, "settings.json"));
            settings.Update(s =>
            {
                s.LastRefresh = DateTimeOffset.Now.AddMinutes(-4);
                s.ExpandedFolders = [folders[0].Id];
                s.GrayscaleFavicons = false;
            });
            using var http = FeedFinder.CreateHttpClient();
            using var vm = new ShellViewModel(store, settings, refresher: null, new FeedFinder(http), new ShellFixture.FakeFiles(),
                new DemoImages(), new ShellFixture.RecordingLauncher(), new AvaloniaUiDispatcher(), TimeProvider.System,
                SidebarLayout.Phone);
            var shell = new MobileShell(vm) { PageTransition = null, ModalTransition = null };
            var window = PhoneRender.Show(shell, "Sand");
            // The demo favicons arrive at once, before the first rows they belong to exist; a reload takes them from the cache.
            vm.Sidebar.Reload();

            await Shot(window, output, "phone-feeds");

            vm.Sidebar.SelectedRow = vm.Sidebar.Rows.First(r => r.Title == "Unread");
            await Shot(window, output, "phone-list");

            vm.List.SelectedRow = vm.List.Articles.First(a => a.Item.Guid == DemoLibrary.FeatureGuid);
            Assert.IsType<ArticlePage>(shell.NavigationStack[^1]);
            await Shot(window, output, "phone-article");

            await shell.PopToRootAsync();
            shell.Root.Show(RootTab.Settings);
            await Shot(window, output, "phone-settings");

            window.Close();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>Lets images arrive and layout settle, then saves the frame.</summary>
    private static async Task Shot(Avalonia.Controls.Window window, string folder, string name)
    {
        for (var i = 0; i < 12; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(15);
        }
        PhoneRender.Save(window, name);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "snapshots", name + ".png"), Path.Combine(folder, name + ".png"), overwrite: true);
    }
}
