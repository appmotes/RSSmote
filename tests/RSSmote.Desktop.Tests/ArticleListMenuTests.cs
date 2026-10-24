using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Templates;
using RSSmote.Desktop.Views;

namespace RSSmote.Desktop.Tests;

/// <summary>The article list's context menu (A6).</summary>
public class ArticleListMenuTests
{
    [AvaloniaFact]
    public void A_right_click_on_an_article_opens_its_menu_without_selecting_it()
    {
        using var f = new ShellFixture();
        var view = new ShellView();
        var window = new Window { Width = 1045, Height = 660, Content = view, DataContext = f.Shell };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var list = view.GetVisualDescendants().OfType<ArticleListView>().Single();
        var row = view.GetVisualDescendants().OfType<ArticleRow>().ElementAt(2);
        var point = row.TranslatePoint(new Point(60, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.Null(f.Shell.List.SelectedRow);
        Assert.NotNull(list.OpenMenu);
        Assert.True(list.OpenMenu!.IsOpen);
        var items = list.OpenMenu.Items.OfType<MenuItem>().ToList();
        Assert.Equal(["Mark as Read", "Mark Above as Read", "Mark Below as Read", "Star", "Open in Browser"],
            items.Select(m => (string)m.Header!));
        Assert.All(items, m => Assert.True(m.IsEnabled, $"{m.Header} is disabled"));
        Assert.Equal(new KeyGesture(Key.M), items[0].InputGesture);

        items[1].Command!.Execute(items[1].CommandParameter);
        Assert.Equal([false, false, true, true], f.Shell.List.Articles.Select(a => a.IsUnread));
        list.OpenMenu.Close();
        window.Close();
    }

    [AvaloniaFact]
    public void The_first_row_cannot_mark_above_and_a_read_starred_row_offers_the_opposite()
    {
        using var f = new ShellFixture();
        var rows = f.Shell.List.Articles.ToList();
        f.Shell.ToggleRowReadCommand.Execute(rows[0]);
        f.Shell.ToggleRowStarredCommand.Execute(rows[0]);

        var items = ArticleListView.MenuItems(f.Shell, rows[0]).OfType<MenuItem>().ToList();
        Assert.Equal(["Mark as Unread", "Mark Above as Read", "Mark Below as Read", "Unstar", "Open in Browser"],
            items.Select(m => (string)m.Header!));
        Assert.False(items[1].IsEnabled);
        Assert.True(items[2].IsEnabled);
    }
}
