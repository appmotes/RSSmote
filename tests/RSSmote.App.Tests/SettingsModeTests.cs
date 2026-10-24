using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

/// <summary>The desktop Settings mode (D30): the menu, the pages stacked under Manage Feeds, and the questions.</summary>
public sealed class SettingsModeTests : IDisposable
{
    private readonly ShellFixture _f = new(s => s.ExpandedFolders = [1]);

    public void Dispose() => _f.Dispose();

    private SettingsModeViewModel Open(SettingsSection? section = null)
    {
        _f.Shell.OpenSettingsCommand.Execute(section);
        return Assert.IsType<SettingsModeViewModel>(_f.Shell.SettingsMode);
    }

    private T Page<T>() => Assert.IsType<T>(_f.Shell.SettingsMode!.Page);

    private ManagedFeedViewModel Managed(SettingsModeViewModel mode, Feed feed) =>
        Assert.IsType<ManageFeedsViewModel>(mode.Page).Groups.SelectMany(g => g.Feeds).Single(r => r.Feed.Id == feed.Id);

    [Fact]
    public void Settings_open_on_articles_and_refresh_and_close_back_to_the_feeds()
    {
        Assert.False(_f.Shell.IsSettingsOpen);
        var mode = Open();
        Assert.True(_f.Shell.IsSettingsOpen);
        Assert.Equal(SettingsSection.Reading, mode.Section);
        Assert.Same(_f.Shell.Settings, Page<ReadingPageViewModel>().Settings);
        Assert.Equal("Articles & Refresh", mode.Title);
        Assert.False(mode.HasAction);

        mode.CloseCommand.Execute(null);
        Assert.False(_f.Shell.IsSettingsOpen);
        Assert.Null(_f.Shell.SettingsMode);
    }

    [Fact]
    public void Each_menu_item_shows_its_page_and_opening_again_moves_there()
    {
        var mode = Open(SettingsSection.ImportFeeds);
        Assert.IsType<ImportFeedsViewModel>(mode.Page);

        _f.Shell.OpenSettingsCommand.Execute(SettingsSection.ExportFeeds);
        Assert.Same(mode, _f.Shell.SettingsMode);
        Assert.Equal(("Export Feeds", SettingsSection.ExportFeeds), (mode.Title, mode.Section));

        mode.Show(SettingsSection.Appearance);
        Assert.IsType<AppearancePageViewModel>(mode.Page);
        mode.Show(SettingsSection.AddFeed);
        Assert.IsType<AddFeedViewModel>(mode.Page);
        mode.Show(SettingsSection.ManageFeeds);
        Assert.Equal(("Manage Feeds", "New Folder"), (mode.Title, mode.ActionTitle));
        Assert.Same(mode.NewFolderCommand, mode.ActionCommand);
    }

    [Fact]
    public void Manage_feeds_stops_following_the_library_once_settings_close()
    {
        var manage = Page<ManageFeedsViewModel>(Open(SettingsSection.ManageFeeds));
        _f.Shell.CloseSettings();

        _f.Store.AddFeed("https://late.example/rss", "Late", null, null);
        Assert.DoesNotContain(manage.Groups.SelectMany(g => g.Feeds), r => r.Title == "Late");
    }

    [Fact]
    public void Editing_a_feed_keeps_manage_feeds_chosen_and_save_returns_to_it()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        mode.EditFeedCommand.Execute(Managed(mode, _f.Wired));
        var edit = Page<EditFeedViewModel>();
        Assert.Equal(SettingsSection.ManageFeeds, mode.Section);
        Assert.Equal(("WIRED", "Save"), (mode.Title, mode.ActionTitle));
        Assert.Same(edit.SaveCommand, mode.ActionCommand);

        edit.Name = "Wired Magazine";
        edit.Picker.ToggleCommand.Execute(edit.Picker.Folders.Single(f => f.Folder.Title == "Apple"));
        mode.ActionCommand!.Execute(null);
        _f.Shell.FlushPendingReload();

        var manage = Page<ManageFeedsViewModel>();
        Assert.Contains(manage.Groups.Single(g => g.Title == "Apple").Feeds, r => r.Title == "Wired Magazine");
        var saved = _f.Store.Feed(_f.Wired.Id)!;
        Assert.Equal(("Wired Magazine", (long?)_f.Apple.Id), (saved.Title, saved.FolderId));
        Assert.Contains("Wired Magazine", _f.Shell.Sidebar.Rows.Select(r => r.Title));
    }

    [Fact]
    public void Manage_feeds_in_the_menu_returns_from_an_edit_page_and_discards_it()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        var manage = mode.Page;
        mode.EditFeedCommand.Execute(Managed(mode, _f.Wired));
        Page<EditFeedViewModel>().Name = "Something else";

        mode.Show(SettingsSection.ManageFeeds);
        Assert.Same(manage, mode.Page);
        Assert.Equal("WIRED", _f.Store.Feed(_f.Wired.Id)!.Title);
    }

    [Fact]
    public void Back_leaves_an_edit_page_without_saving_for_the_page_below()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        Assert.False(mode.CanGoBack);
        mode.EditFolderCommand.Execute(_f.Apple);
        var folder = Page<EditFolderViewModel>();
        folder.EditFeedCommand.Execute(folder.Feeds[1]);
        Page<EditFeedViewModel>().Name = "Something else";
        Assert.True(mode.CanGoBack);

        mode.BackCommand.Execute(null);
        Assert.Same(folder, mode.Page);
        Assert.Equal("Daring Fireball", _f.Store.Feed(_f.Daring.Id)!.Title);
        mode.BackCommand.Execute(null);
        Assert.IsType<ManageFeedsViewModel>(mode.Page);
        Assert.False(mode.CanGoBack);
        mode.BackCommand.Execute(null);
        Assert.IsType<ManageFeedsViewModel>(mode.Page);
    }

    [Fact]
    public void Removing_a_feed_asks_first_then_deletes_it_with_every_article_and_returns()
    {
        var starred = _f.Store.Items(new ArticleStream.OfFeed(_f.Wired.Id), StreamFilter.All).Single();
        _f.Store.SetStarred([starred.Id], true);
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        var mode = Open(SettingsSection.ManageFeeds);
        mode.EditFeedCommand.Execute(Managed(mode, _f.Wired));

        mode.RemoveFeedCommand.Execute(_f.Wired);
        var confirm = Assert.IsType<ConfirmViewModel>(mode.Confirm);
        Assert.True(mode.IsConfirming);
        Assert.Equal("Remove “WIRED”?", confirm.Title);
        Assert.NotNull(_f.Store.Feed(_f.Wired.Id));

        confirm.ConfirmCommand.Execute(null);
        _f.Shell.FlushPendingReload();
        Assert.False(mode.IsConfirming);
        Assert.IsType<ManageFeedsViewModel>(mode.Page);
        Assert.Null(_f.Store.Feed(_f.Wired.Id));
        Assert.Null(_f.Store.Item(starred.Id));
        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
    }

    [Fact]
    public void Cancelling_the_question_keeps_the_feed_and_its_page()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        mode.EditFeedCommand.Execute(Managed(mode, _f.Wired));
        mode.RemoveFeedCommand.Execute(_f.Wired);
        mode.Confirm!.CloseCommand.Execute(null);

        Assert.Null(mode.Confirm);
        Assert.IsType<EditFeedViewModel>(mode.Page);
        Assert.NotNull(_f.Store.Feed(_f.Wired.Id));
    }

    [Fact]
    public void Editing_a_folder_renames_it_and_its_feeds_open_their_own_page()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        mode.EditFolderCommand.Execute(_f.Apple);
        var folder = Page<EditFolderViewModel>();
        Assert.Equal(("Apple", "2 Feeds"), (mode.Title, folder.Subtitle));
        Assert.Equal(["AppleInsider News", "Daring Fireball"], folder.Feeds.Select(f => f.Title));

        folder.EditFeedCommand.Execute(folder.Feeds[1]);
        Assert.Equal("Daring Fireball", Page<EditFeedViewModel>().Title);
        Page<EditFeedViewModel>().SaveCommand.Execute(null);
        Assert.Same(folder, mode.Page);

        folder.Name = "Cupertino";
        mode.ActionCommand!.Execute(null);
        Assert.IsType<ManageFeedsViewModel>(mode.Page);
        Assert.Equal("Cupertino", _f.Store.Folders().Single(f => f.Id == _f.Apple.Id).Title);
    }

    [Fact]
    public void Deleting_a_folder_asks_first_and_keeps_its_feeds_outside_any_folder()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        mode.EditFolderCommand.Execute(_f.Apple);
        Page<EditFolderViewModel>().DeleteCommand.Execute(null);
        var confirm = Assert.IsType<ConfirmViewModel>(mode.Confirm);
        Assert.Equal("Delete “Apple”?", confirm.Title);

        confirm.ConfirmCommand.Execute(null);
        _f.Shell.FlushPendingReload();

        Assert.IsType<ManageFeedsViewModel>(mode.Page);
        Assert.DoesNotContain(_f.Store.Folders(), f => f.Id == _f.Apple.Id);
        Assert.Null(_f.Store.Feed(_f.Insider.Id)!.FolderId);
        Assert.Null(_f.Store.Feed(_f.Daring.Id)!.FolderId);
        Assert.Contains("Daring Fireball", _f.Shell.Sidebar.Rows.Select(r => r.Title));
    }

    [Fact]
    public void New_folder_from_the_bar_adds_a_group_to_manage_feeds()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        mode.ActionCommand!.Execute(null);
        var add = Page<NewFolderViewModel>();
        Assert.Equal("New Folder", mode.Title);
        Assert.False(mode.ActionCommand!.CanExecute(null));

        add.Name = "Science";
        mode.ActionCommand.Execute(null);
        Assert.Contains(Page<ManageFeedsViewModel>().Groups, g => g.Title == "Science");
    }

    [Fact]
    public void Open_site_and_reload_icon_reach_their_services()
    {
        var mode = Open(SettingsSection.ManageFeeds);
        mode.OpenSiteCommand.Execute(_f.Wired);
        Assert.Equal("https://wired.example/", Assert.Single(_f.Launcher.Opened).Url.AbsoluteUri);

        mode.ReloadIconCommand.Execute(_f.Wired);
        Assert.Contains(_f.Images.Forgotten, url => url.Contains("wired.example", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_import_ends_on_manage_feeds_with_what_arrived()
    {
        _f.Files.NextOpen = """
            <opml version="2.0"><body>
              <outline text="News"><outline text="A" xmlUrl="https://a.example/rss" /></outline>
            </body></opml>
            """;
        var mode = Open(SettingsSection.ImportFeeds);
        var import = Page<ImportFeedsViewModel>();
        await import.ChooseCommand.ExecuteAsync(null);
        import.ImportCommand.Execute(null);

        Assert.Equal(SettingsSection.ManageFeeds, mode.Section);
        Assert.Contains(Page<ManageFeedsViewModel>().Groups, g => g.Title == "News");
    }

    [Fact]
    public void Disposing_the_shell_disposes_open_settings()
    {
        var manage = Page<ManageFeedsViewModel>(Open(SettingsSection.ManageFeeds));
        _f.Shell.Dispose();
        _f.Store.AddFeed("https://late.example/rss", "Late", null, null);
        Assert.DoesNotContain(manage.Groups.SelectMany(g => g.Feeds), r => r.Title == "Late");
    }

    private static T Page<T>(SettingsModeViewModel mode) => Assert.IsType<T>(mode.Page);
}
