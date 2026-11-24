using System.Text.Json.Serialization;

namespace RSSmote.Server;

// The Reader API's answers (A25), named as the protocol names them once camel-cased.

public sealed record ReaderUserInfo(string UserId, string UserName, string UserProfileId, string UserEmail);

public sealed record ReaderSubscriptions(IReadOnlyList<ReaderSubscription> Subscriptions);

public sealed record ReaderSubscription(
    string Id,
    string Title,
    IReadOnlyList<ReaderCategory> Categories,
    string Url,
    string HtmlUrl,
    string? IconUrl);

public sealed record ReaderCategory(string Id, string Label);

public sealed record ReaderTags(IReadOnlyList<ReaderTag> Tags);

public sealed record ReaderTag(string Id, string? Type);

public sealed record ReaderUnreadCounts(int Max, IReadOnlyList<ReaderUnreadCount> Unreadcounts);

public sealed record ReaderUnreadCount(string Id, int Count, string NewestItemTimestampUsec);

public sealed record ReaderItemIds(IReadOnlyList<ReaderItemRef> ItemRefs, string? Continuation);

public sealed record ReaderItemRef(string Id);

public sealed record ReaderStream(string Id, long Updated, IReadOnlyList<ReaderItem> Items, string? Continuation);

public sealed record ReaderItem(
    string Id,
    string CrawlTimeMsec,
    string TimestampUsec,
    long Published,
    string Title,
    string? Author,
    IReadOnlyList<ReaderLink> Canonical,
    IReadOnlyList<ReaderLink> Alternate,
    IReadOnlyList<string> Categories,
    ReaderOrigin Origin,
    ReaderContent Summary);

public sealed record ReaderLink(string Href);

public sealed record ReaderOrigin(string StreamId, string Title, string? HtmlUrl);

public sealed record ReaderContent(string Content);

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ReaderUserInfo))]
[JsonSerializable(typeof(ReaderSubscriptions))]
[JsonSerializable(typeof(ReaderTags))]
[JsonSerializable(typeof(ReaderUnreadCounts))]
[JsonSerializable(typeof(ReaderItemIds))]
[JsonSerializable(typeof(ReaderStream))]
public sealed partial class ReaderJsonContext : JsonSerializerContext;
