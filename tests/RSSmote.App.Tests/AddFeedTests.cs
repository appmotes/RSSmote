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

    /// <summary>Add Feed as the desktop shows it, in Settings (D30).</summary>
    private AddFeedViewModel Open()
    {
        _f.Shell.OpenSettingsCommand.Execute(SettingsSection.AddFeed);
        return Assert.IsType<AddFeedViewModel>(_f.Shell.SettingsMode!.Page);
    }

    [Fact]
    public async Task A_site_address_finds_its_feed_and_saving_starts_add_feed_again()
    {
        var panel = Open();
        panel.Address = "blog.example";
        await panel.SearchCommand.ExecuteAsync(null);

        Assert.True(panel.HasResults);
        var row = Assert.Single(panel.Results);
        Assert.Equal("Example Blog", row.Title);
        Assert.True(row.IsOn);

        panel.SaveCommand.Execute(null);
        var next = Assert.IsType<AddFeedViewModel>(_f.Shell.SettingsMode?.Page);
        Assert.NotSame(panel, next);
        Assert.Equal(SettingsSection.AddFeed, _f.Shell.SettingsMode!.Section);
        Assert.Equal(("", false), (next.Address, next.HasResults));
        Assert.Equal("Added “Example Blog”. Paste the next address to add another.", next.Hint);
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
        Assert.Equal(new ArticleStream.All(), _f.Shell.Sidebar.SelectedStream);
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
        Assert.StartsWith("Already added", row.Detail);
        Assert.False(panel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_search_shows_no_results_and_the_reason_under_the_address()
    {
        var panel = Open();
        Assert.Equal(AddFeedViewModel.Prompt, panel.Hint);
        panel.Address = "https://nothing.example/";
        await panel.SearchCommand.ExecuteAsync(null);

        Assert.False(panel.HasResults);
        Assert.True(panel.HasError);
        Assert.Equal(panel.Error, panel.Hint);
    }

    [Fact]
    public async Task A_new_folder_receives_the_feed_and_opens()
    {
        var panel = Open();
        panel.Address = FeedUrl;
        await panel.SearchCommand.ExecuteAsync(null);
        panel.Picker.NewFolderName = "Blogs";
        panel.Picker.AddFolderCommand.Execute(null);

        Assert.Equal("Blogs", panel.Picker.Selected?.Title);
        Assert.True(panel.Picker.Folders.Single(f => f.Folder.Title == "Blogs").IsOn);
        panel.SaveCommand.Execute(null);

        var folder = _f.Store.FolderNamed("Blogs")!;
        Assert.Equal(folder.Id, _f.Store.FeedWithUrl(FeedUrl)?.FolderId);
        Assert.Equal(folder.Id, Assert.IsType<AddFeedViewModel>(_f.Shell.SettingsMode?.Page).Picker.Selected?.Id);
        Assert.Contains(folder.Id, _f.Settings.Current.ExpandedFolders);
    }

    [Fact]
    public void Leaving_settings_discards_the_page()
    {
        var panel = Open();
        panel.Address = "blog.example";
        _f.Shell.CloseSettings();
        Assert.False(_f.Shell.IsSettingsOpen);
        Assert.Null(_f.Shell.SettingsMode);
        Assert.DoesNotContain(_f.Store.Feeds(), f => f.Url == FeedUrl);
    }
}
