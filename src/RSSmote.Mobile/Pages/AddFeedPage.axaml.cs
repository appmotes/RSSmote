using Avalonia.Controls;
using Avalonia.Input;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Pages;

/// <summary>Return in the address field searches.</summary>
public partial class AddFeedPage : PhonePage
{
    public AddFeedPage()
    {
        InitializeComponent();
        Address.KeyDown += OnAddressKeyDown;
        NavigatedTo += (_, _) => Address.Focus();
    }

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not AddFeedViewModel add || !add.SearchCommand.CanExecute(null))
            return;
        e.Handled = true;
        // The keyboard goes, so the results and Add Feed are in view.
        TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
        _ = add.SearchCommand.ExecuteAsync(null);
    }
}
