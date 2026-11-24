namespace RSSmote.Data;

/// <summary>An item without its text: enough to filter, sort and page a stream.</summary>
public readonly record struct ItemRef(long Id, long FeedId, DateTimeOffset Published, bool Unread, bool Starred);
