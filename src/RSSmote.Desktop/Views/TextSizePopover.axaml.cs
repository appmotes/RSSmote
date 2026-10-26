using Avalonia.Controls;
using Avalonia.Input;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views;

public partial class TextSizePopover : UserControl
{
    public TextSizePopover() => InitializeComponent();

    /// <summary>
    /// ⌘/Ctrl + / − / 0 while the popover has the keyboard: the popover is its own window, so the main window's
    /// shortcuts do not see the keys.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Handled || e.KeyModifiers != command || DataContext is not SettingsViewModel settings)
            return;
        int? delta = e.Key switch
        {
            Key.OemPlus or Key.Add => 1,
            Key.OemMinus or Key.Subtract => -1,
            Key.D0 => 0,
            _ => null,
        };
        if (delta is { } step)
        {
            settings.ChangeTextSize(settings.SelectedPane, step);
            e.Handled = true;
        }
    }
}
