using AngleSharp.Dom;
using Ganss.Xss;

namespace RSSmote.Core.Rendering;

/// <summary>
/// Strips active content from feed HTML (A9): no scripts, styles, forms or event handlers, URLs made absolute
/// against the article's address, lazy-loaded images given their real source. Iframes survive only for known
/// video players; any other iframe becomes a plain link.
/// </summary>
public static class ArticleSanitizer
{
    private static readonly HashSet<string> VideoHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com", "www.youtube.com", "youtube-nocookie.com", "www.youtube-nocookie.com", "player.vimeo.com",
    };

    private static readonly string[] LazySources = ["data-src", "data-lazy-src", "data-original"];

    private static readonly HtmlSanitizer Sanitizer = Create();

    // Sanitize is synchronous, so the base address of the call in progress can live per thread.
    [ThreadStatic]
    private static Uri? t_base;

    public static string Sanitize(string html, string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
            return "";
        t_base = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && IsWeb(uri) ? uri : null;
        try
        {
            return Sanitizer.Sanitize(html, t_base?.AbsoluteUri ?? "").Trim();
        }
        finally
        {
            t_base = null;
        }
    }

    public static bool IsVideoEmbed(Uri uri) => IsWeb(uri) && VideoHosts.Contains(uri.Host);

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer { KeepChildNodes = true };
        foreach (var tag in new[] { "iframe", "figure", "figcaption", "picture", "source", "video", "audio" })
            sanitizer.AllowedTags.Add(tag);
        foreach (var tag in new[] { "script", "style", "noscript", "template", "form", "button", "input", "select", "textarea", "object", "embed" })
            sanitizer.AllowedTags.Remove(tag);
        foreach (var attribute in new[] { "srcset", "sizes", "controls", "poster", "allowfullscreen" }.Concat(LazySources))
            sanitizer.AllowedAttributes.Add(attribute);
        foreach (var attribute in new[] { "style", "width", "height", "class", "id" })
            sanitizer.AllowedAttributes.Remove(attribute);
        sanitizer.UriAttributes.Add("poster");
        sanitizer.AllowedSchemes.Add("mailto");
        sanitizer.RemovingTag += (_, e) =>
        {
            // Unknown wrappers keep their text (KeepChildNodes); these never should.
            if (e.Tag.LocalName is "script" or "style" or "noscript" or "template" or "object" or "embed")
                e.Tag.InnerHtml = "";
        };
        sanitizer.PostProcessNode += (_, e) =>
        {
            if (e.Node is not IElement element)
                return;
            if (element.LocalName == "img")
                ResolveLazySource(element);
            else if (element.LocalName == "iframe")
                ReplaceForeignFrame(e, element);
            foreach (var name in LazySources)
                element.RemoveAttribute(name);
        };
        return sanitizer;
    }

    private static void ResolveLazySource(IElement image)
    {
        var src = image.GetAttribute("src");
        if (!string.IsNullOrWhiteSpace(src) && !src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return;
        foreach (var name in LazySources)
        {
            if (Absolute(image.GetAttribute(name)) is { } lazy)
            {
                image.SetAttribute("src", lazy.AbsoluteUri);
                return;
            }
        }
    }

    private static void ReplaceForeignFrame(PostProcessNodeEventArgs e, IElement frame)
    {
        var src = Absolute(frame.GetAttribute("src"));
        if (src is not null && IsVideoEmbed(src))
            return;
        if (src is null)
        {
            e.ReplacementNodes.Add(e.Document.CreateTextNode(""));
            return;
        }
        var paragraph = e.Document.CreateElement("p");
        var link = e.Document.CreateElement("a");
        link.SetAttribute("href", src.AbsoluteUri);
        link.TextContent = src.Host;
        paragraph.AppendChild(link);
        e.ReplacementNodes.Add(paragraph);
    }

    private static Uri? Absolute(string? value) => Resolve(value, t_base);

    /// <summary>
    /// An http(s) address, resolved against <paramref name="baseUri"/> when relative. A rooted path such as
    /// "/a.png" parses as an absolute file URI on Unix (L8), so web addresses are only accepted by scheme.
    /// </summary>
    public static Uri? Resolve(string? value, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        value = value.Trim();
        if (value.StartsWith("//", StringComparison.Ordinal))
            value = (baseUri?.Scheme ?? "https") + ":" + value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) && IsWeb(absolute))
            return absolute;
        return baseUri is not null && Uri.TryCreate(baseUri, value, out var resolved) && IsWeb(resolved) ? resolved : null;
    }

    private static bool IsWeb(Uri uri) => uri.Scheme is "http" or "https";
}
