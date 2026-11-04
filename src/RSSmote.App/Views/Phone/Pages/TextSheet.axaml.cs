using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.Views.Phone.Shell;

namespace RSSmote.App.Views.Phone.Pages;

public partial class TextSheet : ContentPage
{
    public TextSheet() => InitializeComponent();

    /// <summary>The sheet itself, without the area above it.</summary>
    public Border Body => Sheet;

    private void OnScrimClick(object? sender, RoutedEventArgs e) => _ = MobileShell.Of(this)?.CloseSheetAsync();
}
