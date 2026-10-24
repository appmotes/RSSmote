using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Formatting;
using RSSmote.App.Services;
using RSSmote.Core.Discovery;
using RSSmote.Core.Rendering;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>
/// The three columns (A5): selection flows sidebar â list â article. Store, settings and refresher events arrive
/// on any thread and are applied on the UI thread; structural store changes are debounced into one reload.
/// Every command a shortcut or menu can run lives here.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan ReloadDelay = TimeSpan.FromMilliseconds(300);

    private readonly Store _store;
    private readonly SettingsStore _settings;
    private readonly Refresher? _refresher;
    private readonly ILauncher _launcher;
    private readonly FeedFinder _finder;
    private readonly IFilePicker _files;
    private readonly IUiDispatcher _ui;
    private readonly IImageLoader _images;
    private readonly TimeProvider _clock;
    private readonly Debouncer _reload;
    private readonly Debouncer _recount;
    private AppSettings _lastSettings;

    public ShellViewModel(Store store, SettingsStore settings, Refresher? refresher, FeedFinder finder, IFilePicker files,
                          IImageLoader images, ILauncher launcher, IUiDispatcher ui, TimeProvider clock,
                          SidebarLayout layout = SidebarLayout.Desktop)
    {
        _store = store;
        _settings = settings;
        _refresher = refresher;
        _launcher = launcher;
        _finder = finder;
        _files = files;
        _ui = ui;
        _images = images;
        _clock = clock;
        _lastSettings = settings.Current;

        Sidebar = new SidebarViewModel(store, settings, images, ui, layout);
        List = new ArticleListViewModel(store, settings, images, ui, clock, id => store.Feed(id));
        Article = new ArticleViewModel();
        Article.ApplySettings(settings.Current);
        Settings = new SettingsViewModel(settings);
        _reload = new Debouncer(clock, ReloadDelay, ui, ReloadAll);
        _recount = new Debouncer(clock, ReloadDelay, ui, Sidebar.Reload);

        Sidebar.StreamSelected += (_, stream) => ShowStream(stream);
        List.ArticleSelected += (_, row) => OnArticleSelected(row);
        store.Changed += OnStoreChanged;
        settings.Changed += OnSettingsChanged;
        if (refresher is not null)
            refresher.StateChanged += OnRefreshStateChanged;

        // The phone opens on the Feeds root: its list is built when a stream is picked (A16).
        if (layout == SidebarLayout.Phone)
            List.Hide();
        else
            ShowStream(Sidebar.SelectedStream);
        UpdateRefreshState();
    }

    public SidebarViewModel Sidebar { get; }
    public ArticleListViewModel List { get; }
    public ArticleViewModel Article { get; }
    public SettingsViewModel Settings { get; }

    /// <summary>The desktop Settings mode while it shows (D30), or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSettingsOpen))]
    public partial SettingsModeViewModel? SettingsMode { get; private set; }

    public bool IsSettingsOpen => SettingsMode is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    public partial bool IsRefreshing { get; set; }

    /// <summary>Runs a pending debounced reload now; for tests.</summary>
    public void FlushPendingReload()
    {
        _reload.Flush();
        _recount.Flush();
    }

    public void Dispose()
    {
        _reload.Dispose();
        _recount.Dispose();
        SettingsMode?.Dispose();
        _store.Changed -= OnStoreChanged;
        _settings.Changed -= OnSettingsChanged;
        if (_refresher is not null)
            _refresher.StateChanged -= OnRefreshStateChanged;
    }

    // MARK: Flow

    private void ShowStream(ArticleStream stream)
    {
        List.Show(stream, Sidebar.Title(stream));
        UpdateEmptyState();
    }

    private void OnArticleSelected(ArticleRowViewModel? row)
    {
        if (row is not null && row.IsUnread && _settings.Current.MarkAsReadOnSelect)
            _store.SetUnread([row.Id], false);
        Article.Item = row is null ? null : _store.Item(row.Id) ?? row.Item;
        UpdateEmptyState();
    }

    private void ReloadAll()
    {
        Sidebar.Reload();
        List.Title = Sidebar.Title(List.Stream);
        List.Reload(keepSelection: true);
        UpdateEmptyState();
    }

    /// <summary>The article column's path ("3 of 9") and, with nothing selected, its empty state ("4 unread items in Unread").</summary>
    private void UpdateEmptyState()
    {
        var stream = List.Stream;
        var name = stream is ArticleStream.All ? ItemFormatting.FilterTitle(_settings.Current.Filter) : Sidebar.Title(stream);
        var filter = _settings.Current.Filter == StreamFilter.Starred ? StreamFilter.Starred : StreamFilter.Unread;
        var count = _store.Count(stream, filter);
        Article.EmptySubtitle = count > 0
            ? $"{ItemFormatting.CountText(count, filter, capitalized: false)} in {name}"
            : filter == StreamFilter.Starred ? "Nothing starred yet" : "All caught up";

        var articles = List.Articles.ToList();
        var selected = List.SelectedArticle;
        var index = selected is null ? -1 : articles.IndexOf(selected);
        Article.PositionText = index < 0 ? "" : $"{index + 1} of {articles.Count}";
        Article.Favicon = selected?.Favicon;
        Article.IsFaviconGrayscale = selected?.IsFaviconGrayscale ?? true;
    }

    private void OnStoreChanged(object? sender, StoreChangedEventArgs e) => _ui.Post(() =>
    {
        if (e.ItemIds is { } ids)
        {
            List.ApplyFlags(ids);
            if (Article.Item is { } current && ids.Contains(current.Id))
                Article.Item = _store.Item(current.Id);
            UpdateEmptyState();
            _recount.Trigger(); // counts only: rows that were just read stay listed
            return;
        }
        _reload.Trigger();
    });

    private void OnSettingsChanged(object? sender, AppSettings settings) => _ui.Post(() =>
    {
        var previous = _lastSettings;
        _lastSettings = settings;
        Settings.Apply(settings);
        Article.ApplySettings(settings);
        if (previous.Filter != settings.Filter || previous.SortNewestFirst != settings.SortNewestFirst
            || previous.GroupByDate != settings.GroupByDate || previous.DisplayFavicons != settings.DisplayFavicons
            || previous.GrayscaleFavicons != settings.GrayscaleFavicons || previous.HideEmptyFeeds != settings.HideEmptyFeeds
            || !previous.ExpandedFolders.SequenceEqual(settings.ExpandedFolders)
            || !previous.CollapsedSections.SequenceEqual(settings.CollapsedSections))
        {
            Sidebar.Reload();
            List.Title = Sidebar.Title(List.Stream);
            // The phone's pick of Unread or Starred changes the filter and has already built the list for it.
            List.ApplySettings(settings);
            UpdateEmptyState();
        }
        UpdateRefreshState();
    });

    private void OnRefreshStateChanged(object? sender, EventArgs e) => _ui.Post(UpdateRefreshState);

    private void UpdateRefreshState()
    {
        IsRefreshing = _refresher?.IsRefreshing == true;
        Sidebar.UpdateRefreshText(_settings.Current.LastRefresh, IsRefreshing, _clock.GetUtcNow());
    }

    // MARK: Commands

    private bool CanRefresh() => _refresher is not null && !IsRefreshing;

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private void Refresh() => _ = RefreshAsync();

    /// <summary>Refreshes every feed; completes when the pass ends, or at once without a refresher.</summary>
    public Task RefreshAsync() => _refresher?.RefreshAllAsync() ?? Task.CompletedTask;

    /// <summary>
    /// Back from the background (the phones): refreshes when automatic refresh is on and the last pass is older than
    /// its interval, as the timer may not have run meanwhile. True when it started one.
    /// </summary>
    public bool RefreshIfStale()
    {
        var settings = _settings.Current;
        if (IsRefreshing || settings.RefreshIntervalMinutes <= 0
            || settings.LastRefresh is { } last && _clock.GetUtcNow() - last < TimeSpan.FromMinutes(settings.RefreshIntervalMinutes))
            return false;
        _ = RefreshAsync();
        return true;
    }

    [RelayCommand]
    private void MarkAllRead() => _store.MarkAllRead(List.Stream);

    [RelayCommand]
    private void ShowFilter(StreamFilter filter) => _settings.Update(s => s.Filter = filter);

    [RelayCommand]
    private void NextArticle()
    {
        if (!List.SelectAdjacent(1))
            Sidebar.SelectAdjacent(1);
    }

    [RelayCommand]
    private void PreviousArticle() => List.SelectAdjacent(-1);

    [RelayCommand]
    private void NextFeed() => Sidebar.SelectAdjacent(1);

    [RelayCommand]
    private void PreviousFeed() => Sidebar.SelectAdjacent(-1);

    [RelayCommand]
    private void ToggleRead()
    {
        if (Article.Item is { } item)
            _store.SetUnread([item.Id], !item.Unread);
    }

    [RelayCommand]
    private void ToggleStarred()
    {
        if (Article.Item is { } item)
            _store.SetStarred([item.Id], !item.Starred);
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        if (Article.Link is { } link)
            _launcher.Open(link, _settings.Current.OpenLinksInBackground);
    }

    /// <summary>A link clicked inside the article.</summary>
    [RelayCommand]
    private void OpenLink(Uri url) => _launcher.Open(url, _settings.Current.OpenLinksInBackground);

    /// <summary>Closes the article (".").</summary>
    [RelayCommand]
    private void GoBack() => List.SelectedRow = null;

    [RelayCommand]
    private void FocusSearch() => List.FocusSearch();

    // MARK: Settings

    /// <summary>Opens Settings on <paramref name="section"/>, or on Articles &amp; Refresh; while open, moves to it.</summary>
    [RelayCommand]
    private void OpenSettings(SettingsSection? section)
    {
        if (SettingsMode is { } open)
        {
            if (section is { } wanted)
                open.Show(wanted);
            return;
        }
        SettingsMode = new SettingsModeViewModel(this, section ?? SettingsSection.Reading);
    }

    /// <summary>Back to Feeds: pages left open are discarded.</summary>
    [RelayCommand]
    public void CloseSettings()
    {
        var mode = SettingsMode;
        SettingsMode = null;
        mode?.Dispose();
    }

    // MARK: Adding and removing feeds

    // The desktop shows these in its Settings mode (A6); the phone pushes a page for each (A16).

    public AddFeedViewModel CreateAddFeed() =>
        new(_store, (input, ct) => _finder.FindAllAsync(input, ct), SaveFeed);

    /// <summary>Null when the feed is gone.</summary>
    public EditFeedViewModel? CreateEditFeed(Feed feed) =>
        _store.Feed(feed.Id) is { } current ? new EditFeedViewModel(_store, current) : null;

    /// <summary>Null when the folder is gone.</summary>
    public EditFolderViewModel? CreateEditFolder(long folderId, Action<Feed> editFeed, Action<Folder> delete) =>
        _store.Folders().FirstOrDefault(f => f.Id == folderId) is { } folder
            ? new EditFolderViewModel(_store, folder, editFeed, delete)
            : null;

    public NewFolderViewModel CreateNewFolder() => new(_store);

    public ManageFeedsViewModel CreateManageFeeds() => new(_store, _settings, _images, _ui);

    /// <summary>Import Feeds with a preview (D27, D30); an import that adds feeds starts a refresh.</summary>
    public ImportFeedsViewModel CreateImportFeeds() => new(_store, _files, () => _ = RefreshAsync());

    public ExportFeedsViewModel CreateExportFeeds() => new(_store, _files);

    public ConfirmViewModel ConfirmDeleteFolder(Folder folder) => new(
        $"Delete “{folder.Title}”?",
        "The folder is deleted. Its feeds stay and move out of the folder.",
        "Delete Folder",
        () => _store.DeleteFolder(folder.Id, deleteFeeds: false));

    /// <summary>Deletes the feed and all its articles, starred ones too, for good (A14).</summary>
    public ConfirmViewModel ConfirmRemoveFeed(Feed feed) => new(
        $"Remove “{feed.Title}”?",
        "The feed and all its articles, starred ones included, are deleted for good.",
        "Remove Feed",
        () => _store.DeleteFeed(feed.Id));

    [RelayCommand]
    private void OpenSite(Feed feed)
    {
        var url = ArticleSanitizer.Resolve(feed.SiteUrl, null)
                  ?? ArticleSanitizer.Resolve(feed.Host is { } host ? "https://" + host : null, null);
        if (url is not null)
            _launcher.Open(url, _settings.Current.OpenLinksInBackground);
    }

    [RelayCommand]
    private void ReloadIcon(Feed feed) => Sidebar.ReloadFavicon(feed);

    private Feed SaveFeed(FoundFeed found, long? folderId)
    {
        if (_refresher is not null)
            return _refresher.AddFeed(found, folderId);
        var feed = _store.AddFeed(found.Url.AbsoluteUri, found.Feed.Title ?? found.Url.Host, found.Feed.SiteUrl, folderId);
        _store.Upsert(feed.Id, found.Feed.Items);
        return feed;
    }

    /// <summary>Selects a feed just added, with its folder open and a filter that shows its articles.</summary>
    internal void ShowNewFeed(Feed feed, long? folderId)
    {
        _settings.Update(s =>
        {
            if (folderId is { } id && !s.ExpandedFolders.Contains(id))
                s.ExpandedFolders = [.. s.ExpandedFolders, id];
            // The new feed's items may all be read or old; show them anyway.
            if (s.Filter == StreamFilter.Starred)
                s.Filter = StreamFilter.Unread;
        });
        Sidebar.Select(new ArticleStream.OfFeed(feed.Id));
    }

    [RelayCommand]
    private void ChangeFontSize(int delta) =>
        _settings.Update(s => s.ArticleFontSize = delta == 0 ? AppSettings.DefaultFontSize : s.ArticleFontSize + delta);
}
