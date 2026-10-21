using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RSSmote.Core.Parsing;

namespace RSSmote.Core.Discovery;

public sealed record FoundFeed(Uri Url, ParsedFeed Feed);

public enum FeedError
{
    InvalidUrl,
    NotFound,
    Http,
}

public sealed class FeedException(FeedError error, string message, HttpStatusCode? status = null) : Exception(message)
{
    public FeedError Error { get; } = error;
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// Turns what the user typed into feeds (A10): the address itself if it is a feed, otherwise every
/// <c>&lt;link rel="alternate"&gt;</c> it advertises, otherwise the first common feed path that answers.
/// </summary>
public sealed partial class FeedFinder(HttpClient http)
{
    private static readonly string[] FeedTypes =
        ["application/rss+xml", "application/atom+xml", "application/feed+json", "application/json", "text/xml", "application/xml"];

    private static readonly string[] CommonPaths = ["/feed", "/rss", "/feed.xml", "/rss.xml", "/atom.xml", "/index.xml", "/feed.json"];

    [GeneratedRegex(@"<link\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LinkTag();

    [GeneratedRegex("""([a-zA-Z-]+)\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s>]+))""")]
    private static partial Regex Attribute();

    /// <summary>Adds https:// when missing and maps feed:// style schemes; null when there is no host.</summary>
    public static Uri? Normalize(string input)
    {
        var s = input.Trim();
        if (s.StartsWith("feed:https:", StringComparison.OrdinalIgnoreCase) || s.StartsWith("feed:http:", StringComparison.OrdinalIgnoreCase))
            s = s[5..];
        else if (s.StartsWith("feed://", StringComparison.OrdinalIgnoreCase))
            s = "https://" + s[7..];
        else if (s.StartsWith("feeds://", StringComparison.OrdinalIgnoreCase))
            s = "https://" + s[8..];
        if (!s.Contains("://", StringComparison.Ordinal))
            s = "https://" + s;
        return Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && uri.Host.Length > 0 ? uri : null;
    }

    public static IReadOnlyList<Uri> AlternateLinks(string html, Uri baseUri)
    {
        var result = new List<Uri>();
        foreach (Match tag in LinkTag().Matches(html))
        {
            var attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match a in Attribute().Matches(tag.Value))
                attrs[a.Groups[1].Value] = a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value;

            if (attrs.TryGetValue("rel", out var rel) && rel.Contains("alternate", StringComparison.OrdinalIgnoreCase)
                && attrs.TryGetValue("type", out var type) && FeedTypes.Contains(type.ToLowerInvariant())
                && attrs.TryGetValue("href", out var href)
                && Uri.TryCreate(baseUri, HtmlContent.DecodeEntities(href), out var url))
                result.Add(url);
        }
        return result;
    }

    public async Task<FoundFeed> FindAsync(string input, CancellationToken ct = default) =>
        (await FindAllAsync(input, ct).ConfigureAwait(false))[0];

    /// <summary>Every feed the address offers; never empty (throws <see cref="FeedException"/> instead).</summary>
    public async Task<IReadOnlyList<FoundFeed>> FindAllAsync(string input, CancellationToken ct = default)
    {
        var url = Normalize(input) ?? throw new FeedException(FeedError.InvalidUrl, "The address is not a valid URL.");
        var (data, finalUrl) = await GetAsync(url, ct).ConfigureAwait(false);
        if (FeedParser.Parse(data, finalUrl) is { } direct)
            return [new FoundFeed(finalUrl, direct)];

        var html = Encoding.UTF8.GetString(data);
        var results = new List<FoundFeed>();
        foreach (var candidate in AlternateLinks(html, finalUrl).DistinctBy(u => u.AbsoluteUri))
        {
            if (await TryFeedAsync(candidate, ct).ConfigureAwait(false) is { } found)
                results.Add(found);
        }
        if (results.Count == 0)
        {
            foreach (var path in CommonPaths)
            {
                if (await TryFeedAsync(new Uri(finalUrl, path), ct).ConfigureAwait(false) is { } found)
                {
                    results.Add(found);
                    break;
                }
            }
        }
        return results.Count > 0 ? results : throw new FeedException(FeedError.NotFound, "No feed could be found at this address.");
    }

    private async Task<FoundFeed?> TryFeedAsync(Uri url, CancellationToken ct)
    {
        try
        {
            var (data, finalUrl) = await GetAsync(url, ct).ConfigureAwait(false);
            return FeedParser.Parse(data, finalUrl) is { } feed ? new FoundFeed(finalUrl, feed) : null;
        }
        catch (Exception e) when (e is HttpRequestException or FeedException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>GET with the final URL after redirects; non-success statuses throw <see cref="FeedException"/>.</summary>
    public async Task<(byte[] Data, Uri FinalUrl)> GetAsync(Uri url, CancellationToken ct = default)
    {
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new FeedException(FeedError.Http, $"The server responded with HTTP {(int)response.StatusCode}.", response.StatusCode);
        var data = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        return (data, response.RequestMessage?.RequestUri ?? url);
    }

    /// <summary>
    /// The handler feeds need: gzip, deflate and brotli are decoded, because some servers compress whatever the
    /// request asks for.
    /// </summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AutomaticDecompression = DecompressionMethods.All,
        PooledConnectionLifetime = TimeSpan.FromMinutes(10),
    };

    /// <summary>The HttpClient settings feeds expect: decompression, a user agent and a feed-first Accept header.</summary>
    public static HttpClient CreateHttpClient(HttpMessageHandler? handler = null)
    {
        var client = new HttpClient(handler ?? CreateHandler());
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RSSmote/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd(
            "application/rss+xml, application/atom+xml, application/feed+json, application/json, text/xml, text/html;q=0.8, */*;q=0.5");
        return client;
    }
}
