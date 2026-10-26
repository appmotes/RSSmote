using RSSmote.App.ViewModels;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;

namespace RSSmote.App.Tests;

/// <summary>Import with a preview and Export (D27, W2), and the phone's other reading settings.</summary>
public sealed class ImportExportTests : IDisposable
{
    private const string Opml = """
        <opml version="2.0"><body>
          <outline text="Apple">
            <outline text="Daring Fireball" type="rss" xmlUrl="https://df.example/rss" />
            <outline text="Six Colors" type="rss" xmlUrl="https://six.example/rss" />
          </outline>
          <outline text="Science">
            <outline text="Field Notes" type="rss" xmlUrl="https://field.example/rss" />
            <outline text="Night Sky" type="rss" xmlUrl="https://sky.example/rss" />
          </outline>
          <outline text="Loose" type="rss" xmlUrl="https://loose.example/rss" />
        </body></opml>
        """;

    private readonly ShellFixture _f = new();

    public void Dispose() => _f.Dispose();

    [Fact]
    public async Task A_file_shows_what_it_adds_and_saves_nothing_until_import()
    {
        _f.Files.NextOpen = Opml;
        _f.Files.NextName = "my-feeds.opml";
        var import = _f.Shell.CreateImportFeeds();
        await import.ChooseCommand.ExecuteAsync(null);

        Assert.Equal("my-feeds.opml", import.FileName);
        Assert.Equal("5 feeds in 2 folders", import.FileSummary);
        Assert.Equal([("Apple", "1 feed"), ("Science", "2 feeds"), ("No folder", "1 feed")],
            import.Groups.Select(g => (g.Title, g.CountText)));
        Assert.Equal("1 feed is already added and is skipped. Folders that exist are reused.", import.SkippedText);
        Assert.Equal("Import 4 Feeds", import.ImportTitle);
        Assert.Equal(4, _f.Store.Feeds().Count);

        import.ImportCommand.Execute(null);
        Assert.Equal(8, _f.Store.Feeds().Count);
        Assert.Equal(3, _f.Store.Folders().Count);
    }

    [Fact]
    public void Cancelling_leaves_the_library_alone_and_an_empty_file_says_so()
    {
        var import = _f.Shell.CreateImportFeeds();
        import.Preview("empty.opml", OpmlDocument.Parse("<opml><body /></opml>"));
        Assert.Equal("No feeds in that file.", import.Error);
        Assert.False(import.ImportCommand.CanExecute(null));

        import.Preview("my-feeds.opml", OpmlDocument.Parse(Opml));
        import.CloseCommand.Execute(null);
        Assert.Equal(4, _f.Store.Feeds().Count);
    }

    [Fact]
    public async Task Export_counts_what_the_file_holds_and_writes_it()
    {
        var export = _f.Shell.CreateExportFeeds();
        Assert.Equal((4, 2), (export.FeedCount, export.FolderCount));
        await export.ExportCommand.ExecuteAsync(null);
        Assert.Equal("Saved.", export.ResultText);
        Assert.Equal(4, OpmlDocument.Parse(_f.Files.Saved!).Count);
    }

    [Fact]
    public void Each_pane_steps_its_own_size_within_the_scale()
    {
        var settings = _f.Shell.Settings;
        settings.ListStep = 4.4;
        Assert.Equal(120, _f.Settings.Current.ListTextSize);
        Assert.Equal("120 %", settings.ListSizeText);
        Assert.Equal(AppSettings.StandardTextSize, _f.Settings.Current.FeedsTextSize);
        Assert.Equal("100 % · Standard", settings.FeedsSizeText);

        _f.Settings.Update(s => s.ArticleTextSize = AppSettings.TextSizes[^1]);
        settings.LargerCommand.Execute(TextPane.Article);
        Assert.Equal(AppSettings.TextSizes[^1], _f.Settings.Current.ArticleTextSize);

        settings.Panes[1].Command.Execute(settings.Panes[1].Key);
        Assert.Equal(TextPane.List, settings.SelectedPane);
        Assert.Equal(4, settings.PaneStep);
        settings.SmallerCommand.Execute(settings.SelectedPane);
        Assert.Equal(110, _f.Settings.Current.ListTextSize);
        Assert.Equal("110 %", settings.PaneSizeText);

        settings.ResetTextSizesCommand.Execute(null);
        Assert.Equal([100, 100, 100],
            [_f.Settings.Current.FeedsTextSize, _f.Settings.Current.ListTextSize, _f.Settings.Current.ArticleTextSize]);
    }

    [Fact]
    public void The_system_size_applies_on_top_only_while_followed()
    {
        var settings = _f.Shell.Settings;
        _f.Settings.Update(s => s.FeedsTextSize = 120);
        settings.SystemTextScale = 1.5;
        Assert.Equal(1.8, settings.FeedsScale, 3);
        Assert.Equal(24, settings.ArticleFontSize, 3);
        settings.FollowSystemTextSize = false;
        Assert.Equal(1.2, settings.FeedsScale, 3);
        Assert.Equal(16, settings.ArticleFontSize, 3);
    }

    [Fact]
    public void Coming_back_refreshes_only_when_the_last_pass_is_older_than_the_interval()
    {
        _f.Settings.Update(s => s.LastRefresh = ShellFixture.Now.AddMinutes(-10));
        Assert.False(_f.Shell.RefreshIfStale());
        _f.Clock.Advance(TimeSpan.FromMinutes(25));
        Assert.True(_f.Shell.RefreshIfStale());
        _f.Settings.Update(s => s.RefreshIntervalMinutes = 0);
        Assert.False(_f.Shell.RefreshIfStale());
    }
}
