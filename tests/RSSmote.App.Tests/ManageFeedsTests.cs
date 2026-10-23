using RSSmote.App.ViewModels;

namespace RSSmote.App.Tests;

/// <summary>Manage Feeds and the pages it opens on the phone (D27), over the view-models the desktop panels share.</summary>
public sealed class ManageFeedsTests : IDisposable
{
    private readonly ShellFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public void One_group_per_folder_then_the_feeds_in_no_folder()
    {
        using var manage = _f.Shell.CreateManageFeeds();
        Assert.Equal(["Apple", "Tech", "No folder"], manage.Groups.Select(g => g.Title));
        Assert.Equal(["AppleInsider News", "Daring Fireball"], manage.Groups[0].Feeds.Select(r => r.Title));
        Assert.False(manage.Groups[1].HasFeeds);
        Assert.False(manage.Groups[2].IsFolder);
        Assert.Equal("insider.example", manage.Groups[0].Feeds[0].Subtitle);
    }

    [Fact]
    public void A_failed_feed_shows_its_error_and_the_filter_narrows_the_groups()
    {
        _f.Store.UpdateFeed(_f.Wired with { LastError = "No longer returns a feed" });
        using var manage = _f.Shell.CreateManageFeeds();
        var wired = manage.Groups[^1].Feeds.Single(r => r.Title == "WIRED");
        Assert.True(wired.HasError);
        Assert.Equal("No longer returns a feed", wired.Subtitle);

        manage.FilterText = "daring";
        Assert.Equal(["Apple"], manage.Groups.Select(g => g.Title));
        manage.FilterText = "quiet.example";
        Assert.Equal(["No folder"], manage.Groups.Select(g => g.Title));
    }

    [Fact]
    public void The_groups_follow_the_library()
    {
        using var manage = _f.Shell.CreateManageFeeds();
        var folder = _f.Shell.CreateNewFolder();
        folder.Name = " Science ";
        folder.SaveCommand.Execute(null);
        Assert.Equal(["Apple", "Science", "Tech", "No folder"], manage.Groups.Select(g => g.Title));

        _f.Shell.ConfirmRemoveFeed(_f.Wired).ConfirmCommand.Execute(null);
        Assert.DoesNotContain(manage.Groups.SelectMany(g => g.Feeds), r => r.Title == "WIRED");
    }

    [Fact]
    public void New_articles_leave_the_groups_alone()
    {
        using var manage = _f.Shell.CreateManageFeeds();
        var rebuilt = 0;
        manage.Groups.CollectionChanged += (_, _) => rebuilt++;
        _f.Store.Upsert(_f.Wired.Id, [new("w9", "Later", "https://example.com/w9", null, "<p>x</p>", "x", null, ShellFixture.Now)]);
        Assert.Equal(0, rebuilt);

        _f.Store.UpdateFeed(_f.Wired with { LastError = "Gone" });
        Assert.NotEqual(0, rebuilt);
    }

    [Fact]
    public void A_new_folder_named_like_an_existing_one_adds_nothing()
    {
        var folder = _f.Shell.CreateNewFolder();
        Assert.False(folder.SaveCommand.CanExecute(null));
        folder.Name = "Apple";
        folder.SaveCommand.Execute(null);
        Assert.Equal(2, _f.Store.Folders().Count);
    }

    [Fact]
    public void The_folder_picker_can_choose_none()
    {
        var edit = _f.Shell.CreateEditFeed(_f.Daring)!;
        Assert.Equal("Apple", edit.Picker.Selected?.Title);
        edit.Picker.SelectNoneCommand.Execute(null);
        edit.SaveCommand.Execute(null);
        Assert.Null(_f.Store.Feed(_f.Daring.Id)!.FolderId);
        Assert.Equal("Open df.example", edit.OpenSiteTitle);
    }
}
