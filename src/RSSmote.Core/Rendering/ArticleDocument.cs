using RSSmote.Core.Models;

namespace RSSmote.Core.Rendering;

/// <summary>What the article template shows: header texts and the sanitized body.</summary>
public sealed record ArticleDocument(
    long ItemId,
    string Title,
    string DateText,
    IReadOnlyList<string> Byline,
    string? Link,
    string? HeroImage,
    string ContentHtml)
{
    /// <summary>Sanitizes the item's content once; flag changes on the item do not need a new document.</summary>
    public static ArticleDocument Create(Item item, string dateText)
    {
        var content = ArticleSanitizer.Sanitize(item.Content, item.Url);
        string?[] byline = [item.Author, item.FeedTitle];
        var link = ArticleSanitizer.Resolve(item.Url, null);
        var hero = item.ImageUrl is { Length: > 0 } image && !item.Content.Contains(image, StringComparison.Ordinal)
            ? ArticleSanitizer.Resolve(image, link)?.AbsoluteUri
            : null;
        return new ArticleDocument(
            item.Id,
            item.Title,
            dateText,
            byline.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).Distinct().ToArray(),
            link?.AbsoluteUri,
            hero,
            content);
    }
}

/// <summary>Palette values as CSS colours, taken from the app theme (D14).</summary>
public sealed record ArticlePalette(
    string Background,
    string Text,
    string Secondary,
    string Accent,
    string Rule,
    string CodeBackground,
    string Selection);

/// <summary>
/// How a document is rendered. <paramref name="Scripted"/> is false for the fallback renderer, which runs no
/// JavaScript and cannot show iframes.
/// </summary>
public sealed record ArticleStyle(ArticlePalette Palette, double FontSize, bool Scripted);
