using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views;

public partial class ArticleListView : UserControl
{
    private ArticleListViewModel? _list;

    public ArticleListView()
    {
        InitializeComponent();
        Rows.ContainerPrepared += OnContainerPrepared;
        // A right click opens the row's menu without selecting the row (A6).
        Rows.AddHandler(PointerPressedEvent, OnRowPointerPressed, RoutingStrategies.Tunnel);
        Rows.AddHandler(ContextRequestedEvent, OnRowContextRequested);
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

    /// <summary>The context menu last opened on a row; for tests.</summary>
    internal ContextMenu? OpenMenu { get; private set; }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
            e.Handled = true;
    }

    private void OnRowContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is not Control source || DataContext is not ShellViewModel shell
            || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: ArticleRowViewModel row } item)
            return;
        OpenMenu = new ContextMenu { ItemsSource = MenuItems(shell, row) };
        OpenMenu.Open(item);
        e.Handled = true;
    }

    /// <summary>Read, the unread above and below as read, star, and the browser; the keys name the same actions on the open article.</summary>
    internal static List<Control> MenuItems(ShellViewModel shell, ArticleRowViewModel row)
    {
        static MenuItem Item(string header, System.Windows.Input.ICommand command, ArticleRowViewModel row, Key? key = null) =>
            new() { Header = header, Command = command, CommandParameter = row, InputGesture = key is { } k ? new KeyGesture(k) : null };

        var above = Item("Mark Above as Read", shell.MarkAboveReadCommand, row);
        above.IsEnabled = shell.List.UnreadBeside(row, below: false).Count > 0;
        var below = Item("Mark Below as Read", shell.MarkBelowReadCommand, row);
        below.IsEnabled = shell.List.UnreadBeside(row, below: true).Count > 0;
        return
        [
            Item(row.IsUnread ? "Mark as Read" : "Mark as Unread", shell.ToggleRowReadCommand, row, Key.M),
            above,
            below,
            new Separator(),
            Item(row.IsStarred ? "Unstar" : "Star", shell.ToggleRowStarredCommand, row, Key.S),
            new Separator(),
            Item("Open in Browser", shell.OpenRowInBrowserCommand, row, Key.B),
        ];
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        var header = e.Container.DataContext is DateHeaderViewModel;
        e.Container.Classes.Set("header", header);
        e.Container.Focusable = !header;
        if (e.Container.DataContext is ArticleRowViewModel row && DataContext is ShellViewModel shell)
            shell.List.RequestImages(row);
    }
}
