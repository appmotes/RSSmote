using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace RSSmote.Core.Parsing;

public sealed record ParsedItem(
    string Guid,
    string Title,
    string? Link,
    string? Author,
    string Content,
    string Summary,
    string? ImageUrl,
    DateTimeOffset? Published);

public sealed record ParsedFeed(string? Title, string? SiteUrl, IReadOnlyList<ParsedItem> Items);

/// <summary>
/// Parses RSS 2.0, RSS 1.0 (RDF), Atom and JSON Feed into one shape (A10). Returns null when the data is not a feed.
/// Elements are matched by local name, so namespace prefixes do not matter, except that <c>media:content</c> is an
/// image and never the article body.
/// </summary>
public static partial class FeedParser
{
    private const string MediaNs = "http://search.yahoo.com/mrss/";
    private const string XhtmlNs = "http://www.w3.org/1999/xhtml";

    [GeneratedRegex("&([A-Za-z][A-Za-z0-9]{1,31});")]
    private static partial Regex NamedEntity();

    private static readonly HashSet<string> XmlEntities = ["amp", "lt", "gt", "quot", "apos"];

    public static ParsedFeed? Parse(string text, Uri? baseUri = null) => Parse(Encoding.UTF8.GetBytes(text), baseUri);

    public static ParsedFeed? Parse(byte[] data, Uri? baseUri = null)
    {
        var first = data.FirstOrDefault(b => b is not (0x20 or 0x0A or 0x0D or 0x09 or 0xEF or 0xBB or 0xBF));
        return first == (byte)'{' ? ParseJson(data, baseUri) : ParseXml(data, baseUri);
    }

    // MARK: XML

    private static ParsedFeed? ParseXml(byte[] data, Uri? baseUri)
    {
        var doc = Load(data);
        if (doc is null)
        {
            // HTML entities such as &nbsp; are not XML; rewrite them as numeric references and try once more.
            var text = Encoding.UTF8.GetString(data);
            var fixedText = NamedEntity().Replace(text, m =>
            {
                if (XmlEntities.Contains(m.Groups[1].Value))
                    return m.Value;
                var decoded = WebUtility.HtmlDecode(m.Value);
                return decoded == m.Value ? "&amp;" + m.Groups[1].Value + ";" : string.Concat(decoded.Select(c => $"&#{(int)c};"));
            });
            if (fixedText != text)
                doc = Load(Encoding.UTF8.GetBytes(fixedText));
        }
        if (doc?.Root is not { } root)
            return null;

        var rootName = root.Name.LocalName.ToLowerInvariant();
        if (rootName is not ("rss" or "feed" or "rdf"))
            return null;

        var isAtom = rootName == "feed";
        var channel = isAtom ? root : root.Elements().FirstOrDefault(e => Is(e, "channel"));
        var feedTitle = Text(Child(channel, "title"));
        var siteUrl = isAtom ? AlternateHref(root) : Text(Child(channel, "link"));
        var resolveBase = Absolute(siteUrl) ?? baseUri;

        var items = root.Descendants()
            .Where(e => Is(e, "item") || Is(e, "entry"))
            .Select(e => ParseEntry(e, resolveBase))
            .ToList();

        return new ParsedFeed(Clean(feedTitle), siteUrl, items);
    }

    private static XDocument? Load(byte[] data)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
        try
        {
            using var stream = new MemoryStream(data);
            using var reader = XmlReader.Create(stream, settings);
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static ParsedItem ParseEntry(XElement entry, Uri? resolveBase)
    {
        string? link = null, image = null, author = null, content = null, summary = null;
        DateTimeOffset? published = null, updated = null;

        foreach (var e in entry.Elements())
        {
            switch (e.Name.LocalName.ToLowerInvariant())
            {
                case "link" when e.Attribute("href") is { } href:
                    var rel = (string?)e.Attribute("rel") ?? "alternate";
                    if (rel == "alternate")
                        link ??= href.Value;
                    else if (rel == "enclosure" && IsImage(e))
                        image ??= href.Value;
                    break;
                case "link":
                    link ??= Text(e);
                    break;
                case "pubdate" or "published" or "date" or "issued":
                    published ??= FeedDates.Parse(e.Value);
                    break;
                case "updated" or "modified":
                    updated ??= FeedDates.Parse(e.Value);
                    break;
                case "description" or "summary":
                    summary ??= Text(e);
                    break;
                case "encoded":
                    content = Text(e);
                    break;
                case "content" when e.Name.NamespaceName != MediaNs:
                    content = Body(e);
                    break;
                case "creator":
                    author ??= Text(e);
                    break;
                case "author":
                    author ??= Text(Child(e, "name")) ?? Text(e);
                    break;
                case "enclosure" when IsImage(e):
                    image ??= (string?)e.Attribute("url");
                    break;
            }
        }

        image ??= entry.Descendants()
            .Where(e => e.Name.NamespaceName == MediaNs && e.Name.LocalName is "thumbnail" or "content")
            .Where(e => e.Name.LocalName == "thumbnail" || (string?)e.Attribute("medium") == "image" || IsImage(e))
            .Select(e => (string?)e.Attribute("url"))
            .FirstOrDefault(u => !string.IsNullOrEmpty(u));

        var guid = Text(Child(entry, "guid")) ?? Text(Child(entry, "id"));
        return Finish(guid, Text(Child(entry, "title")), link, author, content, summary, image, published ?? updated, resolveBase);
    }

    private static ParsedItem Finish(string? guid, string? rawTitle, string? link, string? author, string? content,
                                     string? summary, string? image, DateTimeOffset? published, Uri? resolveBase)
    {
        var resolvedLink = Resolve(link, resolveBase);
        var body = string.IsNullOrWhiteSpace(content) ? summary ?? "" : content;
        var title = Clean(HtmlContent.DecodeEntities(HtmlContent.StripTags(rawTitle ?? "")).Trim());
        if (string.IsNullOrEmpty(title))
            title = HtmlContent.Excerpt(body, 80).TrimEnd('…');
        var id = string.IsNullOrWhiteSpace(guid)
            ? resolvedLink ?? $"{title}|{published?.ToUnixTimeSeconds() ?? 0}"
            : guid.Trim();

        return new ParsedItem(
            id,
            title,
            resolvedLink,
            Clean(author is null ? null : HtmlContent.DecodeEntities(author).Trim()),
            body,
            HtmlContent.Excerpt(string.IsNullOrWhiteSpace(summary) ? body : summary),
            Resolve(image ?? HtmlContent.FirstImage(body), resolveBase),
            published);
    }

    // MARK: JSON Feed (https://jsonfeed.org)

    private static ParsedFeed? ParseJson(byte[] data, Uri? baseUri)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(data);
        }
        catch (JsonException)
        {
            return null;
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Array)
                return null;

            var home = Str(root, "home_page_url");
            var resolveBase = Absolute(home) ?? baseUri;
            var parsed = new List<ParsedItem>();
            foreach (var raw in items.EnumerateArray())
            {
                if (raw.ValueKind != JsonValueKind.Object)
                    continue;
                var link = Str(raw, "url") ?? Str(raw, "external_url");
                var html = Str(raw, "content_html");
                var plain = Str(raw, "content_text");
                var content = html ?? (plain is null ? null
                    : "<p>" + HtmlContent.Escape(plain).Replace("\n\n", "</p><p>") + "</p>");
                var author = raw.TryGetProperty("authors", out var authors) && authors.ValueKind == JsonValueKind.Array
                    ? authors.EnumerateArray().Select(a => Str(a, "name")).FirstOrDefault(n => n is not null)
                    : raw.TryGetProperty("author", out var single) ? Str(single, "name") : null;
                var id = raw.TryGetProperty("id", out var idValue)
                    ? idValue.ValueKind == JsonValueKind.Number ? idValue.GetRawText() : idValue.GetString()
                    : null;
                var published = FeedDates.Parse(Str(raw, "date_published")) ?? FeedDates.Parse(Str(raw, "date_modified"));

                parsed.Add(Finish(id, Str(raw, "title"), link, author, content, Str(raw, "summary"),
                    Str(raw, "image") ?? Str(raw, "banner_image"), published, resolveBase));
            }
            return new ParsedFeed(Clean(Str(root, "title")), home ?? baseUri?.ToString(), parsed);
        }
    }

    // MARK: helpers

    private static bool Is(XElement e, string localName) =>
        string.Equals(e.Name.LocalName, localName, StringComparison.OrdinalIgnoreCase);

    private static XElement? Child(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => Is(e, localName));

    private static string? Text(XElement? e)
    {
        var value = e?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    /// <summary>Atom/RSS content: inline XHTML is serialised, everything else is its text value.</summary>
    private static string? Body(XElement e)
    {
        if ((string?)e.Attribute("type") != "xhtml")
            return Text(e);
        var container = e.Elements().FirstOrDefault(c => c.Name.NamespaceName == XhtmlNs && c.Name.LocalName == "div") ?? e;
        var html = string.Concat(container.Nodes().Select(n => n.ToString(SaveOptions.DisableFormatting)));
        return html.Replace($" xmlns=\"{XhtmlNs}\"", "").Trim();
    }

    private static bool IsImage(XElement e) =>
        ((string?)e.Attribute("type"))?.StartsWith("image", StringComparison.OrdinalIgnoreCase) == true;

    private static string? AlternateHref(XElement parent) =>
        parent.Elements()
            .Where(e => Is(e, "link") && ((string?)e.Attribute("rel") ?? "alternate") == "alternate")
            .Select(e => (string?)e.Attribute("href"))
            .FirstOrDefault();

    private static Uri? Absolute(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? uri : null;

    private static string? Resolve(string? url, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        url = url.Trim();
        if (Absolute(url) is { } absolute)
            return absolute.ToString();
        return baseUri is not null && Uri.TryCreate(baseUri, url, out var resolved) ? resolved.ToString() : url;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    private static string? Str(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
}
