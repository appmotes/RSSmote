using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Formatting;
using RSSmote.Core.Discovery;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>A feed found at the searched address; on by default unless it is already stored.</summary>
public sealed partial class FoundFeedRowViewModel(FoundFeed found, bool saved) : ObservableObject
{
    public FoundFeed Found { get; } = found;
    public string Title { get; } = found.Feed.Title ?? found.Url.Host;

    /// <summary>"24 articles · nightsky.example/feed.xml": the line under a found feed.</summary>
    public string Detail { get; } = (saved ? "Already added · " : ItemFormatting.ArticleCountText(found.Feed.Items.Count) + " · ")
                                    + EditFeedViewModel.AddressText(found.Url.AbsoluteUri);

    public bool IsSaved { get; } = saved;

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

/// <summary>
/// Add Feed (D27, D30): an address is searched for feeds (the page itself or the feeds it links to), one found feed
/// is picked, optionally put in a folder, and saved. Only plain feed and site addresses; no service shortcuts.
/// </summary>
public sealed partial class AddFeedViewModel : PanelViewModel
{
    private readonly Store _store;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<FoundFeed>>> _find;
    private readonly Func<FoundFeed, long?, Feed> _save;
    private CancellationTokenSource? _search;

    public AddFeedViewModel(Store store, Func<string, CancellationToken, Task<IReadOnlyList<FoundFeed>>> find,
                              Func<FoundFeed, long?, Feed> save)
    {
        _store = store;
        _find = find;
        _save = save;
        Picker = new FolderPickerViewModel(store, null);
    }

    /// <summary>Raised with the last saved feed, just before <see cref="Closed"/>.</summary>
    public event EventHandler<(Feed Feed, long? FolderId)>? Saved;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial string Address { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    [NotifyCanExecuteChangedFor(nameof(SearchCommand))]
    public partial bool IsSearching { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError), nameof(Hint))]
    public partial string? Error { get; private set; }

    /// <summary>The title of the feed saved just before this page, which the hint names.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint))]
    public partial string? Added { get; set; }

    /// <summary>The line under the address: what to paste or what was just added, that a search runs, or why it failed.</summary>
    public string Hint => IsSearching ? "Searching…"
        : Error ?? (Added is { } title ? $"Added “{title}”. Paste the next address to add another." : Prompt);

    public const string Prompt =
        "Paste the address of a feed or a website. RSSmote checks it and shows what it finds before anything is saved.";

    /// <summary>"Found 2 feeds": the group header over the results.</summary>
    public string FoundText => Results.Count == 1 ? "Found 1 feed" : $"Found {Results.Count} feeds";

    public bool HasResults => Results.Count > 0;

    public bool HasError => Error is not null;

    public ObservableCollection<FoundFeedRowViewModel> Results { get; } = [];

    /// <summary>The folder new feeds go into.</summary>
    public FolderPickerViewModel Picker { get; }

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
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(FoundText));
        OnPropertyChanged(nameof(HasResults));
    }

    private void OnResultChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanSave));
        SaveCommand.NotifyCanExecuteChanged();
    }

    /// <summary>One found feed is picked, as radio rows (D27).</summary>
    [RelayCommand]
    private void PickResult(FoundFeedRowViewModel row)
    {
        if (row.IsSaved)
            return;
        foreach (var other in Results)
            other.IsOn = ReferenceEquals(other, row);
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
}
