using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Formatting;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>Edit a feed: its name and its folder. The check mark saves; closing discards.</summary>
public sealed partial class EditFeedViewModel : PanelViewModel
{
    private readonly Store _store;
    private readonly Feed _feed;

    public EditFeedViewModel(Store store, Feed feed)
    {
        _store = store;
        _feed = feed;
        Name = feed.Title;
        Picker = new FolderPickerViewModel(store, feed.FolderId);
    }

    public Feed Feed => _feed;

    public string Title => _feed.Title;

    public string Subtitle => AddressText(_feed.Url);

    public string OpenSiteTitle => ItemFormatting.OpenSiteTitle(_feed);

    public FolderPickerViewModel Picker { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var updated = _feed with { Title = Name.Trim(), FolderId = Picker.Selected?.Id };
        if (updated != _feed)
            _store.UpdateFeed(updated);
        Close();
    }

    /// <summary>"host/path" without the scheme, as panels show addresses.</summary>
    public static string AddressText(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? uri.Host + (uri.PathAndQuery == "/" ? "" : uri.PathAndQuery)
            : url;
}

/// <summary>Name a new folder; saving an existing name keeps that folder.</summary>
public sealed partial class NewFolderViewModel(Store store) : PanelViewModel
{
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; } = "";

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var name = Name.Trim();
        if (store.FolderNamed(name) is null)
            store.AddFolder(name);
        Close();
    }
}

/// <summary>One feed listed in the Edit Folder panel; picking it opens that feed's panel.</summary>
public sealed record FolderFeedRow(Feed Feed)
{
    public string Title => Feed.Title;
    public string Subtitle => Feed.Url;
}

/// <summary>
/// Edit a folder: rename it, see its feeds, or delete it. Deleting asks first and moves the feeds out of the
/// folder; it never removes feeds.
/// </summary>
public sealed partial class EditFolderViewModel : PanelViewModel
{
    private readonly Store _store;
    private readonly Folder _folder;
    private readonly Action<Feed> _editFeed;
    private readonly Action<Folder> _delete;

    public EditFolderViewModel(Store store, Folder folder, Action<Feed> editFeed, Action<Folder> delete)
    {
        _store = store;
        _folder = folder;
        _editFeed = editFeed;
        _delete = delete;
        Name = folder.Title;
        Feeds = store.Feeds().Where(f => f.FolderId == folder.Id)
            .OrderBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(f => new FolderFeedRow(f)).ToList();
    }

    public string Title => _folder.Title;

    public string Subtitle => Feeds.Count == 1 ? "1 Feed" : $"{Feeds.Count} Feeds";

    public IReadOnlyList<FolderFeedRow> Feeds { get; }

    public bool HasFeeds => Feeds.Count > 0;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Name { get; set; }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        var name = Name.Trim();
        if (name != _folder.Title)
            _store.RenameFolder(_folder.Id, name);
        Close();
    }

    [RelayCommand]
    private void Delete() => _delete(_folder);

    [RelayCommand]
    private void EditFeed(FolderFeedRow row) => _editFeed(row.Feed);
}
