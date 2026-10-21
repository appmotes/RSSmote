using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RSSmote.App.Controls.Templates;
using RSSmote.App.Formatting;
using RSSmote.App.Services;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>
/// The sidebar: the filter-named smart stream, then collapsible Folders and Feeds sections (D10). Under the
/// Unread and Starred filters only feeds with something to show are listed, plus the selected one.
/// </summary>
public sealed partial class SidebarViewModel : ObservableObject
{
    public const string FoldersSection = "Folders";
    public const string FeedsSection = "Feeds";

    private readonly Store _store;
    private readonly SettingsStore _settings;
    private readonly IImageLoader _images;
    private readonly IUiDispatcher _ui;
    private readonly Dictionary<long, Bitmap?> _favicons = new();
    private bool _reloading;
    private string _refreshText = "";
    private int _feedCount;

    public SidebarViewModel(Store store, SettingsStore settings, IImageLoader images, IUiDispatcher ui)
    {
        _store = store;
        _settings = settings;
        _images = images;
        _ui = ui;
        SelectedStream = new ArticleStream.All();
        Reload();
    }

    public ObservableCollection<SidebarRowViewModel> Rows { get; } = [];

    /// <summary>Raised when the user picks another stream.</summary>
    public event EventHandler<ArticleStream>? StreamSelected;

    public ArticleStream SelectedStream { get; private set; }

    /// <summary>The sidebar status bar: when the library was last refreshed and how many feeds it holds.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>At least one feed failed its last refresh.</summary>
    [ObservableProperty]
    public partial bool HasFailures { get; set; }

    [ObservableProperty]
    public partial SidebarRowViewModel? SelectedRow { get; set; }

    partial void OnSelectedRowChanged(SidebarRowViewModel? oldValue, SidebarRowViewModel? newValue)
    {
        if (_reloading || newValue is null)
            return;
        if (newValue.Stream is null)
        {
            // Section headers toggle instead of selecting.
            Toggle(newValue);
            SelectedRow = Rows.FirstOrDefault(r => r.Stream == SelectedStream);
            return;
        }
        if (newValue.Stream == SelectedStream)
            return;
        SelectedStream = newValue.Stream;
        StreamSelected?.Invoke(this, SelectedStream);
    }

    public void Select(ArticleStream stream)
    {
        SelectedStream = stream;
        Reload();
        StreamSelected?.Invoke(this, stream);
    }

    public string Title(ArticleStream stream) => stream switch
    {
        ArticleStream.InFolder f => _store.Folders().FirstOrDefault(x => x.Id == f.FolderId)?.Title ?? "Folder",
        ArticleStream.OfFeed f => _store.Feed(f.FeedId)?.Title ?? "Feed",
        _ => ItemFormatting.FilterTitle(_settings.Current.Filter),
    };

    /// <summary>Opens or closes a section or folder and remembers it in settings.</summary>
    public void Toggle(SidebarRowViewModel row)
    {
        if (row.Kind == SidebarRowKind.Section && row.SectionKey is { } key)
        {
            _settings.Update(s => s.CollapsedSections = row.IsExpanded
                ? [.. s.CollapsedSections.Append(key).Distinct()]
                : [.. s.CollapsedSections.Where(k => k != key)]);
        }
        else if (row.Kind == SidebarRowKind.Folder && row.FolderId is { } id)
        {
            _settings.Update(s => s.ExpandedFolders = row.IsExpanded
                ? [.. s.ExpandedFolders.Where(f => f != id)]
                : [.. s.ExpandedFolders.Append(id).Distinct()]);
        }
        Reload();
    }

    /// <summary>Moves to the next or previous stream that has items under the current filter.</summary>
    public void SelectAdjacent(int delta)
    {
        var candidates = Rows.Where(r => r.IsSelectable && (r.Count > 0 || r.Stream == SelectedStream)).ToList();
        var index = candidates.FindIndex(r => r.Stream == SelectedStream);
        var next = index < 0 ? 0 : index + delta;
        if (next >= 0 && next < candidates.Count && candidates[next].Stream is { } stream && stream != SelectedStream)
            Select(stream);
    }

    public void Reload()
    {
        var settings = _settings.Current;
        var filter = settings.Filter;
        var counts = _store.Counts(filter);
        var folders = _store.Folders();
        var feeds = _store.Feeds();
        var expandedFolders = settings.ExpandedFolders.ToHashSet();
        var collapsedSections = settings.CollapsedSections.ToHashSet();

        int CountOf(Feed f) => counts.GetValueOrDefault(f.Id);
        bool IsSelected(ArticleStream s) => s == SelectedStream;
        bool Visible(Feed f) => filter == StreamFilter.All || CountOf(f) > 0 || IsSelected(new ArticleStream.OfFeed(f.Id));

        var rows = new List<SidebarRowViewModel>
        {
            new(SidebarRowKind.Smart, ItemFormatting.FilterTitle(filter), new ArticleStream.All()) { Count = counts.Values.Sum(), Filter = filter },
        };

        var folderRows = new List<SidebarRowViewModel>();
        foreach (var folder in folders)
        {
            var inFolder = feeds.Where(f => f.FolderId == folder.Id).ToList();
            var visible = inFolder.Where(Visible).ToList();
            var selectedInside = IsSelected(new ArticleStream.InFolder(folder.Id))
                                 || inFolder.Any(f => IsSelected(new ArticleStream.OfFeed(f.Id)));
            if (filter != StreamFilter.All && visible.Count == 0 && !selectedInside)
                continue;
            var expanded = expandedFolders.Contains(folder.Id);
            folderRows.Add(new SidebarRowViewModel(SidebarRowKind.Folder, folder.Title, new ArticleStream.InFolder(folder.Id))
            {
                FolderId = folder.Id,
                Count = inFolder.Sum(CountOf),
                IsExpanded = expanded,
                IsExpandable = visible.Count > 0,
            });
            if (expanded)
                folderRows.AddRange(visible.Select(f => FeedRow(f, CountOf(f))));
        }
        AddSection(rows, FoldersSection, folderRows, collapsedSections);

        _feedCount = feeds.Count;
        HasFailures = feeds.Any(f => f.LastError is not null);
        UpdateStatusText();

        var loose = feeds.Where(f => f.FolderId is null && Visible(f)).Select(f => FeedRow(f, CountOf(f))).ToList();
        AddSection(rows, FeedsSection, loose, collapsedSections);

        var previous = SelectedStream;
        _reloading = true;
        try
        {
            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
            // A selected feed hidden by a collapsed folder or section hands the selection to what is still shown.
            var parent = ParentOf(SelectedStream, feeds);
            SelectedRow = rows.FirstOrDefault(r => r.Stream == SelectedStream)
                          ?? (parent is null ? null : rows.FirstOrDefault(r => r.Stream == parent))
                          ?? rows[0];
            SelectedStream = SelectedRow.Stream!;
        }
        finally
        {
            _reloading = false;
        }
        if (SelectedStream != previous)
            StreamSelected?.Invoke(this, SelectedStream);
    }

    private static ArticleStream? ParentOf(ArticleStream stream, IReadOnlyList<Feed> feeds) =>
        stream is ArticleStream.OfFeed f && feeds.FirstOrDefault(x => x.Id == f.FeedId)?.FolderId is { } folderId
            ? new ArticleStream.InFolder(folderId)
            : null;

    private static void AddSection(List<SidebarRowViewModel> rows, string key, List<SidebarRowViewModel> children, HashSet<string> collapsed)
    {
        if (children.Count == 0)
            return;
        var expanded = !collapsed.Contains(key);
        rows.Add(new SidebarRowViewModel(SidebarRowKind.Section, key) { SectionKey = key, IsExpanded = expanded });
        if (expanded)
            rows.AddRange(children);
    }

    private SidebarRowViewModel FeedRow(Feed feed, int count)
    {
        var row = new SidebarRowViewModel(SidebarRowKind.Feed, feed.Title, new ArticleStream.OfFeed(feed.Id))
        {
            Feed = feed,
            Count = count,
            IsFaviconGrayscale = _settings.Current.GrayscaleFavicons,
            HasError = feed.LastError is not null,
        };
        if (_favicons.TryGetValue(feed.Id, out var cached))
            row.Favicon = cached;
        else if (_settings.Current.DisplayFavicons && Favicons.Url(feed) is { } url)
            _ = LoadFaviconAsync(feed.Id, url);
        return row;
    }

    private async Task LoadFaviconAsync(long feedId, string url)
    {
        _favicons[feedId] = null;
        var bitmap = await _images.LoadAsync(url, Favicons.DecodeWidth).ConfigureAwait(false);
        _ui.Post(() =>
        {
            _favicons[feedId] = bitmap;
            foreach (var row in Rows.Where(r => r.Feed?.Id == feedId))
                row.Favicon = bitmap;
        });
    }

    /// <summary>Fetches the feed's favicon again, bypassing the cache.</summary>
    public void ReloadFavicon(Feed feed)
    {
        if (Favicons.Url(feed) is not { } url)
            return;
        _images.Forget(url);
        _favicons.Remove(feed.Id);
        _ = LoadFaviconAsync(feed.Id, url);
    }

    public void UpdateRefreshText(DateTimeOffset? lastRefresh, bool refreshing, DateTimeOffset now)
    {
        _refreshText = ItemFormatting.LastRefreshText(lastRefresh, refreshing, now);
        UpdateStatusText();
    }

    private void UpdateStatusText() => StatusText = $"{_refreshText} · {ItemFormatting.FeedCountText(_feedCount)}";

    public Bitmap? FaviconFor(long feedId) => _favicons.GetValueOrDefault(feedId);
}
