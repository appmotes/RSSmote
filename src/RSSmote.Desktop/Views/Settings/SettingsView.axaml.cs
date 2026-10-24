using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views.Settings;

public partial class SettingsView : UserControl
{
    private SettingsModeViewModel? _mode;

    public SettingsView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_mode is not null)
            _mode.PropertyChanged -= OnModeChanged;
        _mode = DataContext as SettingsModeViewModel;
        if (_mode is not null)
            _mode.PropertyChanged += OnModeChanged;
    }

    /// <summary>Every page starts at its top.</summary>
    private void OnModeChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsModeViewModel.Page))
            Scroller.Offset = default;
    }

    /// <summary>A click beside the dialog cancels it.</summary>
    private void OnConfirmDismissRequested(object? sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _mode?.Confirm?.CloseCommand.Execute(null);
    }
}
