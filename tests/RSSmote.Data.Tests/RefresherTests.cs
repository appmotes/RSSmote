using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;

namespace RSSmote.Data.Tests;

public sealed class RefresherTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-refresh-" + Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeHttp _http = new();
    private readonly Store _store;
    private readonly SettingsStore _settings;
    private readonly Refresher _refresher;

    public RefresherTests()
    {
        _store = new Store(Path.Combine(_folder, "rssmote.sqlite"), _clock);
        _settings = new SettingsStore(Path.Combine(_folder, "settings.json"));
        _refresher = new Refresher(_store, new FeedFinder(new HttpClient(_http)), _settings, _clock);
    }

    public void Dispose()
    {
        _refresher.Dispose();
        _store.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        public ConcurrentDictionary<string, string> Pages { get; } = new();
        public ConcurrentQueue<string> Requests { get; } = new();
        public TaskCompletionSource? Gate { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Enqueue(request.RequestUri!.AbsoluteUri);
            if (Gate is { } gate)
                await gate.Task.WaitAsync(ct);
            var found = Pages.TryGetValue(request.RequestUri.AbsoluteUri, out var body);
            return new HttpResponseMessage(found ? HttpStatusCode.OK : HttpStatusCode.NotFound)
            {
                Content = new StringContent(body ?? "", Encoding.UTF8),
                RequestMessage = request,
            };
        }
    }

    private static string Rss(string title, params (string Guid, string Date)[] items) =>
        $"""
        <rss version="2.0"><channel><title>{title}</title><link>https://site.example/</link>
        {string.Concat(items.Select(i => $"<item><title>{i.Guid}</title><guid>{i.Guid}</guid><pubDate>{i.Date}</pubDate></item>"))}
        </channel></rss>
        """;

    private const string Today = "Mon, 19 Oct 2026 10:00:00 GMT";

    [Fact]
    public async Task Refresh_stores_items_fills_feed_details_and_stamps_the_time()
    {
        _http.Pages["https://a.example/feed"] = Rss("Alpha", ("1", Today), ("2", Today));
        var feed = _store.AddFeed("https://a.example/feed", "https://a.example/feed", null, null);

        var summary = await _refresher.RefreshAllAsync();

        Assert.Equal(new RefreshSummary(1, 2, 0, 0), summary);
        var updated = _store.Feed(feed.Id)!;
        Assert.Equal("Alpha", updated.Title);
        Assert.Equal("https://site.example/", updated.SiteUrl);
        Assert.Equal(_clock.GetUtcNow(), _settings.Current.LastRefresh);
        Assert.Equal(2, _store.Count(new ArticleStream.All(), StreamFilter.Unread));
    }

    [Fact]
    public async Task A_failing_feed_records_its_error_and_a_later_success_clears_it()
    {
        var feed = _store.AddFeed("https://gone.example/feed", "Gone", null, null);

        var summary = await _refresher.RefreshAllAsync();
        Assert.Equal(1, summary.Failed);
        Assert.Equal("The server responded with HTTP 404.", _store.Feed(feed.Id)!.LastError);

        _http.Pages["https://gone.example/feed"] = Rss("Gone", ("1", Today));
        await _refresher.RefreshAllAsync();
        Assert.Null(_store.Feed(feed.Id)!.LastError);
        Assert.Equal("Gone", _store.Feed(feed.Id)!.Title);
    }

    [Fact]
    public async Task A_page_that_is_not_a_feed_is_an_error()
    {
        _http.Pages["https://html.example/feed"] = "<html></html>";
        var feed = _store.AddFeed("https://html.example/feed", "H", null, null);
        await _refresher.RefreshAllAsync();
        Assert.Equal("The address no longer returns a feed.", _store.Feed(feed.Id)!.LastError);
    }

    [Fact]
    public async Task A_second_request_joins_the_running_refresh()
    {
        _http.Pages["https://a.example/feed"] = Rss("A", ("1", Today));
        _store.AddFeed("https://a.example/feed", "A", null, null);
        _http.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = _refresher.RefreshAllAsync();
        var second = _refresher.RefreshAllAsync();
        Assert.Same(first, second);
        Assert.True(_refresher.IsRefreshing);

        _http.Gate.SetResult();
        await first;
        Assert.False(_refresher.IsRefreshing);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task Retention_skips_old_new_items_and_prunes_old_read_ones()
    {
        _settings.Update(s => s.KeepArchivedDays = 30);
        _http.Pages["https://a.example/feed"] = Rss("A", ("fresh", Today), ("ancient", "Mon, 01 Jan 2024 10:00:00 GMT"));
        var feed = _store.AddFeed("https://a.example/feed", "A", null, null);

        var summary = await _refresher.RefreshAllAsync();
        Assert.Equal(1, summary.NewItems);

        var id = _store.Items(new ArticleStream.All(), StreamFilter.All).Single().Id;
        _store.SetUnread([id], false);
        _clock.Advance(TimeSpan.FromDays(31));
        summary = await _refresher.RefreshAllAsync();
        Assert.Equal(1, summary.Pruned);
        Assert.Equal(0, _store.Count(new ArticleStream.OfFeed(feed.Id), StreamFilter.All));
    }

    [Fact]
    public void AddFeed_stores_the_feed_and_its_items()
    {
        var parsed = Core.Parsing.FeedParser.Parse(Rss("Beta", ("1", Today)))!;
        var feed = _refresher.AddFeed(new FoundFeed(new Uri("https://b.example/feed"), parsed), folderId: null);
        Assert.Equal("Beta", feed.Title);
        Assert.Equal(1, _store.Count(new ArticleStream.OfFeed(feed.Id), StreamFilter.Unread));
    }

    [Fact]
    public async Task The_timer_follows_the_interval_setting()
    {
        _http.Pages["https://a.example/feed"] = Rss("A", ("1", Today));
        _store.AddFeed("https://a.example/feed", "A", null, null);
        _settings.Update(s => s.RefreshIntervalMinutes = 15);

        _refresher.Start();
        await Settled(expectedRequests: 1);

        _clock.Advance(TimeSpan.FromMinutes(14));
        Assert.Single(_http.Requests);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await Settled(expectedRequests: 2);

        _settings.Update(s => s.RefreshIntervalMinutes = 0);
        _clock.Advance(TimeSpan.FromHours(3));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Assert.Equal(2, _http.Requests.Count);
    }

    /// <summary>Waits (real time, bounded) until the given number of requests were made and no refresh is running.</summary>
    private async Task Settled(int expectedRequests)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (_http.Requests.Count < expectedRequests || _refresher.IsRefreshing)
        {
            Assert.True(DateTime.UtcNow < until, $"Timed out with {_http.Requests.Count} requests.");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
        Assert.Equal(expectedRequests, _http.Requests.Count);
    }
}
