using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using RSSmote.Core.Models;
using RSSmote.Core.Rendering;
using RSSmote.Data;

namespace RSSmote.Server;

/// <summary>
/// The library for native readers over the Reader API (A25): folders, feeds and articles to read, and read and
/// starred to write. Feeds and folders are kept in the browser app, so their writes answer 403.
/// </summary>
public static class ReaderApi
{
    /// <summary>The same endpoints under both, as a client may or may not add the first to the address it is given.</summary>
    public static readonly string[] Prefixes = ["/api/greader.php", "/api/reader"];

    private const string Policy = "reader";
    private const int MaxIds = 10_000;
    private const int MaxItems = 1_000;
    private const int DefaultCount = 20;

    public static bool Owns(PathString path) => Prefixes.Any(p => path.StartsWithSegments(p));

    public static void AddReader(this IServiceCollection services)
    {
        services.AddSingleton<ReaderTokens>();
        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ReaderAuthHandler>(ReaderAuthHandler.SchemeName, null);
        services.AddAuthorizationBuilder().AddPolicy(Policy, p => p
            .AddAuthenticationSchemes(ReaderAuthHandler.SchemeName)
            .RequireAuthenticatedUser());
    }

    public static void MapReader(this WebApplication app)
    {
        foreach (var prefix in Prefixes)
        {
            Map(app.MapGroup(prefix));
        }
    }

    private static void Map(RouteGroupBuilder root)
    {
        root.MapMethods("/accounts/ClientLogin", [HttpMethods.Get, HttpMethods.Post], async (HttpRequest request, AccessKey key, ReaderTokens tokens) =>
        {
            var args = await Args.Read(request);
            if (!key.Matches(args.One("Passwd")))
            {
                return Results.Text("Error=BadAuthentication\n", "text/plain", statusCode: StatusCodes.Status401Unauthorized);
            }

            var token = tokens.Issue();
            return Results.Text($"SID={token}\nLSID=null\nAuth={token}\n", "text/plain");
        }).RequireRateLimiting(Access.LoginPolicy);

        var api = root.MapGroup("/reader/api/0").RequireAuthorization(Policy);

        api.MapGet("/token", (ReaderTokens tokens) => Results.Text(tokens.Issue() + "\n", "text/plain"));

        api.MapGet("/user-info", () => Results.Json(
            new ReaderUserInfo("1", "RSSmote", "1", ""), ReaderJsonContext.Default.ReaderUserInfo));

        api.MapGet("/subscription/list", (Store store) =>
        {
            var view = new LibraryView(store);
            var subscriptions = view.Feeds.Values
                .OrderBy(f => f.Title, StringComparer.OrdinalIgnoreCase)
                .Select(f => new ReaderSubscription(
                    ReaderIds.Feed(f.Id),
                    f.Title,
                    view.FolderOf(f.Id) is { } folder ? [new ReaderCategory(ReaderIds.Label(folder.Title), folder.Title)] : [],
                    f.Url,
                    f.SiteUrl ?? f.Url,
                    f.FaviconUrl))
                .ToList();
            return Results.Json(new ReaderSubscriptions(subscriptions), ReaderJsonContext.Default.ReaderSubscriptions);
        });

        api.MapGet("/tag/list", (Store store) =>
        {
            List<ReaderTag> tags = [new(ReaderIds.Starred, null), .. store.Folders().Select(f => new ReaderTag(ReaderIds.Label(f.Title), "folder"))];
            return Results.Json(new ReaderTags(tags), ReaderJsonContext.Default.ReaderTags);
        });

        api.MapGet("/unread-count", (Store store) =>
        {
            var view = new LibraryView(store);
            var unread = store.Refs().Where(r => r.Unread).ToList();
            var counts = new List<ReaderUnreadCount>();
            foreach (var feed in unread.GroupBy(r => r.FeedId))
            {
                counts.Add(Count(ReaderIds.Feed(feed.Key), [.. feed]));
            }

            foreach (var folder in unread.Where(r => view.FolderOf(r.FeedId) is not null).GroupBy(r => view.FolderOf(r.FeedId)!.Title))
            {
                counts.Add(Count(ReaderIds.Label(folder.Key), [.. folder]));
            }

            counts.Add(Count(ReaderIds.ReadingList, unread));
            return Results.Json(new ReaderUnreadCounts(unread.Count, counts), ReaderJsonContext.Default.ReaderUnreadCounts);
        });

        api.MapMethods("/stream/items/ids", [HttpMethods.Get, HttpMethods.Post], async (HttpRequest request, Store store) =>
        {
            var args = await Args.Read(request);
            var (page, continuation) = Page(args, new LibraryView(store), store, args.One("s") ?? ReaderIds.ReadingList, MaxIds);
            var refs = page.Select(r => new ReaderItemRef(r.Id.ToString(CultureInfo.InvariantCulture))).ToList();
            return Results.Json(new ReaderItemIds(refs, continuation), ReaderJsonContext.Default.ReaderItemIds);
        });

        api.MapMethods("/stream/items/contents", [HttpMethods.Get, HttpMethods.Post], async (HttpRequest request, Store store, ILoggerFactory loggers) =>
        {
            var args = await Args.Read(request);
            var ids = ItemIds(args, loggers).Take(MaxItems).ToList();
            var view = new LibraryView(store);
            return Results.Json(new ReaderStream(ReaderIds.ReadingList, Now(), Contents(view, store, ids), null),
                ReaderJsonContext.Default.ReaderStream);
        });

        api.MapGet("/stream/contents/{**streamId}", async (string? streamId, HttpRequest request, Store store) =>
        {
            var args = await Args.Read(request);
            // The server leaves an escaped slash escaped in the path.
            var stream = streamId is { Length: > 0 }
                ? streamId.Replace("%2F", "/", StringComparison.OrdinalIgnoreCase)
                : args.One("s") ?? ReaderIds.ReadingList;
            var view = new LibraryView(store);
            var (page, continuation) = Page(args, view, store, stream, MaxItems);
            return Results.Json(new ReaderStream(stream, Now(), Contents(view, store, [.. page.Select(r => r.Id)]), continuation),
                ReaderJsonContext.Default.ReaderStream);
        });

        api.MapPost("/edit-tag", async (HttpRequest request, Store store, ILoggerFactory loggers) =>
        {
            var args = await Args.Read(request);
            var ids = ItemIds(args, loggers);
            foreach (var tag in args.All("a"))
            {
                Tag(store, ids, ReaderIds.Normalize(tag), true);
            }

            foreach (var tag in args.All("r"))
            {
                Tag(store, ids, ReaderIds.Normalize(tag), false);
            }

            return Results.Text("OK", "text/plain");
        });

        api.MapPost("/mark-all-as-read", async (HttpRequest request, Store store) =>
        {
            var args = await Args.Read(request);
            // ts is in microseconds: only what the client had seen by then is marked.
            var cutoff = long.TryParse(args.One("ts"), out var usec)
                ? DateTimeOffset.FromUnixTimeMilliseconds(Math.Clamp(usec / 1000, 0, DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()))
                : DateTimeOffset.MaxValue;
            var match = new LibraryView(store).Match(args.One("s") ?? ReaderIds.ReadingList);
            var ids = store.Refs().Where(r => r.Unread && r.Published <= cutoff && match(r)).Select(r => r.Id).ToList();
            store.SetUnread(ids, false);
            return Results.Text("OK", "text/plain");
        });

        foreach (var write in new[] { "/subscription/edit", "/subscription/quickadd", "/rename-tag", "/disable-tag" })
        {
            api.MapPost(write, () => Results.StatusCode(StatusCodes.Status403Forbidden));
        }
    }

    /// <summary>The request's item ids (<c>i</c>); an id it cannot read is logged, so a client's id form can be seen.</summary>
    private static List<long> ItemIds(Args args, ILoggerFactory loggers)
    {
        var given = args.All("i").ToList();
        var ids = given.Select(ReaderIds.ParseItem).OfType<long>().Distinct().ToList();
        if (given.FirstOrDefault(v => ReaderIds.ParseItem(v) is null) is { } unknown)
        {
            loggers.CreateLogger(typeof(ReaderApi)).LogWarning(
                "Reader API: {Count} of {Given} item ids were not understood, such as {Example}.",
                given.Count(v => ReaderIds.ParseItem(v) is null), given.Count, unknown);
        }

        return ids;
    }

    private static ReaderUnreadCount Count(string id, IReadOnlyList<ItemRef> unread) =>
        new(id, unread.Count, Usec(unread.Max(r => r.Published)));

    private static void Tag(Store store, IReadOnlyCollection<long> ids, string tag, bool add)
    {
        switch (tag)
        {
            case ReaderIds.Read:
                store.SetUnread(ids, !add);
                break;
            case ReaderIds.Starred:
                store.SetStarred(ids, add);
                break;
        }
    }

    /// <summary>
    /// One page of a stream, newest first unless <c>r=o</c>. <c>xt</c> excludes a stream and <c>it</c> keeps only one;
    /// <c>ot</c> and <c>nt</c> bound the publish time in seconds. The continuation is the last item's date and id.
    /// </summary>
    private static (List<ItemRef> Page, string? Continuation) Page(Args args, LibraryView view, Store store, string stream, int max)
    {
        IEnumerable<ItemRef> refs = store.Refs().Where(view.Match(stream));
        foreach (var excluded in args.All("xt"))
        {
            var match = view.Match(excluded);
            refs = refs.Where(r => !match(r));
        }

        foreach (var included in args.All("it"))
        {
            refs = refs.Where(view.Match(included));
        }

        if (Seconds(args.One("ot")) is { } after)
        {
            refs = refs.Where(r => r.Published >= after);
        }

        if (Seconds(args.One("nt")) is { } before)
        {
            refs = refs.Where(r => r.Published < before);
        }

        var oldestFirst = args.One("r") == "o";
        if (oldestFirst)
        {
            refs = refs.Reverse();
        }

        if (ParseKey(args.One("c")) is { } from)
        {
            refs = refs.Where(r => oldestFirst ? Key(r).CompareTo(from) > 0 : Key(r).CompareTo(from) < 0);
        }

        var count = Math.Clamp(int.TryParse(args.One("n"), out var n) ? n : DefaultCount, 1, max);
        var page = refs.Take(count + 1).ToList();
        if (page.Count <= count)
        {
            return (page, null);
        }

        page.RemoveAt(count);
        var (ms, id) = Key(page[^1]);
        return (page, string.Create(CultureInfo.InvariantCulture, $"{ms}_{id}"));
    }

    private static (long Ms, long Id) Key(ItemRef r) => (r.Published.ToUnixTimeMilliseconds(), r.Id);

    private static (long Ms, long Id)? ParseKey(string? value)
    {
        var parts = value?.Split('_');
        if (parts is not [var ms, var id]
            || !long.TryParse(ms, CultureInfo.InvariantCulture, out var date)
            || !long.TryParse(id, CultureInfo.InvariantCulture, out var number))
        {
            return null;
        }

        return (date, number);
    }

    private static DateTimeOffset? Seconds(string? value) =>
        long.TryParse(value, CultureInfo.InvariantCulture, out var s) && s >= 0 && s < 253_402_300_800
            ? DateTimeOffset.FromUnixTimeSeconds(s)
            : null;

    private static List<ReaderItem> Contents(LibraryView view, Store store, IReadOnlyList<long> ids)
    {
        var items = store.Items(ids).ToDictionary(i => i.Id);
        return [.. ids.Where(items.ContainsKey).Select(id => view.ToReader(items[id]))];
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static string Usec(DateTimeOffset time) =>
        (time.ToUnixTimeMilliseconds() * 1000).ToString(CultureInfo.InvariantCulture);

    /// <summary>Folders and feeds as they are for one request, and what the protocol's stream ids mean in them.</summary>
    private sealed class LibraryView
    {
        public LibraryView(Store store)
        {
            Feeds = store.Feeds().ToDictionary(f => f.Id);
            Folders = store.Folders().ToDictionary(f => f.Id);
        }

        public IReadOnlyDictionary<long, Feed> Feeds { get; }

        public IReadOnlyDictionary<long, Folder> Folders { get; }

        public Folder? FolderOf(long feedId) =>
            Feeds.TryGetValue(feedId, out var feed) && feed.FolderId is { } id && Folders.TryGetValue(id, out var folder) ? folder : null;

        /// <summary>Which items a stream id holds; an id it does not know holds none.</summary>
        public Func<ItemRef, bool> Match(string stream)
        {
            var id = ReaderIds.Normalize(stream);
            switch (id)
            {
                case ReaderIds.ReadingList:
                    return _ => true;
                case ReaderIds.Read:
                    return r => !r.Unread;
                case ReaderIds.Starred:
                    return r => r.Starred;
            }

            if (id.StartsWith(ReaderIds.LabelPrefix, StringComparison.Ordinal))
            {
                var title = id[ReaderIds.LabelPrefix.Length..];
                return r => FolderOf(r.FeedId)?.Title == title;
            }

            if (id.StartsWith(ReaderIds.FeedPrefix, StringComparison.Ordinal)
                && long.TryParse(id.AsSpan(ReaderIds.FeedPrefix.Length), CultureInfo.InvariantCulture, out var feedId))
            {
                return r => r.FeedId == feedId;
            }

            return _ => false;
        }

        public ReaderItem ToReader(Item item)
        {
            List<string> categories = [ReaderIds.ReadingList];
            if (FolderOf(item.FeedId) is { } folder)
            {
                categories.Add(ReaderIds.Label(folder.Title));
            }

            if (!item.Unread)
            {
                categories.Add(ReaderIds.Read);
            }

            if (item.Starred)
            {
                categories.Add(ReaderIds.Starred);
            }

            List<ReaderLink> links = item.Url is { Length: > 0 } url ? [new ReaderLink(url)] : [];
            var feed = Feeds.GetValueOrDefault(item.FeedId);
            var ms = item.Published.ToUnixTimeMilliseconds();
            return new ReaderItem(
                ReaderIds.LongItem(item.Id),
                ms.ToString(CultureInfo.InvariantCulture),
                Usec(item.Published),
                item.Published.ToUnixTimeSeconds(),
                item.Title,
                item.Author,
                links,
                links,
                categories,
                new ReaderOrigin(ReaderIds.Feed(item.FeedId), item.FeedTitle, feed?.SiteUrl ?? feed?.Url),
                new ReaderContent(ArticleSanitizer.Sanitize(item.Content.Length > 0 ? item.Content : item.Summary, item.Url)));
        }
    }

    /// <summary>A request's parameters from its query and, for a form post, its body; a name may repeat.</summary>
    private sealed class Args(IQueryCollection query, IFormCollection? form)
    {
        public static async Task<Args> Read(HttpRequest request) =>
            new(request.Query, request.HasFormContentType ? await request.ReadFormAsync() : null);

        public IEnumerable<string> All(string name) =>
            query[name].Concat(form is null ? [] : form[name]).OfType<string>().Where(v => v.Length > 0);

        public string? One(string name) => All(name).FirstOrDefault();
    }
}
