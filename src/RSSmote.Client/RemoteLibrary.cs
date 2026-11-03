using RSSmote.Core.Api;
using RSSmote.Core.Discovery;
using RSSmote.Core.Library;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;

namespace RSSmote.Client;

/// <summary>
/// The server's library mirrored in memory (A20): folders, feeds and items without their content, so every read
/// answers at once. Flag writes change the mirror first and then the server; any other write goes to the server
/// and returns once the mirror has the result. The server's flag events are the truth and are always applied.
/// </summary>
public sealed class RemoteLibrary(ApiClient api) : ILibrary
{
    private const int ContentCacheSize = 50;

    private readonly Lock _gate = new();
    private readonly Dictionary<long, string> _content = [];
    private IReadOnlyList<Folder> _folders = [];
    private IReadOnlyList<Feed> _feeds = [];
    private Dictionary<long, Item> _items = [];
    private long _requested;
    private long _applied;

    public event EventHandler<LibraryChangedEventArgs>? Changed;

    /// <summary>Raised with every snapshot applied, which also carries the library settings and refresh state.</summary>
    public event EventHandler<LibrarySnapshot>? Loaded;

    /// <summary>Fetches the whole library; an answer older than one already applied is dropped.</summary>
    public async Task ReloadAsync(CancellationToken ct = default)
    {
        var ticket = Interlocked.Increment(ref _requested);
        var snapshot = await api.GetAsync("api/library", ApiJsonContext.Default.LibrarySnapshot, ct);
        lock (_gate)
        {
            if (ticket < _applied)
                return;
            _applied = ticket;
            _folders = snapshot.Folders;
            _feeds = snapshot.Feeds;
            _items = snapshot.Items.ToDictionary(i => i.Id);
        }
        Loaded?.Invoke(this, snapshot);
        Changed?.Invoke(this, new LibraryChangedEventArgs(null));
    }

    /// <summary>Applies flags from the server; raises <see cref="Changed"/> for the items that differed.</summary>
    public void ApplyFlags(IReadOnlyList<ItemFlags> flags)
    {
        var changed = new List<long>();
        lock (_gate)
            foreach (var f in flags)
            {
                if (_items.TryGetValue(f.Id, out var item) && (item.Unread != f.Unread || item.Starred != f.Starred))
                {
                    _items[f.Id] = item with { Unread = f.Unread, Starred = f.Starred };
                    changed.Add(f.Id);
                }
            }
        if (changed.Count > 0)
            Changed?.Invoke(this, new LibraryChangedEventArgs(changed));
    }

    // MARK: Reads

    public IReadOnlyList<Folder> Folders()
    {
        lock (_gate)
            return _folders;
    }

    public Folder? FolderNamed(string title) =>
        Folders().FirstOrDefault(f => string.Equals(f.Title, title, StringComparison.CurrentCultureIgnoreCase));

    public IReadOnlyList<Feed> Feeds()
    {
        lock (_gate)
            return _feeds;
    }

    public Feed? Feed(long id) => Feeds().FirstOrDefault(f => f.Id == id);

    public IReadOnlyList<Item> Items(ArticleStream stream, StreamFilter filter, string search = "", bool newestFirst = true, int limit = 3000)
    {
        lock (_gate)
        {
            var matches = Matching(stream, filter, search);
            var ordered = newestFirst
                ? matches.OrderByDescending(i => i.Published).ThenByDescending(i => i.Id)
                : matches.OrderBy(i => i.Published).ThenByDescending(i => i.Id);
            return [.. ordered.Take(limit)];
        }
    }

    public Item? Item(long id)
    {
        lock (_gate)
            return _items.GetValueOrDefault(id);
    }

    public int Count(ArticleStream stream, StreamFilter filter)
    {
        lock (_gate)
            return Matching(stream, filter, "").Count();
    }

    public IReadOnlyDictionary<long, int> Counts(StreamFilter filter)
    {
        lock (_gate)
            return _items.Values.Where(i => filter == StreamFilter.Starred ? i.Starred : i.Unread)
                .GroupBy(i => i.FeedId).ToDictionary(g => g.Key, g => g.Count());
    }

    public IReadOnlyDictionary<long, int> TotalCounts()
    {
        lock (_gate)
            return _items.Values.GroupBy(i => i.FeedId).ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>The same rule as the store's: search matches title, summary or feed title, ignoring case.</summary>
    private IEnumerable<Item> Matching(ArticleStream stream, StreamFilter filter, string search)
    {
        IEnumerable<Item> items = _items.Values;
        switch (stream)
        {
            case ArticleStream.InFolder folder:
                var inFolder = _feeds.Where(f => f.FolderId == folder.FolderId).Select(f => f.Id).ToHashSet();
                items = items.Where(i => inFolder.Contains(i.FeedId));
                break;
            case ArticleStream.OfFeed feed:
                items = items.Where(i => i.FeedId == feed.FeedId);
                break;
        }
        items = filter switch
        {
            StreamFilter.Unread => items.Where(i => i.Unread),
            StreamFilter.Starred => items.Where(i => i.Starred),
            _ => items,
        };
        var term = search.Trim();
        return term.Length == 0
            ? items
            : items.Where(i => i.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                               || i.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)
                               || i.FeedTitle.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    // MARK: Flag writes, applied here first

    public void SetUnread(IReadOnlyCollection<long> ids, bool unread) =>
        SetFlags(ids, i => i with { Unread = unread }, "api/items/unread", unread);

    public void SetStarred(IReadOnlyCollection<long> ids, bool starred) =>
        SetFlags(ids, i => i with { Starred = starred }, "api/items/starred", starred);

    public void MarkAllRead(ArticleStream stream)
    {
        var ids = Items(stream, StreamFilter.Unread, limit: int.MaxValue).Select(i => i.Id).ToList();
        if (ids.Count == 0)
            return;
        Apply(ids, i => i with { Unread = false });
        _ = WriteAsync(() => api.SendAsync(HttpMethod.Post, "api/items/read-all", stream, ApiJsonContext.Default.ArticleStream));
    }

    private void SetFlags(IReadOnlyCollection<long> ids, Func<Item, Item> change, string path, bool value)
    {
        if (ids.Count == 0)
            return;
        Apply(ids, change);
        _ = WriteAsync(() => api.SendAsync(HttpMethod.Post, path, new FlagRequest([.. ids], value), ApiJsonContext.Default.FlagRequest));
    }

    private void Apply(IReadOnlyCollection<long> ids, Func<Item, Item> change)
    {
        lock (_gate)
            foreach (var id in ids)
                if (_items.TryGetValue(id, out var item))
                    _items[id] = change(item);
        Changed?.Invoke(this, new LibraryChangedEventArgs([.. ids]));
    }

    /// <summary>A write that failed is undone by fetching the library again.</summary>
    private async Task WriteAsync(Func<Task> write)
    {
        try
        {
            await write();
        }
        catch (Exception e) when (e is HttpRequestException or SignedOutException or TaskCanceledException)
        {
            if (e is SignedOutException)
                return;
            try
            {
                await ReloadAsync();
            }
            catch (Exception again) when (again is HttpRequestException or SignedOutException or TaskCanceledException)
            {
                // The event stream fetches the library again when it reconnects.
            }
        }
    }

    // MARK: Writes the server answers

    public async Task<Item?> LoadItemAsync(long id)
    {
        if (Item(id) is not { } item)
            return null;
        lock (_gate)
            if (_content.TryGetValue(id, out var cached))
                return item with { Content = cached };
        var full = await api.GetAsync($"api/items/{id}", ApiJsonContext.Default.Item);
        lock (_gate)
        {
            if (_content.Count >= ContentCacheSize)
                _content.Clear();
            _content[id] = full.Content;
        }
        return full;
    }

    public async Task<Folder> AddFolderAsync(string title)
    {
        var folder = await api.SendAsync(HttpMethod.Post, "api/folders", new TitleRequest(title),
            ApiJsonContext.Default.TitleRequest, ApiJsonContext.Default.Folder);
        await ReloadAsync();
        return folder;
    }

    public async Task RenameFolderAsync(long id, string title)
    {
        await api.SendAsync(HttpMethod.Put, $"api/folders/{id}", new TitleRequest(title), ApiJsonContext.Default.TitleRequest);
        await ReloadAsync();
    }

    public async Task DeleteFolderAsync(long id, bool deleteFeeds)
    {
        await api.SendAsync(HttpMethod.Delete, $"api/folders/{id}?deleteFeeds={(deleteFeeds ? "true" : "false")}");
        await ReloadAsync();
    }

    public async Task<Feed> AddFeedAsync(FoundFeed found, long? folderId)
    {
        var feed = await api.SendAsync(HttpMethod.Post, "api/feeds", new AddFeedRequest(found, folderId),
            ApiJsonContext.Default.AddFeedRequest, ApiJsonContext.Default.Feed);
        await ReloadAsync();
        return feed;
    }

    public async Task UpdateFeedAsync(Feed feed)
    {
        await api.SendAsync(HttpMethod.Put, $"api/feeds/{feed.Id}", feed, ApiJsonContext.Default.Feed);
        await ReloadAsync();
    }

    public async Task DeleteFeedAsync(long id)
    {
        await api.SendAsync(HttpMethod.Delete, $"api/feeds/{id}");
        await ReloadAsync();
    }

    public async Task<int> ImportOpmlAsync(IReadOnlyList<OpmlOutline> outlines)
    {
        var result = await api.SendAsync(HttpMethod.Post, "api/opml", new ImportRequest(outlines),
            ApiJsonContext.Default.ImportRequest, ApiJsonContext.Default.CountResponse);
        await ReloadAsync();
        return result.Count;
    }
}
