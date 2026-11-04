using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RSSmote.Client;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Data;

namespace RSSmote.Server.Tests;

/// <summary>Two devices, each a <see cref="RemoteSession"/>, against one server.</summary>
public sealed class SyncTests : IAsyncLifetime
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Key = "correct horse battery";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-sync-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _host;
    private readonly List<RemoteSession> _sessions = [];
    private long _itemId;

    public SyncTests() => _host = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
    {
        b.UseSetting("RSSMOTE_KEY", Key);
        b.UseSetting("RSSMOTE_DATA_DIR", _folder);
    });

    private Store Store => _host.Services.GetRequiredService<Store>();

    public sealed class MemoryStorage : IDeviceStorage
    {
        private readonly Dictionary<string, string> _values = [];
        public string? Read(string key) => _values.GetValueOrDefault(key);

        public bool Write(string key, string value)
        {
            _values[key] = value;
            return true;
        }
    }

    public ValueTask InitializeAsync()
    {
        var feed = Store.AddFeed("https://a.example/feed", "A", "https://a.example", null);
        Store.Upsert(feed.Id, [new ParsedItem("1", "One", "https://a.example/1", null, "<p>Body</p>", "Body", null, DateTimeOffset.UtcNow)]);
        _itemId = Store.Items(new ArticleStream.All(), StreamFilter.All).Single().Id;
        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _host.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
        return ValueTask.CompletedTask;
    }

    private async Task<RemoteSession> Device()
    {
        var api = new ApiClient(_host.CreateClient());
        Assert.True(await api.SignInAsync(Key));
        var session = new RemoteSession(api, new RemoteSettings(api, new MemoryStorage()));
        _sessions.Add(session);
        await session.StartAsync();
        return session;
    }

    private static async Task Eventually(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < until, "Timed out waiting for the other device.");
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task The_mirror_answers_without_content_and_loads_it_on_request()
    {
        var device = await Device();
        var item = device.Library.Item(_itemId)!;
        Assert.Equal("", item.Content);
        Assert.Equal("A", item.FeedTitle);
        Assert.Equal(1, device.Library.Counts(StreamFilter.Unread)[item.FeedId]);
        Assert.Equal("<p>Body</p>", (await device.Library.LoadItemAsync(_itemId))!.Content);
    }

    [Fact]
    public async Task Read_on_one_device_is_read_on_the_other()
    {
        var phone = await Device();
        var desktop = await Device();

        phone.Library.SetUnread([_itemId], false);
        Assert.False(phone.Library.Item(_itemId)!.Unread);

        await Eventually(() => !desktop.Library.Item(_itemId)!.Unread);
        await Eventually(() => !Store.Item(_itemId)!.Unread);
    }

    [Fact]
    public async Task A_folder_added_on_one_device_shows_on_the_other()
    {
        var phone = await Device();
        var desktop = await Device();

        var folder = await phone.Library.AddFolderAsync("News");
        Assert.Contains(phone.Library.Folders(), f => f.Id == folder.Id);
        await Eventually(() => desktop.Library.FolderNamed("News") is not null);
    }

    [Fact]
    public async Task Library_settings_travel_and_device_settings_stay()
    {
        var phone = await Device();
        var desktop = await Device();

        phone.Settings.Update(s =>
        {
            s.RefreshIntervalMinutes = 60;
            s.Filter = StreamFilter.All;
        });

        await Eventually(() => desktop.Settings.Current.RefreshIntervalMinutes == 60);
        Assert.Equal(StreamFilter.Unread, desktop.Settings.Current.Filter);
        Assert.Equal(StreamFilter.All, phone.Settings.Current.Filter);
    }

    [Fact]
    public async Task A_find_that_fails_says_why()
    {
        var device = await Device();
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => device.Finder.FindAllAsync("not a web address at all", Ct));
        Assert.Equal("The address is not a valid URL.", error.Message);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var device = await Device();
        var signedOut = false;
        device.Api.SignedOut += (_, _) => signedOut = true;
        await device.Api.SignOutAsync(Ct);
        await Assert.ThrowsAsync<SignedOutException>(() => device.Library.ReloadAsync(Ct));
        Assert.True(signedOut);
    }
}
