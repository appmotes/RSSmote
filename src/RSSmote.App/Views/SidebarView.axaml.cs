using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Templates;
using RSSmote.App.ViewModels;

namespace RSSmote.App.Views;

public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
        // A click on a folder's chevron opens or closes the folder instead of selecting it.
        Rows.AddHandler(PointerPressedEvent, OnRowPointerPressed, RoutingStrategies.Tunnel);
        Rows.AddHandler(ContextRequestedEvent, OnRowContextRequested);
    }

    /// <summary>The context menu last opened on a row; for tests.</summary>
    internal ContextMenu? OpenMenu { get; private set; }

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Control source || DataContext is not ShellViewModel shell)
            return;
        // A right click opens the row's menu without selecting the row.
        if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            return;
        }
        var disclosure = source.GetSelfAndVisualAncestors().TakeWhile(v => v is not SidebarRow)
            .OfType<Control>().FirstOrDefault(c => c.Name == "PART_Disclosure");
        if (disclosure?.DataContext is not SidebarRowViewModel row)
            return;
        shell.Sidebar.Toggle(row);
        e.Handled = true;
    }

    private void OnRowContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (e.Source is not Control source || DataContext is not ShellViewModel shell
            || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: SidebarRowViewModel row } item)
            return;
        var items = MenuItems(shell, row);
        if (items.Count == 0)
            return;
        OpenMenu = new ContextMenu { ItemsSource = items };
        OpenMenu.Open(item);
        e.Handled = true;
    }

    /// <summary>Feed rows: open site, reload icon, edit, remove. Folder rows: edit. Others have no menu.</summary>
    internal static List<Control> MenuItems(ShellViewModel shell, SidebarRowViewModel row)
    {
        static MenuItem Item(string header, System.Windows.Input.ICommand command, object? parameter) =>
            new() { Header = header, Command = command, CommandParameter = parameter };

        if (row.Feed is { } feed)
        {
            return
            [
                Item(row.OpenSiteTitle, shell.OpenSiteCommand, feed),
                new Separator(),
                Item("Reload Icon", shell.ReloadIconCommand, feed),
                new Separator(),
                Item("Edit…", shell.EditFeedCommand, feed),
                Item("Remove Feed…", shell.RemoveFeedCommand, feed),
            ];
        }
        if (row.IsFolder && row.FolderId is { } folderId)
            return [Item("Edit…", shell.EditFolderCommand, folderId)];
        return [];
    }
}
