using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;

namespace RSSmote.Core.Rendering;

/// <summary>
/// Builds the HTML page for the article column (D14): date, title, a one-line byline ending in the accent source,
/// then the sanitized body. Colours and fonts are written into the stylesheet as literal values, because the fallback
/// renderer has no CSS custom properties.
/// </summary>
public static partial class ArticleTemplate
{
    /// <summary>Scrolls one page; evaluates to "false" at the end so space can move on to the next article.</summary>
    public static string ScrollPageScript(bool up) => up ? "rssmote.scrollPage(true)" : "rssmote.scrollPage(false)";

    private static readonly string Css = ReadResource("article.css");
    private static readonly string Script = ReadResource("article.js");

    [GeneratedRegex(@"var\(--([a-z-]+)\)")]
    private static partial Regex CssVariable();

    [GeneratedRegex(@"(?is)<iframe\b[^>]*?\bsrc=""([^""]+)""[^>]*>.*?</iframe>")]
    private static partial Regex Frame();

    public static string FontStack(ArticleFont font) => font switch
    {
        ArticleFont.Serif => "ui-serif, \"New York\", \"Iowan Old Style\", Charter, Georgia, Cambria, \"Noto Serif\", serif",
        ArticleFont.Rounded => "ui-rounded, \"SF Pro Rounded\", system-ui, -apple-system, \"Segoe UI\", sans-serif",
        ArticleFont.Mono => "ui-monospace, \"SF Mono\", Menlo, Consolas, \"DejaVu Sans Mono\", monospace",
        _ => SystemFontStack,
    };

    private const string SystemFontStack =
        "system-ui, -apple-system, \"Segoe UI\", Ubuntu, Cantarell, \"Noto Sans\", \"Helvetica Neue\", sans-serif";

    public static string Render(ArticleDocument document, ArticleStyle style)
    {
        var e = HtmlContent.Escape;
        var content = style.Scripted ? document.ContentHtml : Frame().Replace(document.ContentHtml, FrameLink);
        var html = new StringBuilder(content.Length + Css.Length + Script.Length + 1024);
        html.Append("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n")
            .Append("<style>\n").Append(Stylesheet(style)).Append("</style>\n</head>\n<body>\n<main>\n<article>\n<header>\n")
            .Append("<div class=\"date\">").Append(e(document.DateText)).Append("</div>\n<h1>");
        if (document.Link is { } link)
            html.Append("<a href=\"").Append(e(link)).Append("\">").Append(e(document.Title)).Append("</a>");
        else
            html.Append(e(document.Title));
        html.Append("</h1>\n<div class=\"byline\">");
        for (var i = 0; i < document.Byline.Count; i++)
        {
            var last = i == document.Byline.Count - 1;
            if (i > 0)
                html.Append(" · ");
            html.Append(last ? "<span class=\"source\">" : "<span>").Append(e(document.Byline[i])).Append("</span>");
        }
        html.Append("</div>\n</header>\n");
        if (document.HeroImage is { } hero)
            html.Append("<figure class=\"hero\"><img src=\"").Append(e(hero)).Append("\" alt=\"\"></figure>\n");
        html.Append("<div class=\"content\">").Append(content).Append("</div>\n</article>\n</main>\n");
        if (style.Scripted)
            html.Append("<script>\n").Append(Script).Append("</script>\n");
        html.Append("</body>\n</html>\n");
        return html.ToString();
    }

    /// <summary>The stylesheet with every <c>var(--name)</c> replaced by the style's value.</summary>
    public static string Stylesheet(ArticleStyle style)
    {
        var p = style.Palette;
        var values = new Dictionary<string, string>
        {
            ["bg"] = p.Background,
            ["text"] = p.Text,
            ["secondary"] = p.Secondary,
            ["accent"] = p.Accent,
            ["rule"] = p.Rule,
            ["code-bg"] = p.CodeBackground,
            ["selection"] = p.Selection,
            ["font"] = FontStack(style.Font),
            ["ui-font"] = SystemFontStack,
            ["font-size"] = style.FontSize.ToString(CultureInfo.InvariantCulture) + "px",
        };
        return CssVariable().Replace(Css, m => values.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);
    }

    private static string FrameLink(Match match)
    {
        var src = HtmlContent.DecodeEntities(match.Groups[1].Value);
        var label = Uri.TryCreate(src, UriKind.Absolute, out var uri) && ArticleSanitizer.IsVideoEmbed(uri)
            ? "▶ Video on " + uri.Host.Replace("www.", "", StringComparison.Ordinal)
            : uri?.Host ?? src;
        return $"<p class=\"embed\"><a href=\"{HtmlContent.Escape(src)}\">{HtmlContent.Escape(label)}</a></p>";
    }

    private static string ReadResource(string name)
    {
        var assembly = typeof(ArticleTemplate).Assembly;
        using var stream = assembly.GetManifestResourceStream("RSSmote.Core.Rendering." + name)
                           ?? throw new InvalidOperationException($"Missing resource {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
