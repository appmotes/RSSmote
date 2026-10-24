using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Pages;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Views;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    private MobileShell? Shell => MobileShell.Of(this);

    private void OnAddFeedClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is { ViewModel: { } vm } shell)
            _ = shell.PushPanelAsync(new Pages.AddFeedPage(), vm.CreateAddFeed());
    }

    private void OnManageFeedsClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is { ViewModel: { } vm } shell)
            _ = shell.PushAsync(new ManageFeedsPage(vm.CreateManageFeeds()));
    }

    private void OnImportClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is { ViewModel: { } vm } shell)
            _ = shell.PushPanelAsync(new ImportFeedsPage(), vm.CreateImportFeeds());
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is { ViewModel: { } vm } shell)
            _ = shell.PushPanelAsync(new ExportFeedsPage(), vm.CreateExportFeeds());
    }

    private void OnAppearanceClick(object? sender, RoutedEventArgs e)
    {
        if (Shell is { ViewModel: { } vm } shell)
            _ = shell.PushAsync(new AppearancePage { DataContext = vm.Settings });
    }
}
