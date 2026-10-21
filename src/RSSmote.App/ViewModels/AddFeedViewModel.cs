using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

public enum AddFeedPage
{
    Search,
    Results,
    Folders,
}

/// <summary>A feed found at the searched address; on by default unless it is already stored.</summary>
public sealed partial class FoundFeedRowViewModel(FoundFeed found, bool saved) : ObservableObject
{
    public FoundFeed Found { get; } = found;
    public string Title { get; } = found.Feed.Title ?? found.Url.Host;
    public string Subtitle { get; } = saved ? "Already added · " + found.Url.AbsoluteUri : found.Url.AbsoluteUri;
    public bool IsSaved { get; } = saved;

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

/// <summary>
/// The Add Feed panel (A6): an address is searched for feeds (the page itself or the feeds it links to), the
/// found feeds are picked, optionally put in a folder, and saved. OPML import starts here too. Only plain
/// feed and site addresses; no service shortcuts.
/// </summary>
public sealed partial class AddFeedViewModel : PanelViewModel
{
    private readonly Store _store;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<FoundFeed>>> _find;
    private readonly Func<FoundFeed, long?, Feed> _save;
    private readonly Func<Task<string?>> _importOpml;
    private CancellationTokenSource? _search;

    public AddFeedViewModel(Store store, Func<string, CancellationToken, Task<IReadOnlyList<FoundFeed>>> find,
                              Func<FoundFeed, long?, Feed> save, Func<Task<string?>> importOpml)
    {
        _store = store;
        _find = find;
        _save = save;
        _importOpml = importOpml;
        Picker = new FolderPickerViewModel(store, null);
        Picker.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FolderPickerViewModel.Selected))
                OnPropertyChanged(nameof(FolderTitle));
        };
    }

    /// <summary>Raised with the last saved feed, just before <see cref="Closed"/>.</summary>
    public event EventHandler<(Feed Feed, long? FolderId)>? Saved;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchPage), nameof(IsResultsPage), nameof(IsFoldersPage))]
    public partial AddFeedPage Page { get; private set; }

    public bool IsSearchPage => Page == AddFeedPage.Search;
    public bool IsResultsPage => Page == AddFeedPage.Results;
    public bool IsFoldersPage => Page == AddFeedPage.Folders;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial string Address { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchTitle), nameof(SearchButtonTitle))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial bool IsSearching { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchTitle), nameof(HasError))]
    public partial string? Error { get; private set; }

    public string SearchTitle => IsSearching ? "Searching…" : Error ?? "Search";

    /// <summary>The search button's label; an error shows as a hint under the field instead.</summary>
    public string SearchButtonTitle => IsSearching ? "Searching…" : "Search";

    /// <summary>"1 FEED FOUND" over the results (upper-case in the string, D6).</summary>
    public string FoundTitle => Results.Count == 1 ? "1 FEED FOUND" : $"{Results.Count} FEEDS FOUND";

    public bool HasError => Error is not null;

    [ObservableProperty]
    public partial string ImportTitle { get; private set; } = "Import from OPML…";

    public ObservableCollection<FoundFeedRowViewModel> Results { get; } = [];

    [ObservableProperty]
    public partial string ResultsTitle { get; private set; } = "";

    /// <summary>The folder new feeds go into.</summary>
    public FolderPickerViewModel Picker { get; }

    public string FolderTitle => Picker.Selected is { } folder ? "Folder: " + folder.Title : "Add to Folder…";

    public bool CanSave => Results.Any(r => r.IsOn);

    public override void Dispose()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
    }

    private bool CanSearch() => !IsSearching && !string.IsNullOrWhiteSpace(Address);

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task Search()
    {
        _search?.Cancel();
        _search?.Dispose();
        var search = _search = new CancellationTokenSource();
        Error = null;
        IsSearching = true;
        try
        {
            var found = await _find(Address.Trim(), search.Token);
            if (search.IsCancellationRequested)
                return;
            ShowResults(found);
        }
        catch (Exception e) when (e is FeedException or HttpRequestException or TaskCanceledException or IOException)
        {
            if (!search.IsCancellationRequested)
                Error = e is TaskCanceledException ? "The server did not respond in time." : e.Message;
        }
        finally
        {
            if (ReferenceEquals(_search, search))
                IsSearching = false;
        }
    }

    private void ShowResults(IReadOnlyList<FoundFeed> found)
    {
        foreach (var row in Results)
            row.PropertyChanged -= OnResultChanged;
        Results.Clear();
        var saved = _store.Feeds().Select(f => f.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var feed in found)
        {
            var row = new FoundFeedRowViewModel(feed, saved.Contains(feed.Url.AbsoluteUri));
            row.PropertyChanged += OnResultChanged;
            Results.Add(row);
        }
        // Only the first new feed is picked, as an address often lists comment or category feeds too.
        if (Results.FirstOrDefault(r => !r.IsSaved) is { } first)
            first.IsOn = true;
        var url = found[0].Url;
        ResultsTitle = url.Host + (url.AbsolutePath == "/" ? "" : url.AbsolutePath);
        Page = AddFeedPage.Results;
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(FoundTitle));
    }

    private void OnResultChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ToggleResult(FoundFeedRowViewModel row)
    {
        if (!row.IsSaved)
            row.IsOn = !row.IsOn;
    }

    [RelayCommand]
    private void Back() => Page = Page switch
    {
        AddFeedPage.Folders => AddFeedPage.Results,
        _ => AddFeedPage.Search,
    };

    /// <summary>Enter on the page itself: search, save or go back from the folders, whichever the page offers.</summary>
    [RelayCommand]
    private async Task Enter()
    {
        switch (Page)
        {
            case AddFeedPage.Search when SearchCommand.CanExecute(null):
                await SearchCommand.ExecuteAsync(null);
                break;
            case AddFeedPage.Results when CanSave:
                Save();
                break;
            case AddFeedPage.Folders:
                Back();
                break;
        }
    }

    [RelayCommand]
    private void ShowFolders()
    {
        Picker.Reload();
        Page = AddFeedPage.Folders;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Feed? last = null;
        foreach (var row in Results.Where(r => r.IsOn && !r.IsSaved))
            last = _save(row.Found, Picker.Selected?.Id);
        if (last is not null)
            Saved?.Invoke(this, (last, Picker.Selected?.Id));
        Close();
    }

    [RelayCommand]
    private async Task ImportOpml()
    {
        if (await _importOpml() is { } result)
            ImportTitle = result;
    }
}
