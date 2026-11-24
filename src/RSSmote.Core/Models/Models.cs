using System.Text.Json.Serialization;

namespace RSSmote.Core.Models;

public sealed record Folder(long Id, string Title);

public sealed record Feed(long Id, string Title, string Url, string? SiteUrl, long? FolderId, string? LastError)
{
    [JsonIgnore]
    public string? Host => Uri.TryCreate(SiteUrl ?? Url, UriKind.Absolute, out var uri) ? uri.Host : null;

    /// <summary>Favicon source for the feed: the site's host through a public favicon service.</summary>
    [JsonIgnore]
    public string? FaviconUrl =>
        Host is { } host ? $"https://www.google.com/s2/favicons?sz=64&domain={Uri.EscapeDataString(host)}" : null;
}

public sealed record Item(
    long Id,
    long FeedId,
    string Guid,
    string Title,
    string? Author,
    string? Url,
    string Content,
    string Summary,
    string? ImageUrl,
    DateTimeOffset Published,
    bool Unread,
    bool Starred,
    string FeedTitle);

/// <summary>Which articles a stream shows: the filter bar's three states.</summary>
public enum StreamFilter
{
    Starred,
    Unread,
    All,
}

/// <summary>A selectable source in the sidebar.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(All), "all")]
[JsonDerivedType(typeof(InFolder), "folder")]
[JsonDerivedType(typeof(OfFeed), "feed")]
public abstract record ArticleStream
{
    public sealed record All : ArticleStream;
    public sealed record InFolder(long FolderId) : ArticleStream;
    public sealed record OfFeed(long FeedId) : ArticleStream;
}
