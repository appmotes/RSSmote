using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.App.Views.Phone;
using RSSmote.App.Views.Phone.Pages;
using RSSmote.App.Views.Phone.Shell;
using RSSmote.Core.Models;

namespace RSSmote.Mobile.Tests;

/// <summary>The phone's top bars (D33): refresh in the middle of Feeds, the theme toggle beside every Aa.</summary>
public class FeedsTopBarTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    [AvaloniaFact]
    public void Refresh_sits_in_the_middle_and_the_toggle_beside_Aa_flips_to_dark()
    {
        using var f = new ShellFixture();
        var shell = new MobileShell(f.Shell);
        var window = PhoneRender.Show(shell, "Sand");
        Settle();

        var feeds = shell.GetVisualDescendants().OfType<FeedsView>().Single();
        var refresh = feeds.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("refresh"));
        var middle = refresh.TranslatePoint(new Point(refresh.Bounds.Width / 2, 0), feeds)!.Value.X;
        Assert.Equal(feeds.Bounds.Width / 2, middle, 1.0);

        var toggle = feeds.GetVisualDescendants().OfType<ThemeToggle>().Single();
        var text = feeds.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Aa"));
        var toggleAt = toggle.TranslatePoint(default, feeds)!.Value;
        var textAt = text.TranslatePoint(default, feeds)!.Value;
        Assert.True(toggleAt.X < textAt.X, "The toggle stands left of Aa.");
        Assert.Equal(toggleAt.Y + toggle.Bounds.Height / 2, textAt.Y + text.Bounds.Height / 2, 1.0);
        Assert.Equal("Dark Theme", ToolTip.GetTip(toggle));

        var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
        Assert.Equal(AppTheme.Dark, f.Settings.Current.Theme);

        f.Shell.IsRefreshing = true;
        Settle();
        Assert.Contains("refreshing", refresh.Classes);
        Assert.Equal(1, refresh.Opacity);
        window.Close();
    }

    [AvaloniaFact]
    public void The_list_and_the_article_carry_the_toggle_beside_Aa_and_it_works()
    {
        using var f = new ShellFixture();
        var shell = new MobileShell(f.Shell)
        {
            PageTransition = null,
        };
        var window = PhoneRender.Show(shell, "Sand");
        Settle();

        f.Shell.Sidebar.SelectedRow = f.Row("Unread");
        Settle();
        AssertToggleBesideAa(window, Assert.IsType<ArticleListPage>(shell.NavigationStack[^1]));
        Assert.Equal(AppTheme.Dark, f.Settings.Current.Theme);

        f.Shell.List.SelectedRow = f.Shell.List.Articles.First();
        Settle();
        Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        Settle();
        AssertToggleBesideAa(window, Assert.IsType<ArticlePage>(shell.NavigationStack[^1]));
        Assert.Equal(AppTheme.Light, f.Settings.Current.Theme);
        window.Close();
    }

    /// <summary>The bar sits outside the page (P21), so the toggle only works once it has the page's data context.</summary>
    private static void AssertToggleBesideAa(Window window, ContentPage page)
    {
        var bar = page.FindControl<StackPanel>("TopBar")!;
        var toggle = bar.Children.OfType<ThemeToggle>().Single();
        var text = bar.Children.OfType<Button>().Single(b => Equals(b.Content, "Aa"));
        Assert.True(toggle.IsEffectivelyVisible);
        Assert.NotNull(toggle.Command);
        Assert.True(toggle.TranslatePoint(default, bar)!.Value.X < text.TranslatePoint(default, bar)!.Value.X);

        var point = toggle.TranslatePoint(new Point(toggle.Bounds.Width / 2, toggle.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }
}
