using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views.Panels;

public partial class AddFeedView : UserControl
{
    private AddFeedViewModel? _model;

    public AddFeedView() => InitializeComponent();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_model is not null)
            _model.PropertyChanged -= OnModelChanged;
        _model = DataContext as AddFeedViewModel;
        if (_model is not null)
            _model.PropertyChanged += OnModelChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusPage();
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AddFeedViewModel.Page))
            FocusPage();
    }

    /// <summary>The page's text field takes the keyboard, so typing and Escape work at once.</summary>
    private void FocusPage() => Dispatcher.UIThread.Post(() =>
    {
        switch (_model?.Page)
        {
            case AddFeedPage.Search:
                AddressBox.Focus();
                break;
            case AddFeedPage.Folders:
                Picker.FocusNewFolder();
                break;
            default:
                Focusable = true;
                Focus();
                break;
        }
    }, DispatcherPriority.Loaded);
}
