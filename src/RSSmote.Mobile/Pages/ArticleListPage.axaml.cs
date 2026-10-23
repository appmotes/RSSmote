using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.Mobile.Pages;

public partial class ArticleListPage : ContentPage
{
    private ArticleListViewModel? _list;

    /// <summary>The top article row and how far below the top of the viewport it sits.</summary>
    private (long Id, double Y)? _anchor;

    public ArticleListPage()
    {
        InitializeComponent();
        Rows.ContainerPrepared += OnContainerPrepared;
        DataContextChanged += (_, _) => Attach(Shell?.List);
        NavigatedTo += (_, _) => KeepPlace();
    }

    public ArticleListPage(ShellViewModel shell) : this() => DataContext = shell;

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    /// <summary>
    /// A rebuild replaces every row, which moves the list: while the page shows it, and while an article covers it,
    /// the row that was on top is put back where it was (P25).
    /// </summary>
    private void Attach(ArticleListViewModel? list)
    {
        if (_list is not null)
        {
            _list.Rebuilding -= OnRebuilding;
            _list.Rebuilt -= OnRebuilt;
        }
        _list = list;
        if (_list is not null)
        {
            _list.Rebuilding += OnRebuilding;
            _list.Rebuilt += OnRebuilt;
        }
    }

    /// <summary>An article covers the page: its rows still hold their last layout, relative to each other.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _anchor = TopRow() ?? _anchor;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnRebuilding(object? sender, EventArgs e)
    {
        if (this.IsAttachedToVisualTree())
            _anchor = TopRow() ?? _anchor;
    }

    private void OnRebuilt(object? sender, EventArgs e)
    {
        if (this.IsAttachedToVisualTree())
            KeepPlace();
    }

    private (long Id, double Y)? TopRow() =>
        Rows.GetRealizedContainers()
            .Where(c => c.DataContext is ArticleRowViewModel)
            .Select(c => (Row: (ArticleRowViewModel)c.DataContext!, Y: c.TranslatePoint(default, Scroller)?.Y, c.Bounds.Height))
            .Where(r => r.Y is { } y && y + r.Height > 0)
            .OrderBy(r => r.Y)
            .Select(r => ((long Id, double Y)?)(r.Row.Id, r.Y!.Value))
            .FirstOrDefault();

    /// <summary>Scrolls the anchored row back to its place; the row heights below an estimate settle within a few passes.</summary>
    private void KeepPlace()
    {
        if (_anchor is not { } anchor || _list?.Articles.FirstOrDefault(a => a.Id == anchor.Id) is not { } row)
            return;
        var index = _list.Rows.IndexOf(row);
        UpdateLayout();
        for (var pass = 0; pass < 4; pass++)
        {
            if (Rows.ContainerFromIndex(index) is not { } container)
            {
                Rows.ScrollIntoView(index);
                UpdateLayout();
                continue;
            }
            if (container.TranslatePoint(default, Scroller)?.Y is not { } y || Math.Abs(y - anchor.Y) < 0.5)
                return;
            Scroller.Offset = Scroller.Offset.WithY(Math.Max(0, Scroller.Offset.Y + y - anchor.Y));
            UpdateLayout();
        }
    }

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
