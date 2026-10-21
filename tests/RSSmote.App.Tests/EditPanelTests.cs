using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Templates;
using RSSmote.App.ViewModels;
using RSSmote.App.Views;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

public sealed class EditPanelTests : IDisposable
{
    private readonly ShellFixture _f = new(s => s.ExpandedFolders = [1]);

    public void Dispose() => _f.Dispose();

    private T Panel<T>() where T : PanelViewModel => Assert.IsType<T>(_f.Shell.Panel);

    [Fact]
    public void Editing_a_feed_renames_it_and_moves_it_to_another_folder()
    {
        _f.Shell.EditFeedCommand.Execute(_f.Wired);
        var panel = Panel<EditFeedViewModel>();
        Assert.Equal("wired.example/rss", panel.Subtitle);
        Assert.Null(panel.Picker.Selected);

        panel.Name = "Wired Magazine";
        panel.Picker.ToggleCommand.Execute(panel.Picker.Folders.Single(f => f.Folder.Title == "Apple"));
        panel.SaveCommand.Execute(null);
        _f.Shell.FlushPendingReload();

        Assert.Null(_f.Shell.Panel);
        var saved = _f.Store.Feed(_f.Wired.Id)!;
        Assert.Equal("Wired Magazine", saved.Title);
        Assert.Equal(_f.Apple.Id, saved.FolderId);
        Assert.Contains("Wired Magazine", _f.Shell.Sidebar.Rows.Select(r => r.Title));
    }

    [Fact]
    public void Closing_the_edit_panel_discards_the_changes()
    {
        _f.Shell.EditFeedCommand.Execute(_f.Wired);
        var panel = Panel<EditFeedViewModel>();
        panel.Name = "Something else";
        panel.CloseCommand.Execute(null);

        Assert.Null(_f.Shell.Panel);
        Assert.Equal("WIRED", _f.Store.Feed(_f.Wired.Id)!.Title);
    }

    [Fact]
    public void Removing_a_feed_asks_first_and_then_deletes_it_with_every_article()
    {
        var starred = _f.Store.Items(new ArticleStream.OfFeed(_f.Wired.Id), StreamFilter.All).Single();
        _f.Store.SetStarred([starred.Id], true);

        _f.Shell.RemoveFeedCommand.Execute(_f.Wired);
        var confirm = Panel<ConfirmViewModel>();
        Assert.Equal("Remove “WIRED”?", confirm.Title);
        Assert.NotNull(_f.Store.Feed(_f.Wired.Id));

        confirm.ConfirmCommand.Execute(null);
        Assert.Null(_f.Shell.Panel);
        Assert.Null(_f.Store.Feed(_f.Wired.Id));
        Assert.Null(_f.Store.Item(starred.Id));
    }

    [Fact]
    public void Cancelling_the_removal_keeps_the_feed()
    {
        _f.Shell.RemoveFeedCommand.Execute(_f.Wired);
        Panel<ConfirmViewModel>().CloseCommand.Execute(null);
        Assert.NotNull(_f.Store.Feed(_f.Wired.Id));
    }

    [Fact]
    public void Removing_the_selected_feed_moves_the_selection_away()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        _f.Shell.RemoveFeedCommand.Execute(_f.Wired);
        Panel<ConfirmViewModel>().ConfirmCommand.Execute(null);
        _f.Shell.FlushPendingReload();

        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
        Assert.DoesNotContain(_f.Shell.List.Articles, a => a.Item.FeedId == _f.Wired.Id);
    }

    [Fact]
    public void Editing_a_folder_renames_it_and_lists_its_feeds()
    {
        _f.Shell.EditFolderCommand.Execute(_f.Apple.Id);
        var panel = Panel<EditFolderViewModel>();
        Assert.Equal("2 Feeds", panel.Subtitle);
        Assert.Equal(["AppleInsider News", "Daring Fireball"], panel.Feeds.Select(f => f.Title));

        panel.Name = "Cupertino";
        panel.SaveCommand.Execute(null);
        Assert.Equal("Cupertino", _f.Store.Folders().Single(f => f.Id == _f.Apple.Id).Title);
    }

    [Fact]
    public void A_feed_in_the_folder_panel_opens_its_own_panel()
    {
        _f.Shell.EditFolderCommand.Execute(_f.Apple.Id);
        var folder = Panel<EditFolderViewModel>();
        folder.EditFeedCommand.Execute(folder.Feeds[1]);
        Assert.Equal("Daring Fireball", Panel<EditFeedViewModel>().Title);
    }

    [Fact]
    public void Deleting_a_folder_asks_first_and_keeps_its_feeds_outside_any_folder()
    {
        _f.Shell.EditFolderCommand.Execute(_f.Apple.Id);
        Panel<EditFolderViewModel>().DeleteCommand.Execute(null);
        var confirm = Panel<ConfirmViewModel>();
        Assert.Equal("Delete “Apple”?", confirm.Title);

        confirm.ConfirmCommand.Execute(null);
        _f.Shell.FlushPendingReload();

        Assert.DoesNotContain(_f.Store.Folders(), f => f.Id == _f.Apple.Id);
        Assert.Null(_f.Store.Feed(_f.Insider.Id)!.FolderId);
        Assert.Null(_f.Store.Feed(_f.Daring.Id)!.FolderId);
        Assert.Contains("Daring Fireball", _f.Shell.Sidebar.Rows.Select(r => r.Title));
    }

    [Fact]
    public void Open_site_and_reload_icon_reach_their_services()
    {
        _f.Shell.OpenSiteCommand.Execute(_f.Wired);
        Assert.Equal("https://wired.example/", Assert.Single(_f.Launcher.Opened).Url.AbsoluteUri);

        _f.Shell.ReloadIconCommand.Execute(_f.Wired);
        Assert.Contains(_f.Images.Forgotten, url => url.Contains("wired.example", StringComparison.Ordinal));
    }

    [Fact]
    public void Feed_and_folder_rows_have_menus_and_other_rows_none()
    {
        Assert.Equal(["Open wired.example", "Reload Icon", "Edit…", "Remove Feed…"],
            SidebarView.MenuItems(_f.Shell, _f.Row("WIRED")).OfType<MenuItem>().Select(m => (string)m.Header!));
        Assert.Equal(["Edit…"], SidebarView.MenuItems(_f.Shell, _f.Row("Apple")).OfType<MenuItem>().Select(m => (string)m.Header!));
        Assert.Empty(SidebarView.MenuItems(_f.Shell, _f.Row("Unread")));
    }
}

public class SidebarMenuRenderTests
{
    [AvaloniaFact]
    public void A_right_click_opens_the_menu_without_selecting_the_row()
    {
        using var f = new ShellFixture();
        var view = new ShellView();
        var window = new Window { Width = 1045, Height = 660, Content = view, DataContext = f.Shell };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var sidebar = view.GetVisualDescendants().OfType<SidebarView>().Single();
        var row = view.GetVisualDescendants().OfType<SidebarRow>().Single(r => r.Title == "WIRED");
        var point = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.Equal(new ArticleStream.All(), f.Shell.Sidebar.SelectedStream);
        Assert.NotNull(sidebar.OpenMenu);
        Assert.True(sidebar.OpenMenu!.IsOpen);
        sidebar.OpenMenu.Close();
        window.Close();
    }
}
