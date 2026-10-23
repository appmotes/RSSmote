using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Pages;

public partial class ArticlePage : ContentPage
{
    public ArticlePage() => InitializeComponent();

    public ArticlePage(ShellViewModel shell) : this() => DataContext = shell;

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    /// <summary>Previous and next stay inside the list; at either end the button does nothing (D26).</summary>
    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Shell?.List.SelectAdjacent(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => Shell?.List.SelectAdjacent(1);

    private void OnLinkClicked(object? sender, ArticleLinkEventArgs e)
    {
        e.Handled = true;
        Shell?.OpenLinkCommand.Execute(e.Url);
    }
}
