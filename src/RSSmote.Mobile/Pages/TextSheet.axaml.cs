using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Pages;

public partial class TextSheet : ContentPage
{
    public TextSheet() => InitializeComponent();

    private void OnScrimClick(object? sender, RoutedEventArgs e) => _ = MobileShell.Of(this)?.CloseSheetAsync();
}
