using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RSSmote.App.ViewModels;

/// <summary>One radio item in a theme menu (D22): an appearance, or a palette for the light or dark slot.</summary>
public sealed partial class ThemeChoiceViewModel(string header, object key, IRelayCommand command) : ObservableObject
{
    public string Header { get; } = header;

    /// <summary>The <c>AppTheme</c> or palette id this item stands for.</summary>
    public object Key { get; } = key;

    public IRelayCommand Command { get; } = command;

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    public override string ToString() => Header;
}
