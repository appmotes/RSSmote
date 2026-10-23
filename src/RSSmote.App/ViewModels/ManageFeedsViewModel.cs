using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RSSmote.App.Services;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>A feed on the Manage Feeds page: its host, or the error of its last refresh.</summary>
public sealed partial class ManagedFeedViewModel(Feed feed, bool grayscale) : ObservableObject
{
    public Feed Feed { get; } = feed;
    public string Title => Feed.Title;
    public string Subtitle => Feed.LastError ?? Feed.Host ?? Feed.Url;
    public bool HasError => Feed.LastError is not null;
    public bool IsFaviconGrayscale { get; } = grayscale;

    [ObservableProperty]
    public partial Bitmap? Favicon { get; set; }
}

/// <summary>A folder and its feeds, or the feeds in no folder (<see cref="Folder"/> null).</summary>
public sealed record ManagedGroup(Folder? Folder, string Title, IReadOnlyList<ManagedFeedViewModel> Feeds)
{
    public bool IsFolder => Folder is not null;
    public bool HasFeeds => Feeds.Count > 0;
}

/// <summary>
/// Every feed, one group per folder and one for feeds in no folder (D27), narrowed by the filter box. Follows the
/// library while it lives; dispose it when its page goes.
/// </summary>
public sealed partial class ManageFeedsViewModel : ObservableObject, IDisposable
{
    /// <summary>The favicon service's own size; the Edit Feed page shows it larger.</summary>
    public const int FaviconDecodeWidth = 64;

    private readonly Store _store;
    private readonly SettingsStore _settings;
    private readonly IImageLoader _images;
    private readonly IUiDispatcher _ui;
    private readonly Dictionary<long, Bitmap?> _favicons = [];
    private (IReadOnlyList<Folder> Folders, IReadOnlyList<Feed> Feeds, string Filter)? _shown;

    public ManageFeedsViewModel(Store store, SettingsStore settings, IImageLoader images, IUiDispatcher ui)
    {
        _store = store;
        _settings = settings;
        _images = images;
        _ui = ui;
        store.Changed += OnStoreChanged;
        Reload();
    }

    public ObservableCollection<ManagedGroup> Groups { get; } = [];

    [ObservableProperty]
    public partial string FilterText { get; set; } = "";

    partial void OnFilterTextChanged(string value) => Reload();

    public void Dispose() => _store.Changed -= OnStoreChanged;

    private void OnStoreChanged(object? sender, StoreChangedEventArgs e)
    {
        if (e.ItemIds is null)
            _ui.Post(Reload);
    }

    /// <summary>
    /// Rebuilds the groups when a folder, a feed or the filter differs from what they show. A refresh changes the
    /// store once per feed, mostly in articles, which leave the groups as they are.
    /// </summary>
    public void Reload()
    {
        var filter = FilterText.Trim();
        var folders = _store.Folders();
        var all = _store.Feeds();
        if (_shown is { } shown && shown.Filter == filter && shown.Folders.SequenceEqual(folders) && shown.Feeds.SequenceEqual(all))
            return;
        _shown = (folders, all, filter);
        var feeds = all
            .Where(f => filter.Length == 0 || f.Title.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                           || (f.Host?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        var groups = folders
            .Select(folder => new ManagedGroup(folder, folder.Title, Rows(feeds.Where(f => f.FolderId == folder.Id))))
            .Append(new ManagedGroup(null, "No folder", Rows(feeds.Where(f => f.FolderId is null))))
            .Where(g => g.HasFeeds || (g.IsFolder && filter.Length == 0));
        Groups.Clear();
        foreach (var group in groups)
            Groups.Add(group);
    }

    private List<ManagedFeedViewModel> Rows(IEnumerable<Feed> feeds)
    {
        var settings = _settings.Current;
        return [.. feeds.Select(feed =>
        {
            var row = new ManagedFeedViewModel(feed, settings.GrayscaleFavicons);
            if (_favicons.TryGetValue(feed.Id, out var cached))
                row.Favicon = cached;
            else if (settings.DisplayFavicons && Favicons.Url(feed) is { } url)
                _ = LoadFaviconAsync(feed.Id, url);
            return row;
        })];
    }

    private async Task LoadFaviconAsync(long feedId, string url)
    {
        _favicons[feedId] = null;
        var bitmap = await _images.LoadAsync(url, FaviconDecodeWidth).ConfigureAwait(false);
        _ui.Post(() =>
        {
            _favicons[feedId] = bitmap;
            foreach (var row in Groups.SelectMany(g => g.Feeds).Where(r => r.Feed.Id == feedId))
                row.Favicon = bitmap;
        });
    }
}
