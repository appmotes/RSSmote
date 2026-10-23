using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Pages;

public partial class EditFolderPage : PhonePage
{
    public EditFolderPage() => InitializeComponent();

    /// <summary>Pushes the page for the folder; Save or deleting the folder pops it.</summary>
    public static void Open(MobileShell shell, long folderId)
    {
        if (shell.ViewModel is not { } vm)
            return;
        var page = new EditFolderPage();
        var edit = vm.CreateEditFolder(folderId,
            feed => EditFeedPage.Open(shell, feed, vm.Sidebar.FaviconFor(feed.Id), "Folder"),
            folder => _ = shell.ConfirmAsync(vm.ConfirmDeleteFolder(folder), () => shell.Close(page)));
        if (edit is not null)
            _ = shell.PushPanelAsync(page, edit);
    }

    private void OnFeedClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is FolderFeedRow row && DataContext is EditFolderViewModel edit)
            edit.EditFeedCommand.Execute(row);
    }
}
