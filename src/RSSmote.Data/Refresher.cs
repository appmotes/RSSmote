using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;

namespace RSSmote.Data;

/// <summary>Outcome of one refresh pass.</summary>
public sealed record RefreshSummary(int Feeds, int NewItems, int Failed, int Pruned);

/// <summary>
/// The local account's network side (A10): fetches every feed (at most six at a time), stores new items, records
/// per-feed errors, marks old unread items read, prunes old items, and repeats on the interval from settings. A refresh requested while one is
/// running joins the running one. Events are raised on background threads.
/// </summary>
public sealed class Refresher : IDisposable
{
    public const int MaxConcurrentFeeds = 6;

    private readonly Store _store;
    private readonly FeedFinder _finder;
    private readonly SettingsStore _settings;
    private readonly TimeProvider _clock;
    private readonly Lock _gate = new();
    private Task<RefreshSummary>? _running;
    private ITimer? _timer;
    private int _scheduledMinutes = -1;
    private int _markReadAfterDays;
    private bool _started;

    public event EventHandler? StateChanged;

    public Refresher(Store store, FeedFinder finder, SettingsStore settings, TimeProvider? clock = null)
    {
        _store = store;
        _finder = finder;
        _settings = settings;
        _clock = clock ?? TimeProvider.System;
        _markReadAfterDays = settings.Current.MarkReadAfterDays;
        _settings.Changed += OnSettingsChanged;
    }

    public bool IsRefreshing
    {
        get
        {
            lock (_gate)
                return _running is { IsCompleted: false };
        }
    }

    public DateTimeOffset? LastRefresh => _settings.Current.LastRefresh;

    /// <summary>Refreshes once now and then on the interval from settings.</summary>
    public void Start()
    {
        lock (_gate)
            _started = true;
        Schedule();
        _ = RefreshAllAsync();
    }

    public void Stop()
    {
        lock (_gate)
        {
            _started = false;
            _timer?.Dispose();
            _timer = null;
            _scheduledMinutes = -1;
        }
    }

    public void Dispose()
    {
        _settings.Changed -= OnSettingsChanged;
        Stop();
    }

    public Task<RefreshSummary> RefreshAllAsync() => RefreshAsync(_store.Feeds());

    /// <summary>Refreshes the given feeds, or joins the refresh that is already running.</summary>
    public Task<RefreshSummary> RefreshAsync(IReadOnlyList<Feed> feeds)
    {
        Task<RefreshSummary> task;
        lock (_gate)
        {
            if (_running is { IsCompleted: false } running)
                return running;
            task = _running = RunAsync(feeds);
        }
        StateChanged?.Invoke(this, EventArgs.Empty);
        // After completion, so a listener reading IsRefreshing sees false.
        task.ContinueWith(_ => StateChanged?.Invoke(this, EventArgs.Empty), TaskScheduler.Default);
        return task;
    }

    /// <summary>Stores a discovered feed with its current items and returns the stored feed.</summary>
    public Feed AddFeed(FoundFeed found, long? folderId)
    {
        var url = found.Url.AbsoluteUri;
        var title = found.Feed.Title ?? found.Url.Host;
        var feed = _store.AddFeed(url, title, found.Feed.SiteUrl, folderId);
        _store.Upsert(feed.Id, found.Feed.Items, RetentionCutoff());
        return feed;
    }

    private async Task<RefreshSummary> RunAsync(IReadOnlyList<Feed> feeds)
    {
        await Task.Yield();
        var newItems = 0;
        var failed = 0;
        var cutoff = RetentionCutoff();
        await Parallel.ForEachAsync(feeds, new ParallelOptions { MaxDegreeOfParallelism = MaxConcurrentFeeds },
            async (feed, ct) =>
            {
                var (added, ok) = await RefreshFeedAsync(feed, cutoff, ct).ConfigureAwait(false);
                Interlocked.Add(ref newItems, added);
                if (!ok)
                    Interlocked.Increment(ref failed);
            }).ConfigureAwait(false);

        MarkOldRead();
        var pruned = _store.Prune(_settings.Current.KeepArchivedDays);
        _settings.Update(s => s.LastRefresh = _clock.GetUtcNow());
        return new RefreshSummary(feeds.Count, newItems, failed, pruned);
    }

    private async Task<(int Added, bool Ok)> RefreshFeedAsync(Feed feed, DateTimeOffset? cutoff, CancellationToken ct)
    {
        var updated = feed;
        var added = 0;
        try
        {
            if (!Uri.TryCreate(feed.Url, UriKind.Absolute, out var url))
                throw new FeedException(FeedError.InvalidUrl, "The address is not a valid URL.");
            var (data, finalUrl) = await _finder.GetAsync(url, ct).ConfigureAwait(false);
            var parsed = FeedParser.Parse(data, finalUrl)
                         ?? throw new FeedException(FeedError.NotFound, "The address no longer returns a feed.");

            added = _store.Upsert(feed.Id, parsed.Items, cutoff);
            if ((string.IsNullOrWhiteSpace(feed.Title) || feed.Title == feed.Url) && parsed.Title is { } title)
                updated = updated with { Title = title };
            if (feed.SiteUrl is null && parsed.SiteUrl is not null)
                updated = updated with { SiteUrl = parsed.SiteUrl };
            updated = updated with { LastError = null };
        }
        catch (Exception e) when (e is FeedException or HttpRequestException or TaskCanceledException or IOException)
        {
            updated = updated with { LastError = Describe(e) };
        }

        if (updated != feed)
            _store.UpdateFeed(updated);
        return (added, updated.LastError is null);
    }

    private static string Describe(Exception e) => e switch
    {
        FeedException fe => fe.Message,
        TaskCanceledException => "The server did not respond in time.",
        HttpRequestException { InnerException: { } inner } => inner.Message,
        _ => e.Message,
    };

    private DateTimeOffset? RetentionCutoff()
    {
        var days = _settings.Current.KeepArchivedDays;
        return days > 0 ? _clock.GetUtcNow().AddDays(-days) : null;
    }

    /// <summary>Unread items older than the setting become read; after every pass, and at once when the setting changes.</summary>
    private int MarkOldRead()
    {
        var days = _settings.Current.MarkReadAfterDays;
        return days > 0 ? _store.MarkReadBefore(_clock.GetUtcNow().AddDays(-days)) : 0;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Schedule();
        if (Interlocked.Exchange(ref _markReadAfterDays, settings.MarkReadAfterDays) != settings.MarkReadAfterDays)
            MarkOldRead();
    }

    /// <summary>(Re)creates the periodic timer when the interval changes; 0 minutes means no timer.</summary>
    private void Schedule()
    {
        lock (_gate)
        {
            var minutes = _settings.Current.RefreshIntervalMinutes;
            if (!_started || minutes == _scheduledMinutes)
                return;
            _timer?.Dispose();
            _timer = null;
            _scheduledMinutes = minutes;
            if (minutes <= 0)
                return;
            var period = TimeSpan.FromMinutes(minutes);
            _timer = _clock.CreateTimer(_ => _ = RefreshAllAsync(), null, period, period);
        }
    }
}
