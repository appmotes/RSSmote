using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Templates;
using RSSmote.Desktop.Views;
using RSSmote.Core.Models;

namespace RSSmote.Desktop.Tests;

public sealed class SidebarMenuTests : IDisposable
{
    private readonly ShellFixture _f = new(s => s.ExpandedFolders = [1]);

    public void Dispose() => _f.Dispose();

    /// <summary>Feeds and folders are edited in Settings (D30); a feed's menu keeps what reaches outside the app.</summary>
    /// <remarks>Builds menu items, so it runs on the headless UI thread (L15).</remarks>
    [AvaloniaFact]
    public void Feed_rows_have_a_menu_and_other_rows_none()
    {
        Assert.Equal(["Open wired.example", "Reload Icon"],
            SidebarView.MenuItems(_f.Shell, _f.Row("WIRED")).OfType<MenuItem>().Select(m => (string)m.Header!));
        Assert.Empty(SidebarView.MenuItems(_f.Shell, _f.Row("Apple")));
        Assert.Empty(SidebarView.MenuItems(_f.Shell, _f.Row("Unread")));
    }

    [Fact]
    public void Open_site_and_reload_icon_reach_their_services()
    {
        _f.Shell.OpenSiteCommand.Execute(_f.Wired);
        Assert.Equal("https://wired.example/", Assert.Single(_f.Launcher.Opened).Url.AbsoluteUri);

        _f.Shell.ReloadIconCommand.Execute(_f.Wired);
        Assert.Contains(_f.Images.Forgotten, url => url.Contains("wired.example", StringComparison.Ordinal));
    }
}

public class SidebarMenuRenderTests
{
    [AvaloniaFact]
    public void A_right_click_opens_the_menu_without_selecting_the_row()
    {
        using var f = new ShellFixture();
        var view = new ShellView();
        var window = new Window { Width = 1045, Height = 660, Content = view, DataContext = f.Shell };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        var sidebar = view.GetVisualDescendants().OfType<SidebarView>().Single();
        var row = view.GetVisualDescendants().OfType<SidebarRow>().Single(r => r.Title == "WIRED");
        var point = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.Equal(new ArticleStream.All(), f.Shell.Sidebar.SelectedStream);
        Assert.NotNull(sidebar.OpenMenu);
        Assert.True(sidebar.OpenMenu!.IsOpen);
        sidebar.OpenMenu.Close();
        window.Close();
    }
}
