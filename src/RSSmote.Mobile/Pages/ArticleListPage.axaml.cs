using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.Mobile.Pages;

public partial class ArticleListPage : ContentPage
{
    public ArticleListPage()
    {
        InitializeComponent();
        Rows.ContainerPrepared += OnContainerPrepared;
    }

    public ArticleListPage(ShellViewModel shell) : this() => DataContext = shell;

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        var header = e.Container.DataContext is DateHeaderViewModel;
        e.Container.Classes.Set("header", header);
        e.Container.IsHitTestVisible = !header;
        if (e.Container.DataContext is ArticleRowViewModel row)
            Shell?.List.RequestImages(row);
    }

    private void OnFilterClick(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.Tag is string name && Enum.TryParse<StreamFilter>(name, out var filter))
            Shell?.ShowFilterCommand.Execute(filter);
    }

    /// <summary>The filter box shows on demand; hiding it clears the search.</summary>
    private void OnSearchClick(object? sender, RoutedEventArgs e)
    {
        Search.IsVisible = !Search.IsVisible;
        if (Search.IsVisible)
            Search.Focus();
        else
            Shell?.List.EndSearch();
    }
}
