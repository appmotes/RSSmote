using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

public sealed partial class FolderChoiceViewModel(Folder folder) : ObservableObject
{
    public Folder Folder { get; } = folder;

    [ObservableProperty]
    public partial bool IsOn { get; set; }
}

/// <summary>
/// Picks at most one folder for a feed, or none: the folders as radio rows plus a field that creates a new one
/// and picks it. Used by the Add Feed and Edit Feed panels.
/// </summary>
public sealed partial class FolderPickerViewModel : ObservableObject
{
    private readonly Store _store;

    public FolderPickerViewModel(Store store, long? folderId)
    {
        _store = store;
        Selected = folderId is { } id ? store.Folders().FirstOrDefault(f => f.Id == id) : null;
        Reload();
    }

    public ObservableCollection<FolderChoiceViewModel> Folders { get; } = [];

    [ObservableProperty]
    public partial Folder? Selected { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddFolderCommand))]
    public partial string NewFolderName { get; set; } = "";

    public void Reload()
    {
        Folders.Clear();
        foreach (var folder in _store.Folders().OrderBy(f => f.Title, StringComparer.CurrentCultureIgnoreCase))
            Folders.Add(new FolderChoiceViewModel(folder) { IsOn = folder.Id == Selected?.Id });
    }

    [RelayCommand]
    private void Toggle(FolderChoiceViewModel choice)
    {
        Selected = Selected?.Id == choice.Folder.Id ? null : choice.Folder;
        foreach (var row in Folders)
            row.IsOn = row.Folder.Id == Selected?.Id;
    }

    /// <summary>No folder: the "None" chip.</summary>
    [RelayCommand]
    private void SelectNone()
    {
        Selected = null;
        foreach (var row in Folders)
            row.IsOn = false;
    }

    private bool CanAddFolder() => !string.IsNullOrWhiteSpace(NewFolderName);

    [RelayCommand(CanExecute = nameof(CanAddFolder))]
    private void AddFolder()
    {
        var name = NewFolderName.Trim();
        Selected = _store.FolderNamed(name) ?? _store.AddFolder(name);
        NewFolderName = "";
        Reload();
    }
}
