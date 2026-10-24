using RSSmote.App.Controls.Templates;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

/// <summary>The phone's Feeds root (D24), built by the shared sidebar view-model in its phone layout.</summary>
public sealed class SidebarPhoneTests : IDisposable
{
    private readonly ShellFixture _f = new(s => s.ExpandedFolders = [1]);

    public SidebarPhoneTests() => _f.Shell.Sidebar.Layout = SidebarLayout.Phone;

    public void Dispose() => _f.Dispose();

    [Fact]
    public void Three_streams_then_the_folders_and_feeds_with_unread_articles()
    {
        Assert.Equal(["Unread", "Starred", "All Items", "Folders", "Apple", "AppleInsider News", "Daring Fireball",
            "Feeds", "WIRED"], _f.Shell.Sidebar.Rows.Select(r => r.Title));
    }

    [Fact]
    public void With_empty_feeds_shown_every_folder_and_feed_is_listed_with_unread_counts()
    {
        _f.Shell.Settings.HideEmptyFeeds = false;
        var rows = _f.Shell.Sidebar.Rows;
        Assert.Equal(["Unread", "Starred", "All Items", "Folders", "Apple", "AppleInsider News", "Daring Fireball", "Tech",
            "Feeds", "Quiet Feed", "WIRED"], rows.Select(r => r.Title));
        Assert.All(rows.Take(3), r => Assert.Equal(SidebarRowKind.Smart, r.Kind));
        Assert.Equal(4, _f.Row("Unread").Count);
        Assert.Equal(0, _f.Row("Starred").Count);
        Assert.False(_f.Row("All Items").HasCount);
        Assert.Equal(0, _f.Row("Quiet Feed").Count);
        Assert.True(_f.Row("AppleInsider News").IsInFolder);
        Assert.Null(_f.Shell.Sidebar.SelectedRow);
    }

    [Fact]
    public void The_phone_builds_the_list_only_while_it_is_shown()
    {
        using var f = new ShellFixture(layout: SidebarLayout.Phone);
        Assert.Empty(f.Shell.List.Rows);

        f.Shell.Sidebar.SelectedRow = f.Row("WIRED");
        Assert.Single(f.Shell.List.Articles);

        f.Shell.List.Hide();
        f.Store.Upsert(f.Wired.Id, [new("w2", "Later", "https://example.com/w2", null, "<p>x</p>", "x", null, ShellFixture.Now)]);
        f.Shell.FlushPendingReload();
        Assert.Single(f.Shell.List.Articles);

        f.Shell.Sidebar.SelectedRow = f.Row("WIRED");
        Assert.Equal(2, f.Shell.List.Articles.Count());
    }

    [Fact]
    public void Picking_a_stream_that_changes_the_filter_builds_the_list_once()
    {
        var ui = new ShellFixture.QueuedUi();
        using var f = new ShellFixture(s => s.Filter = StreamFilter.All, SidebarLayout.Phone, ui);
        var builds = 0;
        f.Shell.List.Rows.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
                builds++;
        };

        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        ui.RunAll();
        Assert.Equal(StreamFilter.Unread, f.Settings.Current.Filter);
        Assert.Equal(4, f.Shell.List.Articles.Count());
        Assert.Equal(1, builds);
    }

    [Fact]
    public void The_list_line_counts_unread_or_starred_and_names_the_order()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("Unread");
        Assert.Equal("4 unread · newest first", _f.Shell.List.PhoneSubtitle);

        _f.Shell.Settings.SetSortNewestFirstCommand.Execute(false);
        _f.Shell.Sidebar.SelectedRow = _f.Row("Starred");
        Assert.Equal("0 starred · oldest first", _f.Shell.List.PhoneSubtitle);
    }

    [Fact]
    public void Counts_and_hiding_stay_unread_whatever_the_filter()
    {
        _f.Shell.ShowFilterCommand.Execute(StreamFilter.All);
        Assert.Equal(3, _f.Row("Apple").Count);
        Assert.DoesNotContain(_f.Shell.Sidebar.Rows, r => r.Title == "Quiet Feed");
    }

    [Fact]
    public void A_stream_row_opens_the_list_under_its_filter_and_can_be_picked_again()
    {
        var picked = new List<ArticleStream>();
        _f.Shell.Sidebar.StreamSelected += (_, s) => picked.Add(s);

        _f.Shell.Sidebar.SelectedRow = _f.Row("All Items");
        Assert.Equal(StreamFilter.All, _f.Settings.Current.Filter);
        Assert.Equal("All Items", _f.Shell.List.Title);

        Assert.Null(_f.Shell.Sidebar.SelectedRow);
        _f.Shell.Sidebar.SelectedRow = _f.Row("All Items");
        Assert.Equal(2, picked.Count);
        Assert.All(picked, s => Assert.IsType<ArticleStream.All>(s));
    }

    [Fact]
    public void A_feed_row_opens_its_feed_without_changing_the_filter()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        Assert.Equal(new ArticleStream.OfFeed(_f.Wired.Id), _f.Shell.List.Stream);
        Assert.Equal(StreamFilter.Unread, _f.Settings.Current.Filter);
    }
}
