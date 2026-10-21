using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RSSmote.Core.Parsing;

/// <summary>Small string helpers for feed HTML: entity decoding, plain text, excerpts, first image.</summary>
public static partial class HtmlContent
{
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"(?is)<(script|style)[^>]*>.*?</\1>")]
    private static partial Regex ScriptOrStyle();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("""<img\b[^>]*?\bsrc\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex ImageSource();

    /// <summary>Decodes named and numeric entities; unknown entities stay as written.</summary>
    public static string DecodeEntities(string text) =>
        text.Contains('&') ? WebUtility.HtmlDecode(text) : text;

    public static string Escape(string text) => WebUtility.HtmlEncode(text);

    public static string StripTags(string html) => Tag().Replace(html, " ");

    /// <summary>Text without scripts, styles or tags, entities decoded, whitespace collapsed.</summary>
    public static string PlainText(string html)
    {
        var text = ScriptOrStyle().Replace(html, " ");
        text = DecodeEntities(StripTags(text));
        return Whitespace().Replace(text, " ").Trim();
    }

    /// <summary>Plain text cut to <paramref name="length"/> characters with an ellipsis.</summary>
    public static string Excerpt(string html, int length = 280)
    {
        var text = PlainText(html);
        if (text.Length <= length)
            return text;
        var info = new StringInfo(text);
        var cut = info.LengthInTextElements > length ? info.SubstringByTextElements(0, length) : text[..length];
        return cut.TrimEnd() + "…";
    }

    public static string? FirstImage(string html)
    {
        var match = ImageSource().Match(html);
        return match.Success ? DecodeEntities(match.Groups[1].Value) : null;
    }
}
