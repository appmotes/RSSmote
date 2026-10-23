using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Formatting;
using RSSmote.App.Services;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>
/// The article list (D18): day headings and article rows for the selected stream under the current filter, sort
/// and search. The article being read stays listed after it stops matching the filter (e.g. once read).
/// </summary>
public sealed partial class ArticleListViewModel : ObservableObject
{
    public const int ThumbnailDecodeWidth = 122;

    private readonly Store _store;
    private readonly SettingsStore _settings;
    private readonly IImageLoader _images;
    private readonly IUiDispatcher _ui;
    private readonly TimeProvider _clock;
    private readonly Func<long, Feed?> _feedLookup;
    private bool _reloading;

    public ArticleListViewModel(Store store, SettingsStore settings, IImageLoader images, IUiDispatcher ui,
                                TimeProvider clock, Func<long, Feed?> feedLookup)
    {
        _store = store;
        _settings = settings;
        _images = images;
        _ui = ui;
        _clock = clock;
        _feedLookup = feedLookup;
        Stream = new ArticleStream.All();
        Filter = settings.Current.Filter;
    }

    /// <summary><see cref="DateHeaderViewModel"/> and <see cref="ArticleRowViewModel"/> items.</summary>
    public ObservableCollection<object> Rows { get; } = [];

    public IEnumerable<ArticleRowViewModel> Articles => Rows.OfType<ArticleRowViewModel>();

    public ArticleStream Stream { get; private set; }

    /// <summary>Raised when the selected article changes, including to none.</summary>
    public event EventHandler<ArticleRowViewModel?>? ArticleSelected;

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    /// <summary>"4 Unread Items": the count badge's tooltip.</summary>
    [ObservableProperty]
    public partial string Subtitle { get; set; } = "";

    /// <summary>"3 unread · newest first": the line under the phone list's title (D25).</summary>
    [ObservableProperty]
    public partial string PhoneSubtitle { get; set; } = "";

    /// <summary>The count badge next to the title; empty when there is nothing to count.</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = "";

    /// <summary>The status bar: how many articles are listed and in which order, or how many match the filter box.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    /// <summary>Nothing to list: the list shows its compact empty state instead (D9).</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string EmptyTitle { get; set; } = "";

    [ObservableProperty]
    public partial string EmptySubtitle { get; set; } = "";

    /// <summary>The filter box has text; the empty state then speaks of matches.</summary>
    public bool IsSearching => !string.IsNullOrWhiteSpace(SearchText);

    [ObservableProperty]
    public partial StreamFilter Filter { get; set; }

    partial void OnFilterChanged(StreamFilter value)
    {
        if (_settings.Current.Filter != value)
            _settings.Update(s => s.Filter = value);
    }

    /// <summary>Asks the view to move the keyboard into the filter box (Cmd/Ctrl+F).</summary>
    public event EventHandler? SearchFocusRequested;

    /// <summary>Asks the view to hand the keyboard back to the list after the filter box is cleared.</summary>
    public event EventHandler? SearchEnded;

    public void FocusSearch() => SearchFocusRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    /// <summary>Escape in the filter box: clears it and returns to the list.</summary>
    public void EndSearch()
    {
        SearchText = "";
        SearchEnded?.Invoke(this, EventArgs.Empty);
    }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        Reload(keepSelection: true);
    }

    [ObservableProperty]
    public partial object? SelectedRow { get; set; }

    partial void OnSelectedRowChanged(object? oldValue, object? newValue)
    {
        if (_reloading)
            return;
        if (newValue is DateHeaderViewModel)
        {
            // Day headings are not selectable: keep the previous article.
            SelectedRow = oldValue;
            return;
        }
        ArticleSelected?.Invoke(this, newValue as ArticleRowViewModel);
    }

    public ArticleRowViewModel? SelectedArticle => SelectedRow as ArticleRowViewModel;

    /// <summary>A view shows the list. While false (a phone back on its Feeds root) store changes leave the rows
    /// alone, and the next <see cref="Show"/> rebuilds them.</summary>
    public bool IsShown { get; private set; } = true;

    public void Show(ArticleStream stream, string title)
    {
        IsShown = true;
        Stream = stream;
        Title = title;
        Reload(keepSelection: false);
    }

    /// <summary>No view shows the list any more; see <see cref="IsShown"/>.</summary>
    public void Hide() => IsShown = false;

    public void Reload(bool keepSelection)
    {
        if (!IsShown)
            return;
        var settings = _settings.Current;
        Filter = settings.Filter;
        var selected = keepSelection ? SelectedArticle : null;
        var items = _store.Items(Stream, settings.Filter, SearchText, settings.SortNewestFirst).ToList();
        if (selected is not null && items.All(i => i.Id != selected.Id))
        {
            var at = items.FindIndex(i => settings.SortNewestFirst ? i.Published < selected.Item.Published : i.Published > selected.Item.Published);
            items.Insert(at < 0 ? items.Count : at, selected.Item);
        }

        var now = _clock.GetUtcNow();
        var today = now.ToLocalTime().Date;
        var rows = new List<object>(items.Count + 8);
        DateTime? lastDay = null;
        DateHeaderViewModel? header = null;
        foreach (var item in items)
        {
            if (settings.GroupByDate)
            {
                var day = item.Published.ToLocalTime().Date;
                if (day != lastDay)
                {
                    header = new DateHeaderViewModel(ItemFormatting.DayHeader(day, today));
                    rows.Add(header);
                    lastDay = day;
                }
                header!.Count++;
            }
            rows.Add(selected?.Id == item.Id ? selected : new ArticleRowViewModel(item, now, settings.GrayscaleFavicons));
        }

        _reloading = true;
        try
        {
            Rows.Clear();
            foreach (var row in rows)
                Rows.Add(row);
            SelectedRow = selected is null ? null : rows.FirstOrDefault(r => r == selected);
        }
        finally
        {
            _reloading = false;
        }
        UpdateSubtitle();
        UpdateStatusText(rows.Count(r => r is ArticleRowViewModel), settings.SortNewestFirst);
        UpdateEmptyText(rows.Count == 0, settings.Filter);
        if (SelectedRow is null)
            ArticleSelected?.Invoke(this, null);
    }

    /// <summary>Applies read/starred changes to the listed rows without rebuilding the list.</summary>
    public void ApplyFlags(IReadOnlyList<long> ids)
    {
        var wanted = ids.ToHashSet();
        foreach (var row in Articles.Where(r => wanted.Contains(r.Id)))
        {
            if (_store.Item(row.Id) is { } item)
                row.Update(item);
        }
        UpdateSubtitle();
    }

    public void UpdateSubtitle()
    {
        var filter = _settings.Current.Filter == StreamFilter.Starred ? StreamFilter.Starred : StreamFilter.Unread;
        var count = _store.Count(Stream, filter);
        Subtitle = ItemFormatting.CountText(count, filter);
        CountText = count > 0 ? count.ToString(System.Globalization.CultureInfo.CurrentCulture) : "";
        PhoneSubtitle = $"{count} {(filter == StreamFilter.Starred ? "starred" : "unread")} · "
                        + (_settings.Current.SortNewestFirst ? "newest first" : "oldest first");
    }

    private void UpdateEmptyText(bool empty, StreamFilter filter)
    {
        IsEmpty = empty;
        (EmptyTitle, EmptySubtitle) = (IsSearching, filter) switch
        {
            (true, _) => ("No matches", $"Nothing here matches “{SearchText.Trim()}”."),
            (_, StreamFilter.Starred) => ("No starred articles", "Press s on an article to star it."),
            (_, StreamFilter.Unread) => ("All caught up", "Nothing unread here."),
            _ => ("No articles", "Nothing has arrived here yet."),
        };
    }

    private void UpdateStatusText(int listed, bool newestFirst) => StatusText = string.IsNullOrWhiteSpace(SearchText)
        ? $"{ItemFormatting.ArticleCountText(listed)} · {(newestFirst ? "newest first" : "oldest first")}"
        : $"{(listed == 1 ? "1 match" : $"{listed} matches")} for “{SearchText.Trim()}”";

    /// <summary>Selects the next or previous article; false when there is none in that direction.</summary>
    public bool SelectAdjacent(int delta)
    {
        var articles = Articles.ToList();
        if (articles.Count == 0)
            return false;
        var index = SelectedArticle is { } current ? articles.IndexOf(current) : -1;
        var next = index < 0 ? (delta > 0 ? 0 : articles.Count - 1) : index + delta;
        if (next < 0 || next >= articles.Count)
            return false;
        SelectedRow = articles[next];
        return true;
    }

    /// <summary>Starts loading a realised row's favicon and thumbnail.</summary>
    public void RequestImages(ArticleRowViewModel row)
    {
        if (!row.ClaimImageRequest())
            return;
        if (_settings.Current.DisplayFavicons && _feedLookup(row.Item.FeedId) is { } feed && Favicons.Url(feed) is { } favicon)
            _ = LoadAsync(favicon, Favicons.DecodeWidth, b => row.Favicon = b);
        if (row.Item.ImageUrl is { } image)
            _ = LoadAsync(image, ThumbnailDecodeWidth, b => row.Thumbnail = b);
    }

    private async Task LoadAsync(string url, int width, Action<Avalonia.Media.Imaging.Bitmap> apply)
    {
        if (await _images.LoadAsync(url, width).ConfigureAwait(false) is { } bitmap)
            _ui.Post(() => apply(bitmap));
    }
}
