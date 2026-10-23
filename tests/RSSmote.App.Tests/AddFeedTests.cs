using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

public sealed class AddFeedTests : IDisposable
{
    private const string FeedUrl = "https://blog.example/feed.xml";

    private readonly ShellFixture _f = new();

    public AddFeedTests()
    {
        _f.Http.Pages[FeedUrl] = """
            <rss version="2.0"><channel><title>Example Blog</title><link>https://blog.example/</link>
            <item><title>Hello</title><guid>h1</guid><link>https://blog.example/hello</link>
            <pubDate>Mon, 19 Oct 2026 10:00:00 GMT</pubDate><description>First post</description></item>
            </channel></rss>
            """;
        _f.Http.Pages["https://blog.example/"] =
            $"""<html><head><link rel="alternate" type="application/rss+xml" href="{FeedUrl}"></head></html>""";
    }

    public void Dispose() => _f.Dispose();

    private AddFeedViewModel Open()
    {
        _f.Shell.ShowAddFeedCommand.Execute(null);
        return Assert.IsType<AddFeedViewModel>(_f.Shell.Panel);
    }

    [Fact]
    public async Task A_site_address_finds_its_feed_and_saving_selects_it()
    {
        var panel = Open();
        panel.Address = "blog.example";
        await panel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(AddFeedPage.Results, panel.Page);
        var row = Assert.Single(panel.Results);
        Assert.Equal("Example Blog", row.Title);
        Assert.True(row.IsOn);

        panel.SaveCommand.Execute(null);
        Assert.Null(_f.Shell.Panel);
        var feed = Assert.Single(_f.Store.Feeds(), f => f.Url == FeedUrl);
        Assert.Equal(new ArticleStream.OfFeed(feed.Id), _f.Shell.Sidebar.SelectedStream);
        Assert.Equal(["Hello"], _f.Shell.List.Articles.Select(a => a.Item.Title));
    }

    [Fact]
    public async Task The_phone_page_saves_its_one_pick_without_selecting_a_stream()
    {
        var page = _f.Shell.CreateAddFeed();
        page.Address = "blog.example";
        await page.SearchCommand.ExecuteAsync(null);
        Assert.True(page.HasResults);
        Assert.Equal("Found 1 feed", page.FoundText);
        Assert.Equal("1 article · blog.example/feed.xml", page.Results[0].Detail);

        page.PickResultCommand.Execute(page.Results[0]);
        page.SaveCommand.Execute(null);
        Assert.Single(_f.Store.Feeds(), f => f.Url == FeedUrl);
        Assert.Null(_f.Shell.Panel);
    }

    [Fact]
    public async Task A_feed_already_stored_cannot_be_picked()
    {
        _f.Store.AddFeed(FeedUrl, "Example Blog", null, null);
        var panel = Open();
        panel.Address = FeedUrl;
        await panel.SearchCommand.ExecuteAsync(null);

        var row = Assert.Single(panel.Results);
        Assert.True(row.IsSaved);
        Assert.False(row.IsOn);
        Assert.StartsWith("Already added", row.Subtitle);
        Assert.False(panel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_search_stays_on_the_search_page_with_the_reason()
    {
        var panel = Open();
        panel.Address = "https://nothing.example/";
        await panel.SearchCommand.ExecuteAsync(null);

        Assert.Equal(AddFeedPage.Search, panel.Page);
        Assert.True(panel.HasError);
        Assert.Equal(panel.Error, panel.SearchTitle);
    }

    [Fact]
    public async Task A_new_folder_receives_the_feed_and_opens()
    {
        var panel = Open();
        panel.Address = FeedUrl;
        await panel.SearchCommand.ExecuteAsync(null);
        panel.ShowFoldersCommand.Execute(null);
        panel.Picker.NewFolderName = "Blogs";
        panel.Picker.AddFolderCommand.Execute(null);

        Assert.Equal("Folder: Blogs", panel.FolderTitle);
        Assert.True(panel.Picker.Folders.Single(f => f.Folder.Title == "Blogs").IsOn);
        panel.BackCommand.Execute(null);
        Assert.Equal(AddFeedPage.Results, panel.Page);
        panel.SaveCommand.Execute(null);

        var folder = _f.Store.FolderNamed("Blogs")!;
        Assert.Equal(folder.Id, _f.Store.FeedWithUrl(FeedUrl)?.FolderId);
        Assert.Contains(folder.Id, _f.Settings.Current.ExpandedFolders);
    }

    [Fact]
    public async Task Opml_import_adds_feeds_and_reports_the_count()
    {
        _f.Files.NextOpen = """
            <opml version="2.0"><body>
              <outline text="News"><outline text="A" xmlUrl="https://a.example/rss" /><outline text="B" xmlUrl="https://b.example/rss" /></outline>
              <outline text="WIRED" xmlUrl="https://wired.example/rss" />
            </body></opml>
            """;
        var panel = Open();
        await panel.ImportOpmlCommand.ExecuteAsync(null);

        Assert.Equal("Imported 2 feeds", panel.ImportTitle);
        Assert.NotNull(_f.Store.FeedWithUrl("https://a.example/rss"));
        Assert.NotNull(_f.Store.FolderNamed("News"));
    }

    [Fact]
    public async Task Cancelling_the_file_dialog_changes_nothing()
    {
        var panel = Open();
        await panel.ImportOpmlCommand.ExecuteAsync(null);
        Assert.Equal("Import from OPML…", panel.ImportTitle);
    }

    [Fact]
    public async Task Export_writes_every_feed()
    {
        await _f.Shell.ExportOpmlCommand.ExecuteAsync(null);
        Assert.Contains("xmlUrl=\"https://wired.example/rss\"", _f.Files.Saved);
        Assert.Contains("text=\"Apple\"", _f.Files.Saved);
    }

    [Fact]
    public void Closing_the_panel_clears_it()
    {
        var panel = Open();
        panel.CloseCommand.Execute(null);
        Assert.Null(_f.Shell.Panel);
        Assert.False(_f.Shell.IsPanelOpen);
    }
}
