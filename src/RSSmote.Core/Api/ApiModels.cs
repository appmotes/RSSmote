using System.Text.Json;
using System.Text.Json.Serialization;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;

namespace RSSmote.Core.Api;

/// <summary>The settings the server keeps, as they belong to the library rather than to one device.</summary>
public sealed record LibrarySettings(int RefreshIntervalMinutes, int KeepArchivedDays, int MarkReadAfterDays, DateTimeOffset? LastRefresh)
{
    public static LibrarySettings From(AppSettings s) => new(s.RefreshIntervalMinutes, s.KeepArchivedDays, s.MarkReadAfterDays, s.LastRefresh);

    public void ApplyTo(AppSettings s)
    {
        s.RefreshIntervalMinutes = RefreshIntervalMinutes;
        s.KeepArchivedDays = KeepArchivedDays;
        s.MarkReadAfterDays = MarkReadAfterDays;
        s.LastRefresh = LastRefresh;
    }
}

/// <summary>Everything a client mirrors: items come without their content.</summary>
public sealed record LibrarySnapshot(
    IReadOnlyList<Folder> Folders,
    IReadOnlyList<Feed> Feeds,
    IReadOnlyList<Item> Items,
    LibrarySettings Settings,
    bool IsRefreshing);

public sealed record ItemFlags(long Id, bool Unread, bool Starred);

/// <summary>Flags written by the client named <see cref="Origin"/>, which has applied them already.</summary>
public sealed record FlagsEvent(string? Origin, IReadOnlyList<ItemFlags> Items);

public sealed record LoginRequest(string Key);

public sealed record FlagRequest(IReadOnlyList<long> Ids, bool Value);

public sealed record TitleRequest(string Title);

public sealed record FindRequest(string Input);

public sealed record AddFeedRequest(FoundFeed Found, long? FolderId);

public sealed record ImportRequest(IReadOnlyList<OpmlOutline> Outlines);

public sealed record CountResponse(int Count);

public sealed record ErrorResponse(string Message);

/// <summary>Paths, headers and event names both sides use.</summary>
public static class ApiNames
{
    /// <summary>A random id per loaded app. Required on every write, which a cross-site form cannot send.</summary>
    public const string ClientHeader = "X-RSSmote-Client";

    public const string LibraryEvent = "library";
    public const string FlagsEvent = "flags";
    public const string RefreshEvent = "refresh";
    public const string SettingsEvent = "settings";
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UseStringEnumConverter = true)]
[JsonSerializable(typeof(LibrarySnapshot))]
[JsonSerializable(typeof(LibrarySettings))]
[JsonSerializable(typeof(FlagsEvent))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(FlagRequest))]
[JsonSerializable(typeof(TitleRequest))]
[JsonSerializable(typeof(FindRequest))]
[JsonSerializable(typeof(AddFeedRequest))]
[JsonSerializable(typeof(ImportRequest))]
[JsonSerializable(typeof(CountResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ArticleStream))]
[JsonSerializable(typeof(Item))]
[JsonSerializable(typeof(Feed))]
[JsonSerializable(typeof(Folder))]
[JsonSerializable(typeof(IReadOnlyList<FoundFeed>))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(AppSettings))]
public sealed partial class ApiJsonContext : JsonSerializerContext;
