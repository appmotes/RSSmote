using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Views;

public partial class FeedsView : UserControl
{
    /// <summary>Feeds inside a folder sit one step in from the folder's title.</summary>
    public static readonly IValueConverter IndentConverter =
        new FuncValueConverter<bool, Thickness>(nested => nested ? new Thickness(30, 0, 0, 0) : default);

    private readonly ObservableCollection<FeedGroup> _groups = [];
    private SidebarViewModel? _sidebar;
    private bool _regroupPending;

    public FeedsView()
    {
        InitializeComponent();
        Groups.ItemsSource = _groups;
        DataContextChanged += (_, _) => Attach((DataContext as ShellViewModel)?.Sidebar);
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private void Attach(SidebarViewModel? sidebar)
    {
        if (_sidebar is not null)
            _sidebar.Rows.CollectionChanged -= OnRowsChanged;
        _sidebar = sidebar;
        if (_sidebar is not null)
            _sidebar.Rows.CollectionChanged += OnRowsChanged;
        Regroup();
    }

    /// <summary>A reload clears and refills the rows one by one; the cards are rebuilt once, after it.</summary>
    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_regroupPending)
            return;
        _regroupPending = true;
        Dispatcher.UIThread.Post(Regroup, DispatcherPriority.Background);
    }

    private void Regroup()
    {
        _regroupPending = false;
        FeedGroup.Fill(_groups, _sidebar?.Rows ?? []);
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is SidebarRowViewModel row && _sidebar is not null)
            _sidebar.SelectedRow = row;
    }

    private void OnDisclosureClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is SidebarRowViewModel row)
            _sidebar?.Toggle(row);
    }

    private void OnSectionClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is SidebarRowViewModel row)
            _sidebar?.Toggle(row);
    }

    private async void OnRefreshRequested(object? sender, RefreshRequestedEventArgs e)
    {
        var deferral = e.GetDeferral();
        try
        {
            if (Shell is { } shell)
                await shell.RefreshAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }
}
