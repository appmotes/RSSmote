using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RSSmote.Core.Api;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Data;

namespace RSSmote.Server.Tests;

public sealed class ReaderTests : IDisposable
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Key = "correct horse battery";
    private const string Api = "/api/reader/reader/api/0";

    private static readonly DateTimeOffset Noon = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-reader-" + Guid.NewGuid().ToString("N"));
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

    public void Dispose()
    {
        foreach (var host in _hosts)
        {
            host.Dispose();
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static async Task<HttpResponseMessage> Login(HttpClient client, string key, string prefix = "/api/reader") =>
        await client.PostAsync($"{prefix}/accounts/ClientLogin", Form(("Email", "me"), ("Passwd", key)), Ct);

    private static async Task<string> Token(WebApplicationFactory<Program> host, string prefix = "/api/reader")
    {
        var response = await Login(host.CreateClient(), Key, prefix);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(Ct);
        return body.Split('\n').Single(l => l.StartsWith("Auth=", StringComparison.Ordinal))["Auth=".Length..];
    }

    private static HttpClient Reader(WebApplicationFactory<Program> host, string token)
    {
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(ReaderIds.AuthScheme, "auth=" + token);
        return client;
    }

    private static async Task<HttpClient> SignedIn(WebApplicationFactory<Program> host) => Reader(host, await Token(host));

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] fields) =>
        new(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value)));

    private static async Task<JsonElement> Json(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement;
    }

    private static string[] Ids(JsonElement answer) =>
        [.. answer.GetProperty("itemRefs").EnumerateArray().Select(r => r.GetProperty("id").GetString()!)];

    /// <summary>A folder "Tech" with feed A (items a1 at noon, a2 an hour later), and feed B with no folder (b1 at 11).</summary>
    private static (Store Store, Feed A, Feed B, Item A1, Item A2, Item B1) Seed(WebApplicationFactory<Program> host)
    {
        var store = host.Services.GetRequiredService<Store>();
        var tech = store.AddFolder("Tech");
        var a = store.AddFeed("https://a.example/feed", "A", "https://a.example", tech.Id);
        var b = store.AddFeed("https://b.example/feed", "B", null, null);
        store.Upsert(a.Id, [
            new ParsedItem("a1", "A one", "https://a.example/1", "Ann", "<p>First <script>x()</script></p>", "First", null, Noon),
            new ParsedItem("a2", "A two", "https://a.example/2", null, "", "Second", null, Noon.AddHours(1)),
        ]);
        store.Upsert(b.Id, [new ParsedItem("b1", "B one", null, null, "<p>B</p>", "B", null, Noon.AddHours(-1))]);
        var items = store.Items(new ArticleStream.All(), StreamFilter.All).ToDictionary(i => i.Guid);
        return (store, a, b, items["a1"], items["a2"], items["b1"]);
    }

    [Theory]
    [InlineData("123", 123L)]
    [InlineData("000000000000007b", 123L)]
    [InlineData("0000000000000123", 0x123L)]
    [InlineData("7B", 123L)]
    [InlineData("", null)]
    [InlineData("not an id", null)]
    [InlineData("00000000000000001", null)]
    public void Item_ids_are_read_as_decimal_only_without_a_leading_zero(string value, long? expected) =>
        Assert.Equal(expected, ReaderIds.ParseItem(value));

    [Fact]
    public void The_long_item_id_reads_back() => Assert.Equal(123L, ReaderIds.ParseItem(ReaderIds.LongItem(123)));

    [Fact]
    public async Task The_key_signs_in_under_both_prefixes()
    {
        var host = Host();
        foreach (var prefix in ReaderApi.Prefixes)
        {
            var client = Reader(host, await Token(host, prefix));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{prefix}/reader/api/0/user-info", Ct)).StatusCode);
        }
    }

    [Fact]
    public async Task A_wrong_key_or_no_token_is_refused()
    {
        var host = Host();
        var login = await Login(host.CreateClient(), "not the key at all");
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().GetAsync($"{Api}/subscription/list", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Reader(host, "made-up").GetAsync($"{Api}/subscription/list", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_token_does_not_open_the_browser_api_nor_a_cookie_the_reader_api()
    {
        var host = Host();
        var reader = await SignedIn(host);
        Assert.Equal(HttpStatusCode.Unauthorized, (await reader.GetAsync("/api/library", Ct)).StatusCode);

        var browser = host.CreateClient();
        browser.DefaultRequestHeaders.Add(ApiNames.ClientHeader, "test");
        await browser.PostAsJsonAsync("/api/login", new LoginRequest(Key), ApiJsonContext.Default.LoginRequest, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, (await browser.GetAsync($"{Api}/subscription/list", Ct)).StatusCode);
    }

    [Fact]
    public async Task A_new_key_signs_every_token_out()
    {
        var token = await Token(Host());
        var other = Reader(Host("a completely different key"), token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync($"{Api}/user-info", Ct)).StatusCode);
    }

    [Fact]
    public async Task Subscriptions_and_tags_show_folders_as_labels()
    {
        var host = Host();
        var (_, a, b, _, _, _) = Seed(host);
        var client = await SignedIn(host);

        var subscriptions = (await Json(client, $"{Api}/subscription/list?output=json")).GetProperty("subscriptions").EnumerateArray().ToList();
        Assert.Equal([ReaderIds.Feed(a.Id), ReaderIds.Feed(b.Id)], subscriptions.Select(s => s.GetProperty("id").GetString()));
        var category = Assert.Single(subscriptions[0].GetProperty("categories").EnumerateArray());
        Assert.Equal(ReaderIds.Label("Tech"), category.GetProperty("id").GetString());
        Assert.Equal("Tech", category.GetProperty("label").GetString());
        Assert.Equal("https://a.example", subscriptions[0].GetProperty("htmlUrl").GetString());
        Assert.Equal(a.FaviconUrl, subscriptions[0].GetProperty("iconUrl").GetString());
        Assert.Empty(subscriptions[1].GetProperty("categories").EnumerateArray());

        var tags = (await Json(client, $"{Api}/tag/list?output=json")).GetProperty("tags").EnumerateArray()
            .Select(t => t.GetProperty("id").GetString()).ToList();
        Assert.Equal([ReaderIds.Starred, ReaderIds.Label("Tech")], tags);
    }

    [Fact]
    public async Task Unread_counts_cover_feeds_labels_and_everything()
    {
        var host = Host();
        var (store, a, b, a1, _, _) = Seed(host);
        store.SetUnread([a1.Id], false);
        var client = await SignedIn(host);

        var answer = await Json(client, $"{Api}/unread-count?output=json");
        var counts = answer.GetProperty("unreadcounts").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetString()!, c => c.GetProperty("count").GetInt32());
        Assert.Equal(1, counts[ReaderIds.Feed(a.Id)]);
        Assert.Equal(1, counts[ReaderIds.Feed(b.Id)]);
        Assert.Equal(1, counts[ReaderIds.Label("Tech")]);
        Assert.Equal(2, counts[ReaderIds.ReadingList]);
        Assert.Equal(2, answer.GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task Item_ids_filter_by_stream_and_state_and_page_on()
    {
        var host = Host();
        var (store, a, _, a1, a2, b1) = Seed(host);
        store.SetUnread([a1.Id], false);
        store.SetStarred([b1.Id], true);
        var client = await SignedIn(host);
        string Id(Item i) => i.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string Url(string query) => $"{Api}/stream/items/ids?{query}";

        Assert.Equal([Id(a2), Id(a1), Id(b1)], Ids(await Json(client, Url($"s={ReaderIds.ReadingList}&n=100"))));
        Assert.Equal([Id(a2), Id(b1)], Ids(await Json(client, Url($"s={ReaderIds.ReadingList}&xt={ReaderIds.Read}&n=100"))));
        Assert.Equal([Id(b1)], Ids(await Json(client, Url($"s={ReaderIds.Starred}"))));
        Assert.Equal([Id(a2), Id(a1)], Ids(await Json(client, Url($"s={Uri.EscapeDataString(ReaderIds.Label("Tech"))}"))));
        Assert.Equal([Id(a1), Id(a2)], Ids(await Json(client, Url($"s={ReaderIds.Feed(a.Id)}&r=o"))));
        Assert.Equal([Id(a2)], Ids(await Json(client, Url($"s={ReaderIds.ReadingList}&ot={Noon.AddMinutes(30).ToUnixTimeSeconds()}"))));
        // A client may name its own user id in place of "-".
        Assert.Equal([Id(b1)], Ids(await Json(client, Url($"s={ReaderIds.Starred.Replace("user/-/", "user/1/", StringComparison.Ordinal)}"))));

        var first = await Json(client, Url($"s={ReaderIds.ReadingList}&n=2"));
        Assert.Equal([Id(a2), Id(a1)], Ids(first));
        var next = await Json(client, Url($"s={ReaderIds.ReadingList}&n=2&c={first.GetProperty("continuation").GetString()}"));
        Assert.Equal([Id(b1)], Ids(next));
        Assert.False(next.TryGetProperty("continuation", out _));
    }

    [Fact]
    public async Task Contents_take_every_id_form_and_carry_state_and_clean_html()
    {
        var host = Host();
        var (store, a, _, a1, a2, b1) = Seed(host);
        store.SetStarred([a1.Id], true);
        store.SetUnread([a1.Id], false);
        var client = await SignedIn(host);

        // The long form, a decimal number, and bare zero-padded hex.
        var response = await client.PostAsync($"{Api}/stream/items/contents", Form(
            ("i", ReaderIds.LongItem(a1.Id)),
            ("i", a2.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("i", b1.Id.ToString("x16", System.Globalization.CultureInfo.InvariantCulture))), Ct);
        var items = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("items").EnumerateArray().ToList();

        Assert.Equal(
            [ReaderIds.LongItem(a1.Id), ReaderIds.LongItem(a2.Id), ReaderIds.LongItem(b1.Id)],
            items.Select(i => i.GetProperty("id").GetString()));
        var first = items[0];
        Assert.Equal("A one", first.GetProperty("title").GetString());
        Assert.Equal("Ann", first.GetProperty("author").GetString());
        Assert.Equal(Noon.ToUnixTimeSeconds(), first.GetProperty("published").GetInt64());
        Assert.Equal((Noon.ToUnixTimeMilliseconds() * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture), first.GetProperty("timestampUsec").GetString());
        Assert.Equal("https://a.example/1", first.GetProperty("alternate")[0].GetProperty("href").GetString());
        Assert.Equal(ReaderIds.Feed(a.Id), first.GetProperty("origin").GetProperty("streamId").GetString());
        Assert.Equal(
            [ReaderIds.ReadingList, ReaderIds.Label("Tech"), ReaderIds.Read, ReaderIds.Starred],
            first.GetProperty("categories").EnumerateArray().Select(c => c.GetString()));
        var html = first.GetProperty("summary").GetProperty("content").GetString()!;
        Assert.Contains("First", html, StringComparison.Ordinal);
        Assert.DoesNotContain("script", html, StringComparison.Ordinal);
        // An item without content falls back to its summary.
        Assert.Contains("Second", items[1].GetProperty("summary").GetProperty("content").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Stream_contents_take_the_stream_from_the_path()
    {
        var host = Host();
        var (_, _, b, _, _, b1) = Seed(host);
        var client = await SignedIn(host);

        var answer = await Json(client, $"{Api}/stream/contents/{Uri.EscapeDataString(ReaderIds.Feed(b.Id))}?n=10");
        Assert.Equal(ReaderIds.Feed(b.Id), answer.GetProperty("id").GetString());
        var item = Assert.Single(answer.GetProperty("items").EnumerateArray());
        Assert.Equal(ReaderIds.LongItem(b1.Id), item.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Edit_tag_sets_read_and_starred_without_the_client_header()
    {
        var host = Host();
        var (store, _, _, a1, a2, _) = Seed(host);
        var client = await SignedIn(host);
        async Task Edit(params (string, string)[] fields)
        {
            var response = await client.PostAsync($"{Api}/edit-tag", Form(fields), Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("OK", await response.Content.ReadAsStringAsync(Ct));
        }

        await Edit(("i", ReaderIds.LongItem(a1.Id)), ("i", ReaderIds.LongItem(a2.Id)), ("a", ReaderIds.Read), ("T", "x"));
        await Edit(("i", ReaderIds.LongItem(a1.Id)), ("a", ReaderIds.Starred));
        Assert.Equal([(a1.Id, false, true), (a2.Id, false, false)], store.Flags([a1.Id, a2.Id]).OrderBy(f => f.Id));

        await Edit(("i", ReaderIds.LongItem(a2.Id)), ("r", ReaderIds.Read));
        await Edit(("i", ReaderIds.LongItem(a1.Id)), ("r", ReaderIds.Starred));
        Assert.Equal([(a1.Id, false, false), (a2.Id, true, false)], store.Flags([a1.Id, a2.Id]).OrderBy(f => f.Id));
    }

    [Fact]
    public async Task Mark_all_as_read_stops_at_the_clients_time()
    {
        var host = Host();
        var (store, _, _, a1, a2, b1) = Seed(host);
        var client = await SignedIn(host);

        var ts = (Noon.AddMinutes(30).ToUnixTimeMilliseconds() * 1000).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var response = await client.PostAsync($"{Api}/mark-all-as-read", Form(("s", ReaderIds.Label("Tech")), ("ts", ts)), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // a1 is in the folder and before ts; a2 is after it; b1 is in no folder.
        Assert.Equal([(a1.Id, false), (a2.Id, true), (b1.Id, true)],
            store.Flags([a1.Id, a2.Id, b1.Id]).OrderBy(f => f.Id).Select(f => (f.Id, f.Unread)));
    }

    [Fact]
    public async Task Feeds_and_folders_cannot_be_changed()
    {
        var host = Host();
        Seed(host);
        var client = await SignedIn(host);
        foreach (var write in new[] { "subscription/edit", "subscription/quickadd", "rename-tag", "disable-tag" })
        {
            var response = await client.PostAsync($"{Api}/{write}", Form(("s", "feed/1"), ("quickadd", "https://c.example")), Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        Assert.Equal(2, host.Services.GetRequiredService<Store>().Feeds().Count);
    }

    [Fact]
    public async Task A_reader_write_reaches_the_browser_apps_event_stream()
    {
        var host = Host();
        var (_, _, _, a1, _, _) = Seed(host);
        var reader = await SignedIn(host);
        var browser = host.CreateClient();
        browser.DefaultRequestHeaders.Add(ApiNames.ClientHeader, "test");
        await browser.PostAsJsonAsync("/api/login", new LoginRequest(Key), ApiJsonContext.Default.LoginRequest, Ct);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var events = await browser.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        using var stream = new StreamReader(await events.Content.ReadAsStreamAsync(timeout.Token));
        await reader.PostAsync($"{Api}/edit-tag", Form(("i", ReaderIds.LongItem(a1.Id)), ("a", ReaderIds.Read)), timeout.Token);

        string? line;
        while ((line = await stream.ReadLineAsync(timeout.Token)) != "event: flags")
        {
            Assert.NotNull(line);
        }

        var data = (await stream.ReadLineAsync(timeout.Token))!["data: ".Length..];
        var flags = JsonSerializer.Deserialize(data, ApiJsonContext.Default.FlagsEvent)!;
        Assert.Null(flags.Origin);
        Assert.Equal(new ItemFlags(a1.Id, false, false), Assert.Single(flags.Items));
    }
}
