using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Pages;

public partial class TextSheet : ContentPage
{
    public TextSheet() => InitializeComponent();

    /// <summary>The sheet itself, without the area above it.</summary>
    public Border Body => Sheet;

    private void OnScrimClick(object? sender, RoutedEventArgs e) => _ = MobileShell.Of(this)?.CloseSheetAsync();
}
