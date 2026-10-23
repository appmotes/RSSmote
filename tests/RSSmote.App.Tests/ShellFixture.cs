using Avalonia.Media.Imaging;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Time.Testing;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Data;

namespace RSSmote.App.Tests;

/// <summary>A seeded library and a shell view-model over it, with no network and an immediate UI dispatcher.</summary>
public sealed class ShellFixture : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-shell-" + Guid.NewGuid().ToString("N"));

    public FakeTimeProvider Clock { get; } = new(Now);
    public Store Store { get; }
    public SettingsStore Settings { get; }
    public RecordingLauncher Launcher { get; } = new();
    public FakeHttp Http { get; } = new();
    public FakeFiles Files { get; } = new();
    public NoImages Images { get; } = new();
    public ShellViewModel Shell { get; }
    public Folder Apple { get; }
    public Feed Insider { get; }
    public Feed Daring { get; }
    public Feed Wired { get; }
    public Feed Quiet { get; }

    public sealed class ImmediateUi : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    public sealed class NoImages : IImageLoader
    {
        public Task<Bitmap?> LoadAsync(string url, int decodeWidth) => Task.FromResult<Bitmap?>(null);
        public List<string> Forgotten { get; } = [];
        public void Forget(string url) => Forgotten.Add(url);
    }

    /// <summary>Serves <see cref="Pages"/> by absolute URL; anything else is a 404.</summary>
    public sealed class FakeHttp : HttpMessageHandler
    {
        public Dictionary<string, string> Pages { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var found = Pages.TryGetValue(request.RequestUri!.AbsoluteUri, out var body);
            return Task.FromResult(new HttpResponseMessage(found ? System.Net.HttpStatusCode.OK : System.Net.HttpStatusCode.NotFound)
            {
                Content = new StringContent(body ?? "", System.Text.Encoding.UTF8),
                RequestMessage = request,
            });
        }
    }

    public sealed class FakeFiles : IFilePicker
    {
        public string? NextOpen { get; set; }
        public string? Saved { get; private set; }
        public Task<string?> OpenTextAsync(string title, string fileTypeName, IReadOnlyList<string> patterns) => Task.FromResult(NextOpen);

        public Task<bool> SaveTextAsync(string title, string suggestedName, string text)
        {
            Saved = text;
            return Task.FromResult(true);
        }
    }

    public sealed class RecordingLauncher : ILauncher
    {
        public List<(Uri Url, bool Background)> Opened { get; } = [];
        public void Open(Uri url, bool inBackground) => Opened.Add((url, inBackground));
    }

    /// <summary>Runs posted actions only when asked, as the UI thread does after the current one returns.</summary>
    public sealed class QueuedUi : IUiDispatcher
    {
        private readonly Queue<Action> _queue = new();
        public void Post(Action action) => _queue.Enqueue(action);

        public void RunAll()
        {
            while (_queue.TryDequeue(out var action))
                action();
        }
    }

    public ShellFixture(Action<AppSettings>? settings = null, SidebarLayout layout = SidebarLayout.Desktop, IUiDispatcher? ui = null)
    {
        Store = new Store(Path.Combine(_folder, "rssmote.sqlite"), Clock);
        Settings = new SettingsStore(Path.Combine(_folder, "settings.json"));
        if (settings is not null)
            Settings.Update(settings);

        Apple = Store.AddFolder("Apple");
        Store.AddFolder("Tech");
        Insider = Store.AddFeed("https://insider.example/rss", "AppleInsider News", "https://insider.example", Apple.Id);
        Daring = Store.AddFeed("https://df.example/rss", "Daring Fireball", "https://df.example", Apple.Id);
        Wired = Store.AddFeed("https://wired.example/rss", "WIRED", "https://wired.example", null);
        Quiet = Store.AddFeed("https://quiet.example/rss", "Quiet Feed", null, null);

        Store.Upsert(Insider.Id, [
            Item("i1", "Two Apple stores looted during French protests", Now.AddHours(-1), "As the French protests now involving about 600,000 protesters escalate."),
            Item("i2", "AI audio firm Samplab acquired by Apple to boost Logic Pro", Now.AddDays(-1), "Apple has acquired AI audio editing firm Samplab."),
        ]);
        Store.Upsert(Daring.Id, [Item("d1", "Tom the Dancing Bug’s Inner Hive", Now.AddHours(-3), "Ruben Bolling’s long-running comic strip.")]);
        Store.Upsert(Wired.Id, [
            Item("w1", "Nathan Fielder’s Surprise Call From Elizabeth Holmes", Now.AddMinutes(-30), "The codirector talks exclusively about the call.", image: "https://wired.example/a.jpg"),
        ]);
        Store.Upsert(Quiet.Id, [Item("q1", "Already read", Now.AddDays(-2), "Nothing new here.")]);
        var read = Store.Items(new ArticleStream.OfFeed(Quiet.Id), StreamFilter.All).Select(i => i.Id).ToList();
        Store.SetUnread(read, false);

        Shell = new ShellViewModel(Store, Settings, refresher: null, new FeedFinder(FeedFinder.CreateHttpClient(Http)), Files,
            Images, Launcher, ui ?? new ImmediateUi(), Clock, layout);
    }

    private static ParsedItem Item(string guid, string title, DateTimeOffset published, string summary, string? image = null) =>
        new(guid, title, $"https://example.com/{guid}", "Author", $"<p>{summary}</p><p>More text.</p>", summary, image, published);

    public SidebarRowViewModel Row(string title) => Shell.Sidebar.Rows.Single(r => r.Title == title);

    public void Dispose()
    {
        Shell.Dispose();
        Store.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }
}
