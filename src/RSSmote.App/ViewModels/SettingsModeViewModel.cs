using System.Windows.Input;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.Core.Models;

namespace RSSmote.App.ViewModels;

/// <summary>An item of the desktop Settings menu, in menu order (D30).</summary>
public enum SettingsSection
{
    AddFeed,
    ManageFeeds,
    ImportFeeds,
    ExportFeeds,
    Appearance,
    Text,
    Reading,
}

/// <summary>Appearance: the mode, a palette per slot and the favicon switches.</summary>
public sealed record AppearancePageViewModel(SettingsViewModel Settings);

/// <summary>Text: a size for each pane (D31).</summary>
public sealed record TextPageViewModel(SettingsViewModel Settings);

/// <summary>Articles &amp; Refresh: the reading switches and the two refresh pickers.</summary>
public sealed record ReadingPageViewModel(SettingsViewModel Settings);

/// <summary>
/// The desktop Settings mode (D30): one menu section at a time; under Manage Feeds the edit pages opened from it stack
/// on top, and each closes back to the page below. A confirmation shows over the page. Disposing the mode disposes
/// every page, so Manage Feeds stops following the library.
/// </summary>
public sealed partial class SettingsModeViewModel : ObservableObject, IDisposable
{
    private readonly ShellViewModel _shell;
    private readonly List<object> _pages = [];

    internal SettingsModeViewModel(ShellViewModel shell, SettingsSection section)
    {
        _shell = shell;
        Show(section);
    }

    /// <summary>The preferences Appearance and Articles &amp; Refresh show, and the version under the menu.</summary>
    public SettingsViewModel Settings => _shell.Settings;

    /// <summary>The menu item shown as chosen; edit pages keep Manage Feeds chosen.</summary>
    [ObservableProperty]
    public partial SettingsSection Section { get; private set; }

    /// <summary>The section's own page, or the last page opened from it.</summary>
    public object? Page => _pages.Count > 0 ? _pages[^1] : null;

    /// <summary>The centred title in the bar.</summary>
    public string Title => Page switch
    {
        AddFeedViewModel => "Add Feed",
        ManageFeedsViewModel => "Manage Feeds",
        EditFeedViewModel edit => edit.Title,
        EditFolderViewModel edit => edit.Title,
        NewFolderViewModel => "New Folder",
        ImportFeedsViewModel => "Import Feeds",
        ExportFeedsViewModel => "Export Feeds",
        AppearancePageViewModel => "Appearance",
        TextPageViewModel => "Text",
        _ => "Articles & Refresh",
    };

    /// <summary>The bar's action at the right: New Folder on Manage Feeds, Save on the pages that edit.</summary>
    public string? ActionTitle => Page switch
    {
        ManageFeedsViewModel => "New Folder",
        EditFeedViewModel or EditFolderViewModel or NewFolderViewModel => "Save",
        _ => null,
    };

    public ICommand? ActionCommand => Page switch
    {
        ManageFeedsViewModel => NewFolderCommand,
        EditFeedViewModel edit => edit.SaveCommand,
        EditFolderViewModel edit => edit.SaveCommand,
        NewFolderViewModel add => add.SaveCommand,
        _ => null,
    };

    public bool HasAction => ActionTitle is not null;

    /// <summary>A page is open over the section's own; the bar shows back.</summary>
    public bool CanGoBack => _pages.Count > 1;

    /// <summary>Remove Feed… or Delete Folder…, asking over the page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConfirming))]
    public partial ConfirmViewModel? Confirm { get; private set; }

    public bool IsConfirming => Confirm is not null;

    public void Dispose() => Clear();

    /// <summary>Shows a menu item's page. Its own item again returns from a page opened under it.</summary>
    [RelayCommand]
    public void Show(SettingsSection section)
    {
        if (section == Section && _pages.Count > 0)
        {
            PopAbove(0);
            return;
        }
        Clear();
        Section = section;
        _pages.Add(section switch
        {
            SettingsSection.AddFeed => NewAddFeed(),
            SettingsSection.ManageFeeds => _shell.CreateManageFeeds(),
            SettingsSection.ImportFeeds => NewImportFeeds(),
            SettingsSection.ExportFeeds => _shell.CreateExportFeeds(),
            SettingsSection.Appearance => new AppearancePageViewModel(_shell.Settings),
            SettingsSection.Text => new TextPageViewModel(_shell.Settings),
            _ => new ReadingPageViewModel(_shell.Settings),
        });
        OnPageChanged();
    }

    /// <summary>Leaves an edit page without saving, for the page below.</summary>
    [RelayCommand]
    private void Back()
    {
        if (_pages.Count > 1)
            PopAbove(_pages.Count - 2);
    }

    /// <summary>Back to Feeds.</summary>
    [RelayCommand]
    private void Close() => _shell.CloseSettings();

    /// <summary>
    /// After a save Add Feed starts again in the same folder and says what was added, so feeds go in one after another.
    /// The reading window selects the feed for when Settings close.
    /// </summary>
    private AddFeedViewModel NewAddFeed(Feed? added = null)
    {
        var add = _shell.CreateAddFeed();
        add.Added = added?.Title;
        if (added?.FolderId is { } folderId && add.Picker.Folders.FirstOrDefault(f => f.Folder.Id == folderId) is { } folder)
            add.Picker.ToggleCommand.Execute(folder);
        Feed? saved = null;
        add.Saved += (_, e) =>
        {
            saved = e.Feed;
            _shell.ShowNewFeed(e.Feed, e.FolderId);
        };
        add.Closed += (_, _) =>
        {
            if (saved is null || _pages.Count == 0 || !ReferenceEquals(_pages[0], add))
                return;
            _pages[0] = NewAddFeed(saved);
            OnPageChanged();
        };
        return add;
    }

    /// <summary>After an import Manage Feeds shows what arrived.</summary>
    private ImportFeedsViewModel NewImportFeeds()
    {
        var import = _shell.CreateImportFeeds();
        import.Closed += (_, _) => Show(SettingsSection.ManageFeeds);
        return import;
    }

    [RelayCommand]
    private void EditFeed(ManagedFeedViewModel row) => OpenEditFeed(row.Feed, row.Favicon);

    private void OpenEditFeed(Feed feed, Bitmap? favicon)
    {
        if (_shell.CreateEditFeed(feed) is not { } edit)
            return;
        edit.Favicon = favicon ?? _shell.Sidebar.FaviconFor(feed.Id);
        Push(edit);
    }

    [RelayCommand]
    private void EditFolder(Folder folder)
    {
        EditFolderViewModel? edit = null;
        edit = _shell.CreateEditFolder(folder.Id,
            feed => OpenEditFeed(feed, null),
            f => Ask(_shell.ConfirmDeleteFolder(f), () => Pop(edit!)));
        if (edit is not null)
            Push(edit);
    }

    [RelayCommand]
    private void NewFolder() => Push(_shell.CreateNewFolder());

    /// <summary>Asks first; once the feed is gone, so is its page.</summary>
    [RelayCommand]
    private void RemoveFeed(Feed feed) => Ask(_shell.ConfirmRemoveFeed(feed), () =>
    {
        if (_pages.OfType<EditFeedViewModel>().FirstOrDefault(e => e.Feed.Id == feed.Id) is { } edit)
            Pop(edit);
    });

    [RelayCommand]
    private void OpenSite(Feed feed) => _shell.OpenSiteCommand.Execute(feed);

    [RelayCommand]
    private void ReloadIcon(Feed feed) => _shell.ReloadIconCommand.Execute(feed);

    private void Ask(ConfirmViewModel confirm, Action confirmed)
    {
        Confirm?.Dispose();
        confirm.Confirmed += (_, _) => confirmed();
        confirm.Closed += (_, _) =>
        {
            if (ReferenceEquals(Confirm, confirm))
                Confirm = null;
        };
        Confirm = confirm;
    }

    /// <summary>Opens a page over the current one; the page closing (Save) returns to the one below.</summary>
    private void Push(PanelViewModel page)
    {
        page.Closed += (_, _) => Pop(page);
        _pages.Add(page);
        OnPageChanged();
    }

    /// <summary>Takes <paramref name="page"/> and every page above it away. The section's own page stays.</summary>
    private void Pop(object page)
    {
        var index = _pages.IndexOf(page);
        if (index > 0)
            PopAbove(index - 1);
    }

    private void PopAbove(int index)
    {
        if (_pages.Count <= index + 1)
            return;
        var removed = _pages.Skip(index + 1).ToList();
        _pages.RemoveRange(index + 1, removed.Count);
        foreach (var page in removed)
            (page as IDisposable)?.Dispose();
        OnPageChanged();
    }

    private void Clear()
    {
        Confirm?.Dispose();
        Confirm = null;
        foreach (var page in _pages)
            (page as IDisposable)?.Dispose();
        _pages.Clear();
    }

    private void OnPageChanged()
    {
        OnPropertyChanged(nameof(Page));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ActionTitle));
        OnPropertyChanged(nameof(ActionCommand));
        OnPropertyChanged(nameof(HasAction));
        OnPropertyChanged(nameof(CanGoBack));
    }
}
