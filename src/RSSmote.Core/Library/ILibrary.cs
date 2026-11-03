using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Core.Opml;

namespace RSSmote.Core.Library;

/// <summary>Raised after every write. <see cref="ItemIds"/> is set when only read/starred flags changed.</summary>
public sealed class LibraryChangedEventArgs(IReadOnlyList<long>? itemIds) : EventArgs
{
    public IReadOnlyList<long>? ItemIds { get; } = itemIds;
    public bool FlagsOnly => ItemIds is not null;
}

/// <summary>
/// Folders, feeds and items as the view-models see them. Reads answer at once; a write whose result the server
/// decides is a task. The flag writes apply at once and are not awaited. <see cref="Item"/> may come without its
/// <see cref="Models.Item.Content"/>; <see cref="LoadItemAsync"/> always has it.
/// </summary>
public interface ILibrary
{
    event EventHandler<LibraryChangedEventArgs>? Changed;

    IReadOnlyList<Folder> Folders();

    Folder? FolderNamed(string title);

    IReadOnlyList<Feed> Feeds();

    Feed? Feed(long id);

    IReadOnlyList<Item> Items(ArticleStream stream, StreamFilter filter, string search = "", bool newestFirst = true, int limit = 3000);

    Item? Item(long id);

    /// <summary>Number of items in a stream matching the filter.</summary>
    int Count(ArticleStream stream, StreamFilter filter);

    /// <summary>Unread counts per feed, or starred counts under the Starred filter.</summary>
    IReadOnlyDictionary<long, int> Counts(StreamFilter filter);

    /// <summary>Every item per feed, read or not.</summary>
    IReadOnlyDictionary<long, int> TotalCounts();

    void SetUnread(IReadOnlyCollection<long> ids, bool unread);

    void SetStarred(IReadOnlyCollection<long> ids, bool starred);

    void MarkAllRead(ArticleStream stream);

    /// <summary>The item with its content.</summary>
    Task<Item?> LoadItemAsync(long id);

    Task<Folder> AddFolderAsync(string title);

    Task RenameFolderAsync(long id, string title);

    /// <summary>Deletes the folder; its feeds are deleted too or moved out of the folder.</summary>
    Task DeleteFolderAsync(long id, bool deleteFeeds);

    /// <summary>Stores a discovered feed with its current items; returns the stored feed.</summary>
    Task<Feed> AddFeedAsync(FoundFeed found, long? folderId);

    Task UpdateFeedAsync(Feed feed);

    /// <summary>Removes the feed and all its items, starred ones included.</summary>
    Task DeleteFeedAsync(long id);

    /// <summary>Adds the outlines whose feeds are not stored yet; returns how many feeds were added.</summary>
    Task<int> ImportOpmlAsync(IReadOnlyList<OpmlOutline> outlines);
}
