using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;
using RSSmote.Desktop.Shell;
using RSSmote.Desktop.Views;
using RSSmote.Desktop.Views.Settings;

namespace RSSmote.Desktop.Tests;

/// <summary>The Settings mode in the real window (D30): how it opens and closes, its keys, and every page rendered.</summary>
public class SettingsWindowTests
{
    private static RawInputModifiers Command => OperatingSystem.IsMacOS() ? RawInputModifiers.Meta : RawInputModifiers.Control;

    private static void Settle()
    {
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static MainWindow Show(ShellFixture f, string palette = "Sand")
    {
        Application.Current!.RequestedThemeVariant = palette == "Ember" ? ThemeVariant.Dark : ThemeVariant.Light;
        var window = new MainWindow(f.Shell) { Width = 1100, Height = 720 };
        window.Show();
        Settle();
        return window;
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    private static Button MenuItem(Window window, string text) =>
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("settingsMenu")
            && b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text));

    private static void Press(Window window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, PhysicalKey.None, null);
        window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle();
    }

    [AvaloniaFact]
    public void The_status_bar_button_opens_settings_in_place_of_the_columns_and_back_returns()
    {
        using var f = new ShellFixture();
        var window = Show(f);
        var sidebar = window.GetVisualDescendants().OfType<SidebarView>().Single();
        var host = window.GetVisualDescendants().OfType<ArticleHost>().Single();

        Click(window, sidebar.SettingsButton);
        Assert.True(f.Shell.IsSettingsOpen);
        var settings = window.GetVisualDescendants().OfType<SettingsView>().Single();
        Assert.True(settings.IsEffectivelyVisible);
        Assert.False(sidebar.IsEffectivelyVisible);
        Assert.True(host.IsObscured);
        Assert.Contains("selected", MenuItem(window, "Articles & Refresh").Classes);

        Click(window, MenuItem(window, "Manage Feeds"));
        Assert.Equal(SettingsSection.ManageFeeds, f.Shell.SettingsMode!.Section);
        Assert.Contains("selected", MenuItem(window, "Manage Feeds").Classes);
        Assert.DoesNotContain("selected", MenuItem(window, "Articles & Refresh").Classes);

        var back = settings.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("settingsBack"));
        Click(window, back);
        Assert.False(f.Shell.IsSettingsOpen);
        Assert.True(sidebar.IsEffectivelyVisible);
        Assert.False(host.IsObscured);
        window.Close();
    }

    [AvaloniaFact]
    public void The_shortcut_opens_settings_where_single_keys_do_nothing_and_escape_leaves()
    {
        using var f = new ShellFixture();
        var window = Show(f);

        Press(window, Key.OemComma, Command);
        Assert.Equal(SettingsSection.Reading, f.Shell.SettingsMode?.Section);

        Press(window, Key.J);
        Assert.Null(f.Shell.List.SelectedRow);
        Press(window, Key.D2, Command);
        Assert.True(f.Shell.IsSettingsOpen);

        Press(window, Key.N, Command);
        Assert.Equal(SettingsSection.AddFeed, f.Shell.SettingsMode?.Section);

        Press(window, Key.Escape);
        Assert.False(f.Shell.IsSettingsOpen);

        Press(window, Key.N, Command);
        Assert.Equal(SettingsSection.AddFeed, f.Shell.SettingsMode?.Section);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_cancels_a_question_before_it_leaves_and_a_click_beside_it_cancels_too()
    {
        using var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        var window = Show(f);
        f.Shell.OpenSettingsCommand.Execute(SettingsSection.ManageFeeds);
        var mode = f.Shell.SettingsMode!;
        mode.EditFolderCommand.Execute(f.Apple);
        Settle();

        Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Delete Folder…"));
        Assert.True(mode.IsConfirming);
        Press(window, Key.Escape);
        Assert.False(mode.IsConfirming);
        Assert.True(f.Shell.IsSettingsOpen);

        mode.RemoveFeedCommand.Execute(f.Daring);
        Settle();
        window.MouseDown(new Point(20, 360), MouseButton.Left);
        window.MouseUp(new Point(20, 360), MouseButton.Left);
        Settle();
        Assert.False(mode.IsConfirming);
        Assert.NotNull(f.Store.Feed(f.Daring.Id));

        Press(window, Key.Escape);
        Assert.False(f.Shell.IsSettingsOpen);
        window.Close();
    }

    [AvaloniaFact]
    public void The_file_menu_and_the_application_menu_open_settings()
    {
        using var f = new ShellFixture();
        var window = new MainWindow(f.Shell);
        List<NativeMenuItem> Items(NativeMenu? menu) =>
            menu?.Items.OfType<NativeMenuItem>().SelectMany(i => Items(i.Menu).Prepend(i)).ToList() ?? [];
        var items = Items(NativeMenu.GetMenu(window));
        void Click(string header)
        {
            var item = items.Single(i => i.Header == header);
            item.Command!.Execute(item.CommandParameter);
        }

        Click("Import Feeds…");
        Assert.Equal(SettingsSection.ImportFeeds, f.Shell.SettingsMode?.Section);
        Click("Export Feeds…");
        Assert.Equal(SettingsSection.ExportFeeds, f.Shell.SettingsMode?.Section);
        Click("Add Feed…");
        Assert.Equal(SettingsSection.AddFeed, f.Shell.SettingsMode?.Section);
        f.Shell.CloseSettings();

        MainWindow.AddSettingsItem(Application.Current!, f.Shell);
        MainWindow.AddSettingsItem(Application.Current!, f.Shell);
        var settings = Assert.Single(Items(NativeMenu.GetMenu(Application.Current!)), i => i.Header == MainWindow.SettingsItemHeader);
        Assert.Equal(new KeyGesture(Key.OemComma, KeyModifiers.Meta), settings.Gesture);
        settings.Command!.Execute(null);
        Assert.Equal(SettingsSection.Reading, f.Shell.SettingsMode?.Section);
    }

    [AvaloniaFact]
    public async Task Adding_a_feed_keeps_settings_open_with_the_address_ready_for_the_next()
    {
        using var f = new ShellFixture();
        f.Http.Pages["https://blog.example/feed.xml"] =
            "<rss version=\"2.0\"><channel><title>Example Blog</title><item><title>a</title><guid>a</guid></item></channel></rss>";
        var window = Show(f);
        Press(window, Key.N, Command);
        var add = (AddFeedViewModel)f.Shell.SettingsMode!.Page!;
        add.Address = "https://blog.example/feed.xml";
        await add.SearchCommand.ExecuteAsync(null);
        Settle();

        Click(window, window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == "Add Feed"));
        Assert.True(f.Shell.IsSettingsOpen);
        Assert.NotSame(add, f.Shell.SettingsMode!.Page);
        var focused = Assert.IsType<TextBox>(window.FocusManager?.GetFocusedElement());
        Assert.Equal("AddressBox", focused.Name);
        Assert.Equal("", focused.Text);
        window.Close();
    }

    /// <summary>Every page of the design canvas, in Sand and Ember, into bin/…/snapshots (D19).</summary>
    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public async Task Every_settings_page_renders(string palette)
    {
        using var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        f.Http.Pages["https://blog.example/feed.xml"] =
            "<rss version=\"2.0\"><channel><title>Example Blog</title><item><title>a</title><guid>a</guid></item></channel></rss>";
        f.Files.NextOpen = """
            <opml version="2.0"><body>
              <outline text="Science"><outline text="A" xmlUrl="https://a.example/rss" /><outline text="B" xmlUrl="https://b.example/rss" /></outline>
              <outline text="WIRED" xmlUrl="https://wired.example/rss" />
            </body></opml>
            """;
        var window = Show(f, palette);
        f.Shell.OpenSettingsCommand.Execute(null);
        var mode = f.Shell.SettingsMode!;
        Settle();
        ShellRenderTests.Save(window, $"settings-reading-{palette}");

        mode.Show(SettingsSection.Appearance);
        Settle();
        ShellRenderTests.Save(window, $"settings-appearance-{palette}");

        mode.Show(SettingsSection.ManageFeeds);
        Settle();
        ShellRenderTests.Save(window, $"settings-manage-{palette}");
        var quiet = ((ManageFeedsViewModel)mode.Page!).Groups.SelectMany(g => g.Feeds).Single(r => r.Feed.Id == f.Daring.Id);
        mode.EditFeedCommand.Execute(quiet);
        Settle();
        Assert.True(window.GetVisualDescendants().OfType<EditFeedView>().Single().IsEffectivelyVisible);
        ShellRenderTests.Save(window, $"settings-edit-feed-{palette}");
        mode.RemoveFeedCommand.Execute(f.Daring);
        Settle();
        ShellRenderTests.Save(window, $"settings-confirm-{palette}");
        mode.Confirm!.CloseCommand.Execute(null);
        mode.Show(SettingsSection.ManageFeeds);
        mode.EditFolderCommand.Execute(f.Apple);
        Settle();
        ShellRenderTests.Save(window, $"settings-edit-folder-{palette}");

        mode.Show(SettingsSection.AddFeed);
        var add = (AddFeedViewModel)mode.Page!;
        add.Address = "https://blog.example/feed.xml";
        await add.SearchCommand.ExecuteAsync(null);
        Settle();
        ShellRenderTests.Save(window, $"settings-add-feed-{palette}");

        mode.Show(SettingsSection.ImportFeeds);
        await ((ImportFeedsViewModel)mode.Page!).ChooseCommand.ExecuteAsync(null);
        Settle();
        ShellRenderTests.Save(window, $"settings-import-{palette}");

        mode.Show(SettingsSection.ExportFeeds);
        Settle();
        ShellRenderTests.Save(window, $"settings-export-{palette}");
        window.Close();
    }
}
