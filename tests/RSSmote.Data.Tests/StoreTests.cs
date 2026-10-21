using Microsoft.Data.Sqlite;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;
using RSSmote.Core.Parsing;

namespace RSSmote.Data.Tests;

public sealed class StoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Store _store;

    public StoreTests()
    {
        _store = new Store(DbPath, new FixedClock(Now));
    }

    private string DbPath => Path.Combine(_folder, "rssmote.sqlite");

    public void Dispose()
    {
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private static ParsedItem Item(string guid, string title = "T", DateTimeOffset? published = null, string summary = "s") =>
        new(guid, title, $"https://x.example/{guid}", null, "<p>c</p>", summary, null, published ?? Now);

    [Fact]
    public void New_database_is_at_the_current_schema_version() => Assert.Equal(Store.SchemaVersion, _store.Version);

    [Fact]
    public void Upsert_inserts_once_and_refreshes_text_but_keeps_flags()
    {
        var feed = _store.AddFeed("https://a.example/feed", "A", null, null);
        Assert.Equal(1, _store.Upsert(feed.Id, [Item("1", "Old")]));
        var id = _store.Items(new ArticleStream.All(), StreamFilter.All).Single().Id;
        _store.SetUnread([id], false);
        _store.SetStarred([id], true);

        Assert.Equal(0, _store.Upsert(feed.Id, [Item("1", "New")]));

        var item = _store.Item(id)!;
        Assert.Equal("New", item.Title);
        Assert.False(item.Unread);
        Assert.True(item.Starred);
        Assert.Equal("A", item.FeedTitle);
    }

    [Fact]
    public void Upsert_skips_new_items_older_than_the_cutoff()
    {
        var feed = _store.AddFeed("https://a.example/feed", "A", null, null);
        var inserted = _store.Upsert(feed.Id, [Item("old", published: Now.AddDays(-40)), Item("new")], olderThan: Now.AddDays(-30));
        Assert.Equal(1, inserted);
        Assert.Equal("new", Assert.Single(_store.Items(new ArticleStream.All(), StreamFilter.All)).Guid);
    }

    [Fact]
    public void Items_filter_by_stream_flag_and_search_and_sort_by_date()
    {
        var folder = _store.AddFolder("Tech");
        var a = _store.AddFeed("https://a.example/feed", "Alpha", null, folder.Id);
        var b = _store.AddFeed("https://b.example/feed", "Beta", null, null);
        _store.Upsert(a.Id, [Item("a1", "Swift news", Now.AddHours(-2)), Item("a2", "Kotlin", Now.AddHours(-1))]);
        _store.Upsert(b.Id, [Item("b1", "Gardening", Now)]);

        var all = _store.Items(new ArticleStream.All(), StreamFilter.All);
        Assert.Equal(["b1", "a2", "a1"], all.Select(i => i.Guid));
        Assert.Equal(["a1", "a2"], _store.Items(new ArticleStream.All(), StreamFilter.All, newestFirst: false).Take(2).Select(i => i.Guid));
        Assert.Equal(["a2", "a1"], _store.Items(new ArticleStream.InFolder(folder.Id), StreamFilter.All).Select(i => i.Guid));
        Assert.Equal(["b1"], _store.Items(new ArticleStream.OfFeed(b.Id), StreamFilter.All).Select(i => i.Guid));
        Assert.Equal(["a1"], _store.Items(new ArticleStream.All(), StreamFilter.All, search: "swift").Select(i => i.Guid));
        Assert.Equal(2, _store.Items(new ArticleStream.All(), StreamFilter.All, search: "alpha").Count);

        _store.SetStarred([all[0].Id], true);
        _store.SetUnread([all[1].Id], false);
        Assert.Equal(["b1"], _store.Items(new ArticleStream.All(), StreamFilter.Starred).Select(i => i.Guid));
        Assert.Equal(["b1", "a1"], _store.Items(new ArticleStream.All(), StreamFilter.Unread).Select(i => i.Guid));
    }

    [Fact]
    public void Counts_follow_the_filter()
    {
        var a = _store.AddFeed("https://a.example/feed", "A", null, null);
        _store.Upsert(a.Id, [Item("1"), Item("2"), Item("3")]);
        var ids = _store.Items(new ArticleStream.All(), StreamFilter.All).Select(i => i.Id).ToList();
        _store.SetUnread([ids[0]], false);
        _store.SetStarred([ids[0], ids[1]], true);

        Assert.Equal(2, _store.Counts(StreamFilter.Unread)[a.Id]);
        Assert.Equal(2, _store.Counts(StreamFilter.Starred)[a.Id]);
        Assert.Equal(2, _store.Count(new ArticleStream.OfFeed(a.Id), StreamFilter.Unread));
        Assert.Equal(3, _store.Count(new ArticleStream.All(), StreamFilter.All));
    }

    [Fact]
    public void Mark_all_read_only_touches_the_stream()
    {
        var folder = _store.AddFolder("F");
        var a = _store.AddFeed("https://a.example/feed", "A", null, folder.Id);
        var b = _store.AddFeed("https://b.example/feed", "B", null, null);
        _store.Upsert(a.Id, [Item("a")]);
        _store.Upsert(b.Id, [Item("b")]);

        _store.MarkAllRead(new ArticleStream.InFolder(folder.Id));

        Assert.Equal(["b"], _store.Items(new ArticleStream.All(), StreamFilter.Unread).Select(i => i.Guid));
    }

    [Fact]
    public void Prune_removes_only_old_read_unstarred_items()
    {
        var a = _store.AddFeed("https://a.example/feed", "A", null, null);
        _store.Upsert(a.Id, [
            Item("old-read", published: Now.AddDays(-40)),
            Item("old-starred", published: Now.AddDays(-40)),
            Item("old-unread", published: Now.AddDays(-40)),
            Item("new-read", published: Now.AddDays(-1)),
        ]);
        var byGuid = _store.Items(new ArticleStream.All(), StreamFilter.All).ToDictionary(i => i.Guid, i => i.Id);
        _store.SetUnread([byGuid["old-read"], byGuid["old-starred"], byGuid["new-read"]], false);
        _store.SetStarred([byGuid["old-starred"]], true);

        Assert.Equal(1, _store.Prune(30));
        Assert.Equal(0, _store.Prune(0));
        Assert.DoesNotContain("old-read", _store.Items(new ArticleStream.All(), StreamFilter.All).Select(i => i.Guid));
    }

    [Fact]
    public void Deleting_a_folder_can_keep_or_drop_its_feeds()
    {
        var keep = _store.AddFolder("Keep");
        var drop = _store.AddFolder("Drop");
        var kept = _store.AddFeed("https://k.example/feed", "K", null, keep.Id);
        var dropped = _store.AddFeed("https://d.example/feed", "D", null, drop.Id);
        _store.Upsert(dropped.Id, [Item("d")]);

        _store.DeleteFolder(keep.Id, deleteFeeds: false);
        _store.DeleteFolder(drop.Id, deleteFeeds: true);

        Assert.Empty(_store.Folders());
        Assert.Null(_store.Feed(kept.Id)!.FolderId);
        Assert.Null(_store.Feed(dropped.Id));
        Assert.Empty(_store.Items(new ArticleStream.All(), StreamFilter.All));
    }

    [Fact]
    public void Adding_a_known_url_returns_the_existing_feed()
    {
        var first = _store.AddFeed("https://a.example/feed", "A", null, null);
        var second = _store.AddFeed("https://a.example/feed", "Other", null, null);
        Assert.Equal(first.Id, second.Id);
        Assert.Single(_store.Feeds());
    }

    [Fact]
    public void Opml_import_reuses_folders_by_name_and_skips_known_feeds()
    {
        _store.AddFolder("tech");
        _store.AddFeed("https://known.example/feed", "Known", null, null);
        var added = _store.ImportOpml([
            new OpmlOutline("Known", "https://known.example/feed", null, null),
            new OpmlOutline("A", "https://a.example/feed", null, "Tech"),
            new OpmlOutline("B", "https://b.example/feed", null, "News"),
        ]);
        Assert.Equal(2, added);
        Assert.Equal(["News", "tech"], _store.Folders().Select(f => f.Title));
        Assert.Equal(_store.FolderNamed("TECH")!.Id, _store.FeedWithUrl("https://a.example/feed")!.FolderId);
    }

    [Fact]
    public void Changed_reports_item_ids_only_for_flag_changes()
    {
        var events = new List<StoreChangedEventArgs>();
        _store.Changed += (_, e) => events.Add(e);
        var a = _store.AddFeed("https://a.example/feed", "A", null, null);
        _store.Upsert(a.Id, [Item("1")]);
        var id = _store.Items(new ArticleStream.All(), StreamFilter.All).Single().Id;
        _store.SetStarred([id], true);

        Assert.Equal(3, events.Count);
        Assert.False(events[0].FlagsOnly);
        Assert.Equal([id], events[2].ItemIds!);
    }

    [Fact]
    public void Published_round_trips_to_the_millisecond()
    {
        var a = _store.AddFeed("https://a.example/feed", "A", null, null);
        var when = new DateTimeOffset(2025, 10, 6, 9, 30, 15, 123, TimeSpan.Zero);
        _store.Upsert(a.Id, [Item("1", published: when)]);
        Assert.Equal(when, _store.Items(new ArticleStream.All(), StreamFilter.All).Single().Published);
    }

    [Fact]
    public void Opens_a_library_written_by_the_macOS_app()
    {
        var path = Path.Combine(_folder, "legacy.sqlite");
        using (var db = new SqliteConnection($"Data Source={path};Pooling=False"))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            // Tables exactly as the macOS app creates them, at user_version 0.
            cmd.CommandText = """
                CREATE TABLE folders (id INTEGER PRIMARY KEY, title TEXT NOT NULL);
                CREATE TABLE feeds (id INTEGER PRIMARY KEY, folder_id INTEGER REFERENCES folders(id) ON DELETE SET NULL,
                    title TEXT NOT NULL, url TEXT NOT NULL UNIQUE, site_url TEXT, last_error TEXT);
                CREATE TABLE items (id INTEGER PRIMARY KEY, feed_id INTEGER NOT NULL REFERENCES feeds(id) ON DELETE CASCADE,
                    guid TEXT NOT NULL, title TEXT NOT NULL, author TEXT, url TEXT, content TEXT NOT NULL, summary TEXT NOT NULL,
                    image_url TEXT, published REAL NOT NULL, unread INTEGER NOT NULL DEFAULT 1, starred INTEGER NOT NULL DEFAULT 0,
                    UNIQUE(feed_id, guid));
                INSERT INTO folders VALUES (1, 'Apple');
                INSERT INTO feeds VALUES (1, 1, 'Daring Fireball', 'https://daringfireball.net/feeds/main', NULL, NULL);
                INSERT INTO items(feed_id, guid, title, content, summary, published, unread, starred)
                    VALUES (1, 'g1', 'Hello', '<p>x</p>', 'x', 1759743000.5, 0, 1);
                """;
            cmd.ExecuteNonQuery();
        }

        using (var store = new Store(path))
        {
            Assert.Equal(Store.SchemaVersion, store.Version);
            var item = Assert.Single(store.Items(new ArticleStream.InFolder(1), StreamFilter.Starred));
            Assert.Equal("Hello", item.Title);
            Assert.Equal("Daring Fireball", item.FeedTitle);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1759743000500), item.Published);
        }
        using (var reopened = new Store(path))
            Assert.Single(reopened.Feeds());
    }
}
