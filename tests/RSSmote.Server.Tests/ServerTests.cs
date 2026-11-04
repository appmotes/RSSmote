using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RSSmote.Core.Api;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Data;

namespace RSSmote.Server.Tests;

public sealed class ServerTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Key = "correct horse battery";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-server-" + Guid.NewGuid().ToString("N"));
    private readonly List<IDisposable> _hosts = [];

    private WebApplicationFactory<Program> Host(string key = Key)
    {
        var host = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("RSSMOTE_KEY", key);
            b.UseSetting("RSSMOTE_DATA_DIR", _folder);
        });
        _hosts.Add(host);
        return host;
    }

    private static HttpClient Client(WebApplicationFactory<Program> host)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Add(ApiNames.ClientHeader, "test");
        return client;
    }

    private static async Task<HttpClient> SignedIn(WebApplicationFactory<Program> host)
    {
        var client = Client(host);
        var login = await client.PostAsJsonAsync("/api/login", new LoginRequest(Key), ApiJsonContext.Default.LoginRequest);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        return client;
    }

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public async Task Library_needs_a_sign_in()
    {
        var response = await Client(Host()).GetAsync("/api/library", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_key_is_refused()
    {
        var client = Client(Host());
        var login = await client.PostAsJsonAsync("/api/login", new LoginRequest("not the key at all"), ApiJsonContext.Default.LoginRequest, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/session", Ct)).StatusCode);
    }

    [Fact]
    public async Task The_key_signs_in_and_the_library_answers()
    {
        var client = await SignedIn(Host());
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync("/api/session", Ct)).StatusCode);
        var snapshot = await client.GetFromJsonAsync("/api/library", ApiJsonContext.Default.LibrarySnapshot, Ct);
        Assert.NotNull(snapshot);
        Assert.Empty(snapshot.Feeds);
    }

    [Fact]
    public async Task Sign_in_attempts_are_limited()
    {
        var client = Client(Host());
        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            codes.Add((await client.PostAsJsonAsync("/api/login", new LoginRequest("wrong key, try again"), ApiJsonContext.Default.LoginRequest, Ct)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, codes[^1]);
        Assert.All(codes[..5], c => Assert.Equal(HttpStatusCode.Unauthorized, c));
    }

    [Fact]
    public async Task A_new_key_signs_every_device_out()
    {
        var client = await SignedIn(Host());
        var cookie = (await client.PostAsJsonAsync("/api/login", new LoginRequest(Key), ApiJsonContext.Default.LoginRequest, Ct))
            .Headers.GetValues("Set-Cookie").First().Split(';')[0];

        var other = Host("a completely different key").CreateClient();
        other.DefaultRequestHeaders.Add("Cookie", cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/session", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_write_without_the_client_header_is_refused()
    {
        var host = Host();
        var client = await SignedIn(host);
        client.DefaultRequestHeaders.Remove(ApiNames.ClientHeader);
        var response = await client.PostAsJsonAsync("/api/items/unread", new FlagRequest([1], false), ApiJsonContext.Default.FlagRequest, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Marking_read_reaches_the_store_and_the_snapshot_carries_no_content()
    {
        var host = Host();
        var client = await SignedIn(host);
        var store = host.Services.GetRequiredService<Store>();
        var feed = store.AddFeed("https://a.example/feed", "A", null, null);
        store.Upsert(feed.Id, [new ParsedItem("1", "One", "https://a.example/1", null, "<p>Body</p>", "Body", null, DateTimeOffset.UtcNow)]);
        var item = store.Items(new ArticleStream.All(), StreamFilter.All).Single();

        var response = await client.PostAsJsonAsync("/api/items/unread", new FlagRequest([item.Id], false), ApiJsonContext.Default.FlagRequest, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var snapshot = await client.GetFromJsonAsync("/api/library", ApiJsonContext.Default.LibrarySnapshot, Ct);
        var mirrored = Assert.Single(snapshot!.Items);
        Assert.False(mirrored.Unread);
        Assert.Equal("", mirrored.Content);
        var full = await client.GetFromJsonAsync($"/api/items/{item.Id}", ApiJsonContext.Default.Item, Ct);
        Assert.Equal("<p>Body</p>", full!.Content);
    }

    [Fact]
    public async Task Mark_all_read_takes_a_stream()
    {
        var host = Host();
        var client = await SignedIn(host);
        var store = host.Services.GetRequiredService<Store>();
        var feed = store.AddFeed("https://a.example/feed", "A", null, null);
        store.Upsert(feed.Id, [new ParsedItem("1", "One", null, null, "x", "x", null, DateTimeOffset.UtcNow)]);

        var response = await client.PostAsJsonAsync("/api/items/read-all", new ArticleStream.OfFeed(feed.Id), ApiJsonContext.Default.ArticleStream, Ct);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, store.Count(new ArticleStream.All(), StreamFilter.Unread));
    }

    [Fact]
    public async Task Events_carry_flags_with_the_writer()
    {
        var host = Host();
        var client = await SignedIn(host);
        var store = host.Services.GetRequiredService<Store>();
        var feed = store.AddFeed("https://a.example/feed", "A", null, null);
        store.Upsert(feed.Id, [new ParsedItem("1", "One", null, null, "x", "x", null, DateTimeOffset.UtcNow)]);
        var id = store.Items(new ArticleStream.All(), StreamFilter.All).Single().Id;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var events = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(timeout.Token));
        await client.PostAsJsonAsync("/api/items/starred", new FlagRequest([id], true), ApiJsonContext.Default.FlagRequest, timeout.Token);

        string? line;
        while ((line = await reader.ReadLineAsync(timeout.Token)) != "event: flags")
        {
            Assert.NotNull(line);
        }

        var data = (await reader.ReadLineAsync(timeout.Token))!["data: ".Length..];
        var flags = System.Text.Json.JsonSerializer.Deserialize(data, ApiJsonContext.Default.FlagsEvent)!;
        Assert.Equal("test", flags.Origin);
        Assert.Equal(new ItemFlags(id, true, true), Assert.Single(flags.Items));
    }

    [Fact]
    public async Task Settings_keep_the_server_stamp()
    {
        var client = await SignedIn(Host());
        var sent = new LibrarySettings(60, 10, 2, DateTimeOffset.UnixEpoch);
        var response = await client.PutAsJsonAsync("/api/settings", sent, ApiJsonContext.Default.LibrarySettings, Ct);
        var saved = await response.Content.ReadFromJsonAsync(ApiJsonContext.Default.LibrarySettings, Ct);
        Assert.Equal(60, saved!.RefreshIntervalMinutes);
        Assert.NotEqual(DateTimeOffset.UnixEpoch, saved.LastRefresh);
    }

    [Fact]
    public async Task The_image_proxy_takes_only_web_addresses()
    {
        var client = await SignedIn(Host());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/image?url=file:///etc/passwd", Ct)).StatusCode);
    }

    [Fact]
    public void A_short_key_stops_the_server()
    {
        var host = Host("short");
        Assert.ThrowsAny<Exception>(() => host.CreateClient());
    }
}
