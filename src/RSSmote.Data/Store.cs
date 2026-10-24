using Microsoft.Data.Sqlite;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;
using RSSmote.Core.Parsing;

namespace RSSmote.Data;

/// <summary>Raised after every write. <see cref="ItemIds"/> is set when only read/starred flags changed.</summary>
public sealed class StoreChangedEventArgs(IReadOnlyList<long>? itemIds) : EventArgs
{
    public IReadOnlyList<long>? ItemIds { get; } = itemIds;
    public bool FlagsOnly => ItemIds is not null;
}

/// <summary>
/// The local library in one SQLite file (A8): folders, feeds and items. One connection behind a lock, so it is
/// safe to call from any thread; <see cref="Changed"/> is raised on the calling thread after the lock is released.
/// </summary>
public sealed class Store : IDisposable
{
    /// <summary>Schema version written to <c>PRAGMA user_version</c>. Version 1 matches the macOS app's tables.</summary>
    public const int SchemaVersion = 1;

    private const string ItemColumns = """
        i.id, i.feed_id, i.guid, i.title, i.author, i.url, i.content, i.summary, i.image_url,
        i.published, i.unread, i.starred, f.title
        """;

    private readonly SqliteConnection _db;
    private readonly Lock _gate = new();
    private readonly TimeProvider _clock;
    private SqliteTransaction? _tx;

    public event EventHandler<StoreChangedEventArgs>? Changed;

    public Store(string path, TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } folder)
            Directory.CreateDirectory(folder);
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
        };
        _db = new SqliteConnection(connection.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL");
        Exec("PRAGMA foreign_keys=ON");
        Migrate();
    }

    public int Version
    {
        get
        {
            lock (_gate)
                return Convert.ToInt32(Scalar("PRAGMA user_version"));
        }
    }

    public void Dispose()
    {
        lock (_gate)
            _db.Dispose();
    }

    // MARK: Schema

    private void Migrate()
    {
        lock (_gate)
        {
            var version = Convert.ToInt32(Scalar("PRAGMA user_version"));
            if (version < 1)
                InTransaction(() =>
                {
                    // IF NOT EXISTS: a library written by the macOS app already has these tables at version 0.
                    Exec("""
                        CREATE TABLE IF NOT EXISTS folders (
                            id INTEGER PRIMARY KEY,
                            title TEXT NOT NULL
                        )
                        """);
                    Exec("""
                        CREATE TABLE IF NOT EXISTS feeds (
                            id INTEGER PRIMARY KEY,
                            folder_id INTEGER REFERENCES folders(id) ON DELETE SET NULL,
                            title TEXT NOT NULL,
                            url TEXT NOT NULL UNIQUE,
                            site_url TEXT,
                            last_error TEXT
                        )
                        """);
                    Exec("""
                        CREATE TABLE IF NOT EXISTS items (
                            id INTEGER PRIMARY KEY,
                            feed_id INTEGER NOT NULL REFERENCES feeds(id) ON DELETE CASCADE,
                            guid TEXT NOT NULL,
                            title TEXT NOT NULL,
                            author TEXT,
                            url TEXT,
                            content TEXT NOT NULL,
                            summary TEXT NOT NULL,
                            image_url TEXT,
                            published REAL NOT NULL,
                            unread INTEGER NOT NULL DEFAULT 1,
                            starred INTEGER NOT NULL DEFAULT 0,
                            UNIQUE(feed_id, guid)
                        )
                        """);
                    Exec("CREATE INDEX IF NOT EXISTS items_published ON items(published)");
                    Exec("CREATE INDEX IF NOT EXISTS items_feed_unread ON items(feed_id, unread)");
                    Exec("PRAGMA user_version = 1");
                });
        }
    }

    // MARK: Folders

    public IReadOnlyList<Folder> Folders()
    {
        lock (_gate)
            return Query("SELECT id, title FROM folders ORDER BY title COLLATE NOCASE", [],
                r => new Folder(r.GetInt64(0), r.GetString(1)));
    }

    public Folder AddFolder(string title)
    {
        Folder folder;
        lock (_gate)
            folder = AddFolderCore(title);
        Notify();
        return folder;
    }

    public Folder? FolderNamed(string title) =>
        Folders().FirstOrDefault(f => string.Equals(f.Title, title, StringComparison.CurrentCultureIgnoreCase));

    public void RenameFolder(long id, string title)
    {
        lock (_gate)
            Run("UPDATE folders SET title=? WHERE id=?", title, id);
        Notify();
    }

    /// <summary>Deletes the folder; its feeds are deleted too or moved out of the folder.</summary>
    public void DeleteFolder(long id, bool deleteFeeds)
    {
        lock (_gate)
            InTransaction(() =>
            {
                if (deleteFeeds)
                    Run("DELETE FROM feeds WHERE folder_id=?", id);
                Run("DELETE FROM folders WHERE id=?", id);
            });
        Notify();
    }

    private Folder AddFolderCore(string title)
    {
        Run("INSERT INTO folders(title) VALUES(?)", title);
        return new Folder(LastId(), title);
    }

    // MARK: Feeds

    public IReadOnlyList<Feed> Feeds()
    {
        lock (_gate)
            return Query("SELECT id, title, url, site_url, folder_id, last_error FROM feeds ORDER BY title COLLATE NOCASE", [],
                r => new Feed(r.GetInt64(0), r.GetString(1), r.GetString(2), NullableString(r, 3),
                    r.IsDBNull(4) ? null : r.GetInt64(4), NullableString(r, 5)));
    }

    public Feed? Feed(long id) => Feeds().FirstOrDefault(f => f.Id == id);

    public Feed? FeedWithUrl(string url) => Feeds().FirstOrDefault(f => f.Url == url);

    /// <summary>Adds a feed, or returns the existing one with the same URL.</summary>
    public Feed AddFeed(string url, string title, string? siteUrl, long? folderId)
    {
        Feed feed;
        lock (_gate)
            feed = AddFeedCore(url, title, siteUrl, folderId);
        Notify();
        return feed;
    }

    public void UpdateFeed(Feed feed)
    {
        lock (_gate)
            Run("UPDATE feeds SET title=?, url=?, site_url=?, folder_id=?, last_error=? WHERE id=?",
                feed.Title, feed.Url, feed.SiteUrl, feed.FolderId, feed.LastError, feed.Id);
        Notify();
    }

    /// <summary>Removes the feed and all its items, starred ones included.</summary>
    public void DeleteFeed(long id)
    {
        lock (_gate)
            Run("DELETE FROM feeds WHERE id=?", id);
        Notify();
    }

    private Feed AddFeedCore(string url, string title, string? siteUrl, long? folderId)
    {
        var existing = Query("SELECT id, title, url, site_url, folder_id, last_error FROM feeds WHERE url=?", [url],
            r => new Feed(r.GetInt64(0), r.GetString(1), r.GetString(2), NullableString(r, 3),
                r.IsDBNull(4) ? null : r.GetInt64(4), NullableString(r, 5)));
        if (existing.Count > 0)
            return existing[0];
        Run("INSERT INTO feeds(url, title, site_url, folder_id) VALUES(?,?,?,?)", url, title, siteUrl, folderId);
        return new Feed(LastId(), title, url, siteUrl, folderId, null);
    }

    /// <summary>Creates folders and feeds for outlines whose feeds are not stored yet; returns how many feeds were added.</summary>
    public int ImportOpml(IEnumerable<OpmlOutline> outlines)
    {
        var added = 0;
        lock (_gate)
            InTransaction(() =>
            {
                var folders = Query("SELECT id, title FROM folders", [], r => new Folder(r.GetInt64(0), r.GetString(1)))
                    .ToDictionary(f => f.Title, f => f.Id, StringComparer.CurrentCultureIgnoreCase);
                var urls = Query("SELECT url FROM feeds", [], r => r.GetString(0)).ToHashSet();
                foreach (var outline in outlines)
                {
                    if (!urls.Add(outline.XmlUrl))
                        continue;
                    long? folderId = null;
                    if (outline.Folder is { } name)
                        folderId = folders.TryGetValue(name, out var id) ? id : folders[name] = AddFolderCore(name).Id;
                    AddFeedCore(outline.XmlUrl, outline.Title, outline.HtmlUrl, folderId);
                    added++;
                }
            });
        Notify();
        return added;
    }

    // MARK: Items

    /// <summary>
    /// Inserts new items and refreshes the text of known ones (same feed and guid), keeping their read and
    /// starred flags. New items published before <paramref name="olderThan"/> are skipped so retention does not
    /// bring them back. Returns the number inserted.
    /// </summary>
    public int Upsert(long feedId, IEnumerable<ParsedItem> items, DateTimeOffset? olderThan = null)
    {
        var inserted = 0;
        lock (_gate)
            InTransaction(() =>
            {
                foreach (var item in items)
                {
                    var exists = Query("SELECT 1 FROM items WHERE feed_id=? AND guid=?", [feedId, item.Guid], _ => true).Count > 0;
                    if (exists)
                    {
                        Run("UPDATE items SET title=?, author=?, url=?, content=?, summary=?, image_url=? WHERE feed_id=? AND guid=?",
                            item.Title, item.Author, item.Link, item.Content, item.Summary, item.ImageUrl, feedId, item.Guid);
                        continue;
                    }
                    var published = item.Published ?? _clock.GetUtcNow();
                    if (olderThan is { } cutoff && published < cutoff)
                        continue;
                    Run("INSERT INTO items(feed_id, guid, title, author, url, content, summary, image_url, published) VALUES(?,?,?,?,?,?,?,?,?)",
                        feedId, item.Guid, item.Title, item.Author, item.Link, item.Content, item.Summary, item.ImageUrl, published);
                    inserted++;
                }
            });
        Notify();
        return inserted;
    }

    public IReadOnlyList<Item> Items(ArticleStream stream, StreamFilter filter, string search = "", bool newestFirst = true, int limit = 3000)
    {
        var (where, args) = Conditions(stream, filter, search);
        var sql = $"""
            SELECT {ItemColumns}
            FROM items i JOIN feeds f ON f.id = i.feed_id
            {where}
            ORDER BY i.published {(newestFirst ? "DESC" : "ASC")}, i.id DESC
            LIMIT {limit}
            """;
        lock (_gate)
            return Query(sql, args, ReadItem);
    }

    public Item? Item(long id)
    {
        lock (_gate)
            return Query($"SELECT {ItemColumns} FROM items i JOIN feeds f ON f.id = i.feed_id WHERE i.id=?", [id], ReadItem)
                .FirstOrDefault();
    }

    /// <summary>Number of items in a stream matching the filter, without loading them.</summary>
    public int Count(ArticleStream stream, StreamFilter filter)
    {
        var (where, args) = Conditions(stream, filter, "");
        lock (_gate)
            return Convert.ToInt32(Scalar($"SELECT COUNT(*) FROM items i JOIN feeds f ON f.id = i.feed_id {where}", args));
    }

    /// <summary>Unread counts per feed, or starred counts under the Starred filter.</summary>
    public IReadOnlyDictionary<long, int> Counts(StreamFilter filter)
    {
        var column = filter == StreamFilter.Starred ? "starred" : "unread";
        lock (_gate)
            return Query($"SELECT feed_id, COUNT(*) FROM items WHERE {column}=1 GROUP BY feed_id", [],
                    r => (r.GetInt64(0), r.GetInt32(1)))
                .ToDictionary(p => p.Item1, p => p.Item2);
    }

    /// <summary>Every item per feed, read or not.</summary>
    public IReadOnlyDictionary<long, int> TotalCounts()
    {
        lock (_gate)
            return Query("SELECT feed_id, COUNT(*) FROM items GROUP BY feed_id", [], r => (r.GetInt64(0), r.GetInt32(1)))
                .ToDictionary(p => p.Item1, p => p.Item2);
    }

    public void SetUnread(IReadOnlyCollection<long> ids, bool unread) => SetFlag("unread", ids, unread);

    public void SetStarred(IReadOnlyCollection<long> ids, bool starred) => SetFlag("starred", ids, starred);

    public void MarkAllRead(ArticleStream stream)
    {
        var ids = Items(stream, StreamFilter.Unread, limit: int.MaxValue).Select(i => i.Id).ToList();
        SetUnread(ids, false);
    }

    /// <summary>Marks unread items published before <paramref name="cutoff"/> as read; returns how many.</summary>
    public int MarkReadBefore(DateTimeOffset cutoff)
    {
        List<long> ids;
        lock (_gate)
        {
            ids = Query("SELECT id FROM items WHERE unread=1 AND published < ?", [cutoff], r => r.GetInt64(0));
            if (ids.Count > 0)
                Run("UPDATE items SET unread=0 WHERE unread=1 AND published < ?", cutoff);
        }
        if (ids.Count > 0)
            Notify(ids);
        return ids.Count;
    }

    /// <summary>Deletes read, unstarred items older than <paramref name="days"/> days; 0 keeps everything.</summary>
    public int Prune(int days)
    {
        if (days <= 0)
            return 0;
        var cutoff = _clock.GetUtcNow().AddDays(-days);
        int deleted;
        lock (_gate)
            deleted = Run("DELETE FROM items WHERE unread=0 AND starred=0 AND published < ?", cutoff);
        if (deleted > 0)
            Notify();
        return deleted;
    }

    private void SetFlag(string column, IReadOnlyCollection<long> ids, bool value)
    {
        if (ids.Count == 0)
            return;
        lock (_gate)
            InTransaction(() =>
            {
                foreach (var id in ids)
                    Run($"UPDATE items SET {column}=? WHERE id=?", value, id);
            });
        Notify(ids.ToList());
    }

    private static (string Where, object?[] Args) Conditions(ArticleStream stream, StreamFilter filter, string search)
    {
        var clauses = new List<string>();
        var args = new List<object?>();
        switch (stream)
        {
            case ArticleStream.InFolder folder:
                clauses.Add("i.feed_id IN (SELECT id FROM feeds WHERE folder_id=?)");
                args.Add(folder.FolderId);
                break;
            case ArticleStream.OfFeed feed:
                clauses.Add("i.feed_id=?");
                args.Add(feed.FeedId);
                break;
        }
        switch (filter)
        {
            case StreamFilter.Unread:
                clauses.Add("i.unread=1");
                break;
            case StreamFilter.Starred:
                clauses.Add("i.starred=1");
                break;
        }
        var term = search.Trim();
        if (term.Length > 0)
        {
            clauses.Add("(i.title LIKE ? OR i.summary LIKE ? OR f.title LIKE ?)");
            var like = $"%{term}%";
            args.AddRange([like, like, like]);
        }
        return (clauses.Count == 0 ? "" : "WHERE " + string.Join(" AND ", clauses), args.ToArray());
    }

    private static Item ReadItem(SqliteDataReader r) => new(
        r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), NullableString(r, 4), NullableString(r, 5),
        r.GetString(6), r.GetString(7), NullableString(r, 8),
        DateTimeOffset.FromUnixTimeMilliseconds((long)Math.Round(r.GetDouble(9) * 1000)),
        r.GetInt64(10) != 0, r.GetInt64(11) != 0, r.GetString(12));

    // MARK: SQLite helpers (call with the lock held)

    private void Notify(IReadOnlyList<long>? ids = null) => Changed?.Invoke(this, new StoreChangedEventArgs(ids));

    private void InTransaction(Action body)
    {
        using var tx = _db.BeginTransaction();
        _tx = tx;
        try
        {
            body();
            tx.Commit();
        }
        finally
        {
            _tx = null;
        }
    }

    /// <summary>Builds a command; each <c>?</c> in the SQL takes the next argument.</summary>
    private SqliteCommand Command(string sql, object?[] args)
    {
        var cmd = _db.CreateCommand();
        cmd.Transaction = _tx;
        var index = 0;
        var text = new System.Text.StringBuilder(sql.Length + args.Length * 3);
        foreach (var c in sql)
        {
            if (c == '?')
            {
                var name = "$p" + index;
                text.Append(name);
                cmd.Parameters.AddWithValue(name, ToDb(args[index]));
                index++;
            }
            else
            {
                text.Append(c);
            }
        }
        cmd.CommandText = text.ToString();
        return cmd;
    }

    private static object ToDb(object? value) => value switch
    {
        null => DBNull.Value,
        bool b => b ? 1L : 0L,
        DateTimeOffset d => d.ToUnixTimeMilliseconds() / 1000.0,
        _ => value,
    };

    private void Exec(string sql)
    {
        using var cmd = Command(sql, []);
        cmd.ExecuteNonQuery();
    }

    private int Run(string sql, params object?[] args)
    {
        using var cmd = Command(sql, args);
        return cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql, params object?[] args)
    {
        using var cmd = Command(sql, args);
        return cmd.ExecuteScalar();
    }

    private List<T> Query<T>(string sql, object?[] args, Func<SqliteDataReader, T> read)
    {
        using var cmd = Command(sql, args);
        using var reader = cmd.ExecuteReader();
        var result = new List<T>();
        while (reader.Read())
            result.Add(read(reader));
        return result;
    }

    private long LastId() => (long)Scalar("SELECT last_insert_rowid()")!;

    private static string? NullableString(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : r.GetString(ordinal);
}
