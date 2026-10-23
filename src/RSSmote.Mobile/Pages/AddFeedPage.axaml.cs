using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Pages;

/// <summary>Return in the address field searches; the line under it says what the search is doing.</summary>
public partial class AddFeedPage : PhonePage
{
    private const string Prompt =
        "Paste the address of a feed or a website. RSSmote checks it and shows what it finds before anything is saved.";

    private AddFeedViewModel? _add;

    public AddFeedPage()
    {
        InitializeComponent();
        Hint.Text = Prompt;
        Address.KeyDown += OnAddressKeyDown;
        DataContextChanged += (_, _) => Attach(DataContext as AddFeedViewModel);
        NavigatedTo += (_, _) => Address.Focus();
    }

    private void Attach(AddFeedViewModel? add)
    {
        if (_add is not null)
            _add.PropertyChanged -= OnChanged;
        _add = add;
        if (_add is not null)
            _add.PropertyChanged += OnChanged;
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_add is not { } add || e.PropertyName is not (nameof(AddFeedViewModel.IsSearching) or nameof(AddFeedViewModel.Error)))
            return;
        Hint.Text = add.IsSearching ? "Searching…" : add.Error ?? Prompt;
    }

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _add is not { } add || !add.SearchCommand.CanExecute(null))
            return;
        e.Handled = true;
        // The keyboard goes, so the results and Add Feed are in view.
        TopLevel.GetTopLevel(this)?.FocusManager?.Focus(null);
        _ = add.SearchCommand.ExecuteAsync(null);
    }
}
