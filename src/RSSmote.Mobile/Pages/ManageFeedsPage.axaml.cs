using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Pages;

/// <summary>Owns its view-model, which follows the library until the page leaves the stack.</summary>
public partial class ManageFeedsPage : PhonePage, IDisposable
{
    public ManageFeedsPage() => InitializeComponent();

    public ManageFeedsPage(ManageFeedsViewModel manage) : this() => DataContext = manage;

    public void Dispose() => (DataContext as ManageFeedsViewModel)?.Dispose();

    private MobileShell? Shell => MobileShell.Of(this);

    private void OnFeedClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is ManagedFeedViewModel row && Shell is { } shell)
            EditFeedPage.Open(shell, row.Feed, row.Favicon, "Feeds");
    }

    private void OnEditFolderClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is Folder folder && Shell is { } shell)
            EditFolderPage.Open(shell, folder.Id);
    }

    private void OnNewFolderClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is { ViewModel: { } vm } shell)
            _ = shell.PushPanelAsync(new NewFolderPage(), vm.CreateNewFolder());
    }
}
