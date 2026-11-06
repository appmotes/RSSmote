using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.App.Theming;
using RSSmote.App.Views.Desktop;
using RSSmote.Core.Models;

namespace RSSmote.Desktop.Tests;

/// <summary>The sidebar's theme toggle and refresh button (D33).</summary>
public class SidebarToolbarTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static (Window Window, ShellView View) Show(ShellFixture f)
    {
        var view = new ShellView();
        var window = new Window
        {
            Width = 1100,
            Height = 720,
            Content = view,
            DataContext = f.Shell,
        };
        window.Show();
        Settle();
        return (window, view);
    }

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Settle();
    }

    [AvaloniaFact]
    public void The_toggle_shows_where_it_goes_and_switches_to_the_other_slot()
    {
        using var f = new ShellFixture();
        var (window, view) = Show(f);
        window.RequestedThemeVariant = ThemeCatalogue.Fern;
        Settle();

        var toggle = view.GetVisualDescendants().OfType<ThemeToggle>().Single();
        var icons = toggle.GetVisualDescendants().OfType<Icon>().ToList();
        Assert.Equal("Dark Theme", ToolTip.GetTip(toggle));
        Assert.Equal([true, false], icons.Select(i => i.IsVisible));

        Click(window, toggle);
        Assert.Equal(AppTheme.Dark, f.Settings.Current.Theme);

        window.RequestedThemeVariant = ThemeCatalogue.Fjord;
        Settle();
        Assert.Equal("Light Theme", ToolTip.GetTip(toggle));
        Assert.Equal([false, true], icons.Select(i => i.IsVisible));

        Click(window, toggle);
        Assert.Equal(AppTheme.Light, f.Settings.Current.Theme);
        window.Close();
    }

    [AvaloniaFact]
    public void Refresh_sits_in_the_middle_and_its_arrow_turns_only_while_a_refresh_runs()
    {
        using var f = new ShellFixture();
        var (window, view) = Show(f);
        var refresh = view.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("refresh"));
        var arrow = refresh.GetVisualDescendants().OfType<Icon>().Single();
        var sidebar = view.GetVisualDescendants().OfType<SidebarView>().Single();
        var middle = refresh.TranslatePoint(new Point(refresh.Bounds.Width / 2, 0), sidebar)!.Value.X;
        Assert.Equal(sidebar.Bounds.Width / 2, middle, 1.0);
        Assert.DoesNotContain("refreshing", refresh.Classes);

        f.Shell.IsRefreshing = true;
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(16);
            Settle();
        }

        Assert.Contains("refreshing", refresh.Classes);
        Assert.Equal(1, refresh.Opacity);
        // The animator swaps the transform for a group that holds the rotation.
        var turn = arrow.RenderTransform as RotateTransform
                   ?? Assert.IsType<TransformGroup>(arrow.RenderTransform).Children.OfType<RotateTransform>().Single();
        Assert.NotEqual(0, turn.Angle);

        f.Shell.IsRefreshing = false;
        Settle();
        Assert.DoesNotContain("refreshing", refresh.Classes);
        var rest = arrow.RenderTransform as RotateTransform
                   ?? (arrow.RenderTransform as TransformGroup)?.Children.OfType<RotateTransform>().SingleOrDefault();
        Assert.Equal(0, rest?.Angle ?? 0);
        window.Close();
    }
}
