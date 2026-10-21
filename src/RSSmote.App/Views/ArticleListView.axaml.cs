using Avalonia.Controls;
using RSSmote.App.ViewModels;

namespace RSSmote.App.Views;

public partial class ArticleListView : UserControl
{
    private ArticleListViewModel? _list;

    public ArticleListView()
    {
        InitializeComponent();
        Rows.ContainerPrepared += OnContainerPrepared;
        DataContextChanged += (_, _) => Attach((DataContext as ShellViewModel)?.List);
    }

    /// <summary>Cmd/Ctrl+F moves the keyboard into the filter box; Escape there hands it back to the list.</summary>
    private void Attach(ArticleListViewModel? list)
    {
        if (_list is not null)
        {
            _list.SearchFocusRequested -= OnSearchFocusRequested;
            _list.SearchEnded -= OnSearchEnded;
        }
        _list = list;
        if (_list is not null)
        {
            _list.SearchFocusRequested += OnSearchFocusRequested;
            _list.SearchEnded += OnSearchEnded;
        }
    }

    private void OnSearchFocusRequested(object? sender, EventArgs e)
    {
        Search.Focus();
        Search.SelectAll();
    }

    private void OnSearchEnded(object? sender, EventArgs e) => Rows.Focus();

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        var header = e.Container.DataContext is DateHeaderViewModel;
        e.Container.Classes.Set("header", header);
        e.Container.Focusable = !header;
        if (e.Container.DataContext is ArticleRowViewModel row && DataContext is ShellViewModel shell)
            shell.List.RequestImages(row);
    }
}
