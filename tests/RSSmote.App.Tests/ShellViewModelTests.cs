using RSSmote.App.Controls.Templates;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

public sealed class ShellViewModelTests : IDisposable
{
    private readonly ShellFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public void Unread_filter_lists_only_feeds_with_unread_items()
    {
        var titles = _f.Shell.Sidebar.Rows.Select(r => r.Title).ToList();
        Assert.Equal(["Unread", "Starred", "All Items", "Folders", "Apple", "Feeds", "WIRED"], titles);
        Assert.Equal(4, _f.Row("Unread").Count);
        Assert.Equal(3, _f.Row("Apple").Count);
        Assert.DoesNotContain("Tech", titles);
        Assert.DoesNotContain("Quiet Feed", titles);
    }

    [Fact]
    public void The_sidebar_status_counts_every_feed_and_the_smart_row_knows_its_filter()
    {
        Assert.EndsWith(" · 4 feeds", _f.Shell.Sidebar.StatusText);
        Assert.False(_f.Shell.Sidebar.HasFailures);
        Assert.Equal(StreamFilter.Unread, _f.Row("Unread").Filter);

        _f.Shell.ShowFilterCommand.Execute(StreamFilter.Starred);
        Assert.Equal(StreamFilter.Starred, _f.Row("Starred").Filter);
    }

    /// <summary>A stream row picks all feeds with its filter; a folder keeps the filter, and the filter bar changes it (D10).</summary>
    [Fact]
    public void The_three_stream_rows_pick_all_feeds_with_their_filter_and_a_folder_keeps_it()
    {
        var sidebar = _f.Shell.Sidebar;
        _f.Shell.Settings.HideEmptyFeeds = false;
        sidebar.SelectedRow = _f.Row("Starred");
        _f.Shell.FlushPendingReload();
        Assert.Equal(StreamFilter.Starred, _f.Settings.Current.Filter);
        Assert.Equal("Starred", sidebar.SelectedRow?.Title);
        Assert.Equal(new ArticleStream.All(), sidebar.SelectedStream);
        Assert.Equal(4, _f.Row("Unread").Count);

        sidebar.SelectedRow = _f.Row("Apple");
        _f.Shell.ShowFilterCommand.Execute(StreamFilter.All);
        _f.Shell.FlushPendingReload();
        Assert.Equal("Apple", sidebar.SelectedRow?.Title);
        Assert.Equal(StreamFilter.All, _f.Settings.Current.Filter);
        Assert.Equal(4, _f.Row("Unread").Count);

        sidebar.SelectedRow = _f.Row("Unread");
        _f.Shell.FlushPendingReload();
        Assert.Equal(new ArticleStream.All(), sidebar.SelectedStream);
        Assert.Equal(StreamFilter.Unread, _f.Settings.Current.Filter);
        _f.Shell.ShowFilterCommand.Execute(StreamFilter.Starred);
        _f.Shell.FlushPendingReload();
        Assert.Equal("Starred", sidebar.SelectedRow?.Title);
    }

    [Fact]
    public void The_list_shows_a_count_badge_day_counts_and_a_status_line()
    {
        var list = _f.Shell.List;
        Assert.Equal("4", list.CountText);
        Assert.Equal([3, 1], list.Rows.OfType<DateHeaderViewModel>().Select(h => h.Count));
        Assert.Equal("4 articles · newest first", list.StatusText);

        list.SearchText = "Apple";
        Assert.Matches("^\\d+ match(es)? for “Apple”$", list.StatusText);

        list.EndSearch();
        Assert.Equal("", list.SearchText);
        Assert.Equal("4 articles · newest first", list.StatusText);
    }

    [Fact]
    public void All_filter_lists_every_feed_with_articles_and_folders_open_on_request()
    {
        _f.Shell.ShowFilterCommand.Execute(StreamFilter.All);
        Assert.Contains("Quiet Feed", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.DoesNotContain("Tech", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.DoesNotContain("Daring Fireball", _f.Shell.Sidebar.Rows.Select(r => r.Title));

        _f.Shell.Sidebar.Toggle(_f.Row("Apple"));

        var titles = _f.Shell.Sidebar.Rows.Select(r => r.Title).ToList();
        Assert.Equal(titles.IndexOf("Apple") + 1, titles.IndexOf("AppleInsider News"));
        Assert.Contains(_f.Apple.Id, _f.Settings.Current.ExpandedFolders);
    }

    [Fact]
    public void With_empty_feeds_shown_every_folder_and_feed_is_listed_under_any_filter()
    {
        _f.Store.AddFeed("https://empty.example/rss", "Empty Feed", null, null);
        _f.Shell.Settings.HideEmptyFeeds = false;
        _f.Shell.FlushPendingReload();
        Assert.Equal(["Unread", "Starred", "All Items", "Folders", "Apple", "Tech", "Feeds", "Empty Feed", "Quiet Feed", "WIRED"],
            _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.Equal(0, _f.Row("Quiet Feed").Count);

        _f.Shell.Settings.HideEmptyFeeds = true;
        _f.Shell.ShowFilterCommand.Execute(StreamFilter.All);
        _f.Shell.FlushPendingReload();
        Assert.DoesNotContain("Empty Feed", _f.Shell.Sidebar.Rows.Select(r => r.Title));
    }

    [Fact]
    public void The_list_menu_marks_the_unread_above_or_below_a_row_read_and_keeps_them_listed()
    {
        var rows = _f.Shell.List.Articles.ToList();
        Assert.Equal(4, rows.Count);
        var third = rows[2];
        Assert.Equal(2, _f.Shell.List.UnreadBeside(third, below: false).Count);

        _f.Shell.MarkAboveReadCommand.Execute(third);
        Assert.Equal([false, false, true, true], rows.Select(r => r.IsUnread));
        Assert.Empty(_f.Shell.List.UnreadBeside(third, below: false));
        Assert.Equal(rows, _f.Shell.List.Articles);

        _f.Shell.MarkBelowReadCommand.Execute(rows[0]);
        Assert.All(rows, r => Assert.False(r.IsUnread));
        Assert.Null(_f.Shell.List.SelectedRow);
    }

    [Fact]
    public void The_list_menu_reads_stars_and_opens_a_row_without_selecting_it()
    {
        var row = _f.Shell.List.Articles.ElementAt(1);
        _f.Shell.ToggleRowStarredCommand.Execute(row);
        _f.Shell.ToggleRowReadCommand.Execute(row);
        Assert.Equal((true, false), (row.IsStarred, row.IsUnread));
        _f.Shell.ToggleRowReadCommand.Execute(row);
        Assert.True(row.IsUnread);

        _f.Shell.OpenRowInBrowserCommand.Execute(row);
        Assert.Equal(row.Item.Url, Assert.Single(_f.Launcher.Opened).Url.AbsoluteUri);
        Assert.Null(_f.Shell.Article.Item);
    }

    [Fact]
    public void A_feed_marked_read_leaves_the_sidebar_at_once_and_the_selection_moves_up()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        _f.Shell.MarkAllReadCommand.Execute(null);

        Assert.DoesNotContain("WIRED", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
        Assert.Equal(new ArticleStream.All(), _f.Shell.List.Stream);
    }

    [Fact]
    public void A_folder_marked_read_leaves_the_sidebar_and_a_feed_in_a_folder_hands_over_to_the_folder()
    {
        _f.Settings.Update(s => s.ExpandedFolders = [_f.Apple.Id]);
        _f.Shell.FlushPendingReload();
        _f.Shell.Sidebar.SelectedRow = _f.Row("Daring Fireball");
        _f.Shell.MarkAllReadCommand.Execute(null);
        Assert.DoesNotContain("Daring Fireball", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.Equal(new ArticleStream.InFolder(_f.Apple.Id), _f.Shell.Sidebar.SelectedStream);

        _f.Shell.MarkAllReadCommand.Execute(null);
        Assert.DoesNotContain("Apple", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
    }

    [Fact]
    public void Reading_the_last_article_keeps_the_feed_and_with_empty_feeds_shown_marking_read_keeps_it_too()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        _f.Shell.List.SelectedRow = _f.Shell.List.Articles.Single();
        _f.Shell.FlushPendingReload();
        Assert.Contains("WIRED", _f.Shell.Sidebar.Rows.Select(r => r.Title));

        _f.Shell.Settings.HideEmptyFeeds = false;
        _f.Shell.MarkAllReadCommand.Execute(null);
        Assert.Equal(new ArticleStream.OfFeed(_f.Wired.Id), _f.Shell.Sidebar.SelectedStream);
    }

    [Fact]
    public void A_collapsed_section_hides_its_rows_and_selecting_it_does_not_change_the_stream()
    {
        var section = _f.Row("Feeds");
        _f.Shell.Sidebar.SelectedRow = section;

        Assert.DoesNotContain("WIRED", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
        Assert.Contains("Feeds", _f.Settings.Current.CollapsedSections);
    }

    [Fact]
    public void The_list_groups_by_day_newest_first()
    {
        var rows = _f.Shell.List.Rows;
        Assert.Equal("TODAY", Assert.IsType<DateHeaderViewModel>(rows[0]).Title);
        Assert.Equal(["w1", "i1", "d1", "i2"], _f.Shell.List.Articles.Select(a => a.Item.Guid));
        Assert.Equal("YESTERDAY", rows.OfType<DateHeaderViewModel>().Last().Title);
        Assert.Equal("Unread", _f.Shell.List.Title);
        Assert.Equal("4 Unread Items", _f.Shell.List.Subtitle);
    }

    [Fact]
    public void Selecting_a_feed_lists_its_articles()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        Assert.Equal("WIRED", _f.Shell.List.Title);
        Assert.Equal(["w1"], _f.Shell.List.Articles.Select(a => a.Item.Guid));
        Assert.Equal("No article selected", _f.Shell.Article.EmptyTitle);
        Assert.Equal("1 unread item in WIRED", _f.Shell.Article.EmptySubtitle);

        _f.Shell.List.SelectedRow = _f.Shell.List.Articles.First();
        Assert.Equal("1 of 1", _f.Shell.Article.PositionText);
        Assert.Equal("WIRED", _f.Shell.Article.FeedTitle);
    }

    [Fact]
    public void Selecting_an_article_marks_it_read_but_keeps_it_listed()
    {
        var first = _f.Shell.List.Articles.First();
        _f.Shell.List.SelectedRow = first;

        Assert.False(first.IsUnread);
        Assert.Equal(first.Id, _f.Shell.Article.Item?.Id);
        Assert.False(_f.Shell.Article.IsUnread);
        Assert.Contains(first, _f.Shell.List.Articles);
        Assert.Equal("3 Unread Items", _f.Shell.List.Subtitle);

        _f.Shell.FlushPendingReload();
        Assert.Equal(3, _f.Row("Unread").Count);
        Assert.Same(first, _f.Shell.List.SelectedRow);
    }

    [Fact]
    public void Day_headings_cannot_be_selected()
    {
        var article = _f.Shell.List.Articles.First();
        _f.Shell.List.SelectedRow = article;
        _f.Shell.List.SelectedRow = _f.Shell.List.Rows[0];
        Assert.Same(article, _f.Shell.List.SelectedRow);
    }

    [Fact]
    public void Next_article_walks_the_list_then_moves_to_the_next_feed()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("Apple");
        _f.Shell.NextArticleCommand.Execute(null);
        _f.Shell.NextArticleCommand.Execute(null);
        _f.Shell.NextArticleCommand.Execute(null);
        Assert.Equal("i2", _f.Shell.List.SelectedArticle?.Item.Guid);

        _f.Shell.NextArticleCommand.Execute(null);
        Assert.Equal(new ArticleStream.OfFeed(_f.Wired.Id), _f.Shell.Sidebar.SelectedStream);
    }

    [Fact]
    public void Starring_updates_the_row_and_the_article_in_place()
    {
        var row = _f.Shell.List.Articles.Last();
        _f.Shell.List.SelectedRow = row;
        _f.Shell.ToggleStarredCommand.Execute(null);

        Assert.True(row.IsStarred);
        Assert.True(_f.Shell.Article.IsStarred);
        Assert.Same(row, _f.Shell.List.Articles.Last());
    }

    [Fact]
    public void Changing_the_filter_from_the_bar_persists_and_reloads()
    {
        _f.Shell.List.Filter = StreamFilter.Starred;
        Assert.Equal(StreamFilter.Starred, _f.Settings.Current.Filter);
        Assert.Empty(_f.Shell.List.Articles);
        Assert.Equal("Starred", _f.Row("Starred").Title);
        Assert.Equal(SidebarRowKind.Smart, _f.Row("Starred").Kind);
        Assert.Equal("No Starred Items", _f.Shell.List.Subtitle);
    }

    [Fact]
    public void Open_in_browser_honours_the_background_setting()
    {
        _f.Settings.Update(s => s.OpenLinksInBackground = true);
        _f.Shell.List.SelectedRow = _f.Shell.List.Articles.First();
        _f.Shell.OpenInBrowserCommand.Execute(null);
        var (url, background) = Assert.Single(_f.Launcher.Opened);
        Assert.Equal("https://example.com/w1", url.AbsoluteUri);
        Assert.True(background);
    }

    [Fact]
    public void Go_back_closes_the_article()
    {
        _f.Shell.List.SelectedRow = _f.Shell.List.Articles.First();
        _f.Shell.GoBackCommand.Execute(null);
        Assert.False(_f.Shell.Article.HasArticle);
    }

    [Fact]
    public void The_article_document_survives_flag_changes()
    {
        _f.Shell.List.SelectedRow = _f.Shell.List.Articles.First();
        var document = _f.Shell.Article.Document;
        Assert.NotNull(document);
        Assert.Contains("<p>The codirector talks exclusively about the call.</p>", document.ContentHtml);
        Assert.Equal(["AUTHOR", "WIRED"], document.Byline.Select(b => b.ToUpperInvariant()));

        _f.Shell.ToggleStarredCommand.Execute(null);
        _f.Shell.ToggleReadCommand.Execute(null);
        Assert.Same(document, _f.Shell.Article.Document);

        _f.Shell.NextArticleCommand.Execute(null);
        Assert.NotSame(document, _f.Shell.Article.Document);
    }

    [Fact]
    public void The_size_keys_change_the_article_or_the_pane_chosen_in_the_open_popover()
    {
        _f.Shell.ChangeFontSizeCommand.Execute(2);
        Assert.Equal(120, _f.Settings.Current.ArticleTextSize);
        Assert.Equal(19.2, _f.Shell.Settings.ArticleFontSize, 3);

        _f.Shell.Settings.SelectedPane = TextPane.List;
        _f.Shell.Settings.IsTextSizeOpen = true;
        _f.Shell.ChangeFontSizeCommand.Execute(1);
        Assert.Equal(110, _f.Settings.Current.ListTextSize);
        Assert.Equal(1.1, _f.Shell.Settings.ListScale, 3);

        _f.Shell.Settings.IsTextSizeOpen = false;
        _f.Shell.ChangeFontSizeCommand.Execute(0);
        Assert.Equal(AppSettings.StandardTextSize, _f.Settings.Current.ArticleTextSize);
        Assert.Equal(110, _f.Settings.Current.ListTextSize);
    }

    [Fact]
    public void Links_in_the_article_open_with_the_background_setting()
    {
        _f.Settings.Update(s => s.OpenLinksInBackground = true);
        _f.Shell.OpenLinkCommand.Execute(new Uri("https://example.com/elsewhere"));
        Assert.Equal((new Uri("https://example.com/elsewhere"), true), Assert.Single(_f.Launcher.Opened));
    }

    [Theory]
    [InlineData("j", Avalonia.Input.Key.J)]
    [InlineData(" ", Avalonia.Input.Key.Space)]
    [InlineData(".", Avalonia.Input.Key.OemPeriod)]
    [InlineData("2", Avalonia.Input.Key.D2)]
    [InlineData("=", Avalonia.Input.Key.OemPlus)]
    [InlineData("Escape", Avalonia.Input.Key.Escape)]
    public void Page_keys_map_to_avalonia_keys(string domKey, Avalonia.Input.Key key) =>
        Assert.Equal(key, RSSmote.App.Controls.Headless.ArticleHost.ToKey(domKey));

    [Fact]
    public void Collapsing_the_folder_of_the_selected_feed_selects_the_folder()
    {
        _f.Shell.Sidebar.Toggle(_f.Row("Apple"));
        _f.Shell.Sidebar.SelectedRow = _f.Row("Daring Fireball");

        _f.Shell.Sidebar.Toggle(_f.Row("Apple"));

        Assert.False(_f.Row("Apple").IsExpanded);
        Assert.DoesNotContain("Daring Fireball", _f.Shell.Sidebar.Rows.Select(r => r.Title));
        Assert.DoesNotContain(_f.Apple.Id, _f.Settings.Current.ExpandedFolders);
        Assert.Equal(new ArticleStream.InFolder(_f.Apple.Id), _f.Shell.Sidebar.SelectedStream);
        Assert.Same(_f.Row("Apple"), _f.Shell.Sidebar.SelectedRow);
        Assert.Equal("Apple", _f.Shell.List.Title);
    }

    [Fact]
    public void Collapsing_the_section_of_the_selected_feed_falls_back_to_the_top_stream()
    {
        _f.Shell.Sidebar.SelectedRow = _f.Row("WIRED");
        _f.Shell.Sidebar.SelectedRow = _f.Row("Feeds");

        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
        Assert.Equal(4, _f.Shell.List.Articles.Count());
    }
}
