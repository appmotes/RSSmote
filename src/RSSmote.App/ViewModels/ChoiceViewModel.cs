using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RSSmote.App.ViewModels;

/// <summary>One radio item in a menu or a settings list: an appearance, a palette (D22), an interval.</summary>
public sealed partial class ChoiceViewModel(string header, object key, IRelayCommand command, ThemeVariant? variant = null)
    : ObservableObject
{
    public string Header { get; } = header;

    /// <summary>The value this item stands for: an <c>AppTheme</c>, a palette id, a number of minutes or days.</summary>
    public object Key { get; } = key;

    public IRelayCommand Command { get; } = command;

    /// <summary>A palette's variant, so a view can draw its swatch in that palette's colours.</summary>
    public ThemeVariant? Variant { get; } = variant;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    public override string ToString() => Header;
}
