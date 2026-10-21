using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views;

public partial class ShellView : UserControl
{
    public ShellView() => InitializeComponent();

    /// <summary>A click beside the open panel, or Escape inside it, closes it.</summary>
    private void OnPanelDismissRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        (DataContext as ShellViewModel)?.Panel?.CloseCommand.Execute(null);
    }
}
