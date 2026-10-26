using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;
using RSSmote.Core.Parsing;
using RSSmote.Mobile.Pages;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Tests;

/// <summary>Feeds → article list → article on the phone (D23–D26).</summary>
public class ReadingFlowTests
{
    private static void Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static (ShellFixture Fixture, MobileShell Shell, Avalonia.Controls.Window Window) Open(string palette)
    {
        var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        var shell = new MobileShell(f.Shell) { PageTransition = null, ModalTransition = null };
        var window = PhoneRender.Show(shell, palette);
        Settle();
        return (f, shell, window);
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Feeds_root_renders_streams_folders_and_feeds(string palette)
    {
        var (f, shell, window) = Open(palette);
        Assert.Equal(SidebarLayout.Phone, f.Shell.Sidebar.Layout);
        PhoneRender.Save(window, $"phone-m1-{palette}");
        window.Close();
        f.Dispose();
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Picking_a_stream_then_an_article_pushes_the_list_and_the_article(string palette)
    {
        var (f, shell, window) = Open(palette);

        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        PhoneRender.Save(window, $"phone-m2-{palette}");

        f.Shell.List.SelectedRow = f.Shell.List.Articles.First();
        Settle();
        Assert.IsType<ArticlePage>(shell.NavigationStack[^1]);
        Assert.Equal("1 of 4", f.Shell.Article.PositionText);
        PhoneRender.Save(window, $"phone-m3-{palette}");
        window.Close();
        f.Dispose();
    }

    /// <summary>The stack ignores a push while the list still slides in (P24); the article follows once it ends.</summary>
    [AvaloniaFact]
    public void An_article_picked_while_the_list_slides_in_opens_once_the_slide_ends()
    {
        var (f, shell, window) = Open("Sand");
        var held = new HeldTransition();
        shell.PageTransition = held;

        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        f.Shell.List.SelectedRow = f.Shell.List.Articles.First();
        Settle();
        Assert.IsNotType<ArticlePage>(shell.NavigationStack[^1]);

        for (var i = 0; i < 3; i++)
        {
            held.Finish();
            Settle();
        }
        Assert.IsType<ArticlePage>(shell.NavigationStack[^1]);
        Assert.Equal(["RootPage", "ArticleListPage", "ArticlePage"], shell.NavigationStack.Select(p => p.GetType().Name));
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void Going_back_clears_the_picks_so_the_same_rows_open_again()
    {
        var (f, shell, window) = Open("Sand");
        f.Shell.Sidebar.SelectedRow = f.Row("WIRED");
        Settle();
        f.Shell.List.SelectedRow = f.Shell.List.Articles.First();
        Settle();
        Assert.Equal(["RootPage", "ArticleListPage", "ArticlePage"], shell.NavigationStack.Select(p => p.GetType().Name));

        _ = shell.PopAsync();
        Settle();
        Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        Assert.Null(f.Shell.List.SelectedRow);

        _ = shell.PopAsync();
        Settle();
        Assert.IsType<RootPage>(shell.NavigationStack[^1]);
        f.Shell.Sidebar.SelectedRow = f.Row("WIRED");
        Settle();
        Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void The_list_filter_buttons_set_the_filter()
    {
        var (f, shell, window) = Open("Sand");
        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        f.Shell.ShowFilterCommand.Execute(StreamFilter.All);
        Settle();
        Assert.Equal(StreamFilter.All, f.Shell.List.Filter);
        Assert.Equal(5, f.Shell.List.Articles.Count());
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void The_list_title_scrolls_away_and_only_rows_in_view_are_realised()
    {
        var (f, shell, window) = Open("Sand");
        f.Store.Upsert(f.Wired.Id, [.. Enumerable.Range(0, 200).Select(i => new ParsedItem($"m{i}", $"Item {i}",
            $"https://example.com/m{i}", null, "<p>x</p>", "x", null, ShellFixture.Now.AddMinutes(-60 - i)))]);
        f.Shell.FlushPendingReload();
        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        var page = Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        var rows = page.FindControl<ListBox>("Rows")!;
        var scroller = page.GetVisualDescendants().OfType<ScrollViewer>().First();
        var title = page.GetVisualDescendants().OfType<TextBlock>().First(t => t.Classes.Contains("phoneLargeTitle"));
        Assert.InRange(rows.GetRealizedContainers().Count(), 1, 20);
        Assert.InRange(title.TranslatePoint(default, page)!.Value.Y, 0, 200);

        scroller.Offset = new Vector(0, 3000);
        Settle();
        Assert.Equal(3000, scroller.Offset.Y);
        // Scrolled out of view, the title's row is either above the page or recycled.
        Assert.True(title.TranslatePoint(default, page) is not { } at || at.Y < 0);
        Assert.InRange(rows.GetRealizedContainers().Count(), 1, 20);
        PhoneRender.Save(window, "phone-m2-scrolled");
        window.Close();
        f.Dispose();
    }

    /// <summary>The first article row whose bottom is below the top of the list's viewport, and where it sits.</summary>
    private static (long Id, double Y) TopRow(ArticleListPage page)
    {
        var scroller = page.GetVisualDescendants().OfType<ScrollViewer>().First();
        return page.FindControl<ListBox>("Rows")!.GetRealizedContainers()
            .Where(c => c.DataContext is ArticleRowViewModel)
            .Select(c => (Id: ((ArticleRowViewModel)c.DataContext!).Id, Y: c.TranslatePoint(default, scroller)!.Value.Y, c.Bounds.Height))
            .Where(r => r.Y + r.Height > 0)
            .OrderBy(r => r.Y)
            .Select(r => (r.Id, r.Y))
            .First();
    }

    [AvaloniaFact]
    public void A_reload_while_an_article_is_open_keeps_the_list_where_it_was()
    {
        var (f, shell, window) = Open("Sand");
        f.Store.Upsert(f.Wired.Id, [.. Enumerable.Range(0, 200).Select(i => new ParsedItem($"m{i}", $"Item {i}",
            $"https://example.com/m{i}", null, "<p>x</p>", "x", null, ShellFixture.Now.AddMinutes(-60 - i)))]);
        f.Shell.FlushPendingReload();
        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        var page = Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        var scroller = page.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroller.Offset = new Vector(0, 3000);
        Settle();
        var before = TopRow(page);

        f.Shell.List.SelectedRow = f.Shell.List.Articles.First(a => a.Id == before.Id);
        Settle();
        Assert.IsType<ArticlePage>(shell.NavigationStack[^1]);
        f.Store.Upsert(f.Wired.Id, [.. Enumerable.Range(0, 30).Select(i => new ParsedItem($"n{i}", $"New {i}",
            $"https://example.com/n{i}", null, "<p>x</p>", "x", null, ShellFixture.Now.AddMinutes(-i)))]);
        f.Shell.FlushPendingReload();
        Settle();

        _ = shell.PopAsync();
        Settle();
        var after = TopRow(page);
        Assert.Equal(before.Id, after.Id);
        Assert.InRange(after.Y, before.Y - 1, before.Y + 1);
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void A_reload_while_the_list_shows_keeps_its_top_row_in_place()
    {
        var (f, shell, window) = Open("Sand");
        f.Store.Upsert(f.Wired.Id, [.. Enumerable.Range(0, 200).Select(i => new ParsedItem($"m{i}", $"Item {i}",
            $"https://example.com/m{i}", null, "<p>x</p>", "x", null, ShellFixture.Now.AddMinutes(-60 - i)))]);
        f.Shell.FlushPendingReload();
        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        var page = Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        page.GetVisualDescendants().OfType<ScrollViewer>().First().Offset = new Vector(0, 3000);
        Settle();
        var before = TopRow(page);

        f.Store.Upsert(f.Wired.Id, [.. Enumerable.Range(0, 30).Select(i => new ParsedItem($"n{i}", $"New {i}",
            $"https://example.com/n{i}", null, "<p>x</p>", "x", null, ShellFixture.Now.AddMinutes(-i)))]);
        f.Shell.FlushPendingReload();
        Settle();
        var after = TopRow(page);
        Assert.Equal(before.Id, after.Id);
        Assert.InRange(after.Y, before.Y - 1, before.Y + 1);
        window.Close();
        f.Dispose();
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Aa_opens_the_text_sheet_over_the_article(string palette)
    {
        var (f, shell, window) = Open(palette);
        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        f.Shell.List.SelectedRow = f.Shell.List.Articles.First();
        Settle();
        var aa = shell.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Aa");
        aa.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        var sheet = Assert.IsType<TextSheet>(shell.ModalStack[^1]);
        PhoneRender.Save(window, $"phone-m4-{palette}");

        Assert.Equal(TextPane.Article, f.Shell.Settings.SelectedPane);
        var larger = sheet.GetVisualDescendants().OfType<Button>().Last(b => b.Content as string == "A");
        larger.Command!.Execute(larger.CommandParameter);
        Assert.Equal(110, f.Settings.Current.ArticleTextSize);
        window.Close();
        f.Dispose();
    }

    /// <summary>Aa on the Feeds root and on the list opens the sheet on that page's pane, and the rows grow (D31).</summary>
    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Aa_on_the_feeds_and_the_list_opens_the_sheet_on_their_pane(string palette)
    {
        var (f, shell, window) = Open(palette);
        var feedsAa = shell.Root.GetVisualDescendants().OfType<Button>().First(b => b.Content as string == "Aa");
        feedsAa.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        var sheet = Assert.IsType<TextSheet>(shell.ModalStack[^1]);
        Assert.Equal(TextPane.Feeds, f.Shell.Settings.SelectedPane);
        f.Shell.Settings.PaneStep = 4;
        Settle();
        Assert.Equal(120, f.Settings.Current.FeedsTextSize);
        PhoneRender.Save(window, $"phone-text-feeds-{palette}");
        _ = shell.CloseSheetAsync();
        Settle();

        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        var list = Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        shell.ShowTextSheet(TextPane.List);
        Settle();
        Assert.Equal(TextPane.List, f.Shell.Settings.SelectedPane);
        var larger = shell.ModalStack[^1].GetVisualDescendants().OfType<Button>().Last(b => b.Content as string == "A");
        larger.Command!.Execute(larger.CommandParameter);
        Settle();
        Assert.Equal(110, f.Settings.Current.ListTextSize);
        var scaled = list.GetVisualDescendants().OfType<App.Controls.Headless.Scaled>().First();
        Assert.Equal(1.1, App.Controls.Headless.Scaled.GetFactor(scaled), 3);
        PhoneRender.Save(window, $"phone-text-list-{palette}");
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void The_bars_step_aside_on_a_scroll_down_and_come_back_on_a_scroll_up()
    {
        var (f, shell, window) = Open("Sand");
        f.Store.Upsert(f.Wired.Id, [new ParsedItem("long", "Long read", "https://example.com/long", null,
            string.Concat(Enumerable.Repeat("<p>Lorem ipsum dolor sit amet, consectetur adipiscing elit.</p>", 300)),
            "x", null, ShellFixture.Now)]);
        f.Shell.FlushPendingReload();
        f.Shell.Sidebar.SelectedRow = f.Row("WIRED");
        Settle();
        f.Shell.List.SelectedRow = f.Shell.List.Articles.First(a => a.Item.Guid == "long");
        Settle();
        var page = Assert.IsType<ArticlePage>(shell.NavigationStack[^1]);
        var scroller = page.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(page.BarsShown);

        scroller.Offset = new Vector(0, 600);
        Settle();
        Assert.False(page.BarsShown);
        PhoneRender.Save(window, "phone-m3-reading");

        // The page ignores scrolling while it resizes after the bars change.
        Thread.Sleep(350);
        scroller.Offset = new Vector(0, 500);
        Settle();
        Assert.True(page.BarsShown);
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void The_system_back_pops_a_page_and_leaves_the_root_to_the_system()
    {
        var (f, shell, window) = Open("Sand");
        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();

        var back = new Avalonia.Interactivity.RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(back);
        Settle();
        Assert.True(back.Handled);
        Assert.IsType<RootPage>(shell.NavigationStack[^1]);

        var again = new Avalonia.Interactivity.RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(again);
        Assert.False(again.Handled);
        window.Close();
        f.Dispose();
    }

    /// <summary>Holds the incoming page exactly one width to the right, as a slide's first frame does on a device.</summary>
    private sealed class ParkedSlide(bool layOutFirst, double width) : Avalonia.Animation.IPageTransition
    {
        public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
        {
            if (layOutFirst)
                LaidOutPageSlide.LayOutInPlace(to);
            if (to is not null)
                to.RenderTransform = new Avalonia.Media.TranslateTransform(width, 0);
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }
    }

    [AvaloniaTheory]
    [InlineData(false, 0)]
    [InlineData(true, 8)]
    public void A_list_pushed_one_width_aside_realises_its_rows_only_when_laid_out_in_place_first(bool layOutFirst, int rows)
    {
        var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        f.Store.Upsert(f.Wired.Id, [.. Enumerable.Range(0, 200).Select(i => new ParsedItem($"m{i}", $"Item {i}",
            $"https://example.com/m{i}", null, "<p>x</p>", "x", null, ShellFixture.Now.AddMinutes(-60 - i)))]);
        f.Shell.FlushPendingReload();
        var shell = new MobileShell(f.Shell);
        var window = PhoneRender.Show(shell, "Sand");
        Settle();
        shell.PageTransition = new ParkedSlide(layOutFirst, shell.Bounds.Width);

        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        var page = Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]);
        Assert.Equal(shell.Bounds.Width, page.TranslatePoint(default, window)!.Value.X);
        Assert.Equal(rows, page.FindControl<ListBox>("Rows")!.GetRealizedContainers().Count(c => c.DataContext is ArticleRowViewModel));
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void The_shell_lays_each_page_out_in_place_before_it_slides()
    {
        using var f = new ShellFixture();
        var shell = new MobileShell(f.Shell);
        var window = PhoneRender.Show(shell, "Sand");
        Assert.IsType<LaidOutPageSlide>(shell.PageTransition);
        shell.PageTransition = new Avalonia.Animation.PageSlide(TimeSpan.FromMilliseconds(250));
        Assert.Equal(TimeSpan.FromMilliseconds(250), Assert.IsType<LaidOutPageSlide>(shell.PageTransition).Duration);
        window.Close();
    }
}
