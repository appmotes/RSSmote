using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using RSSmote.App.ViewModels;
using RSSmote.App.Views;
using RSSmote.App.Views.Desktop;
using RSSmote.App.Views.Phone.Shell;
using RSSmote.Core.Models;

namespace RSSmote.Mobile.Tests;

/// <summary>The one root picks the desktop columns or the phone stack by width, and keeps the stream on a switch (A22).</summary>
public sealed class MainViewTests
{
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    [AvaloniaFact]
    public void A_narrow_window_shows_the_phone_stack_and_a_wide_one_the_columns()
    {
        using var f = new ShellFixture();
        var view = new MainView
        {
            DataContext = f.Shell,
        };
        var window = new Window
        {
            Width = 1100,
            Height = 720,
            Content = view,
        };
        window.Show();
        Settle();
        Assert.IsType<ShellView>(view.Content);
        Assert.Equal(SidebarLayout.Desktop, f.Shell.Sidebar.Layout);

        window.Width = PhoneRender.Width;
        Settle();
        Assert.IsType<MobileShell>(view.Content);
        Assert.Equal(SidebarLayout.Phone, f.Shell.Sidebar.Layout);
        Assert.False(f.Shell.List.IsShown);

        window.Width = 1100;
        Settle();
        Assert.IsType<ShellView>(view.Content);
        Assert.True(f.Shell.List.IsShown);
        Assert.NotEmpty(f.Shell.List.Articles);
        window.Close();
    }

    [AvaloniaFact]
    public void A_phone_stack_left_behind_no_longer_follows_the_shell()
    {
        using var f = new ShellFixture();
        var view = new MainView
        {
            DataContext = f.Shell,
        };
        var window = new Window
        {
            Width = PhoneRender.Width,
            Height = PhoneRender.Height,
            Content = view,
        };
        window.Show();
        Settle();
        var phone = Assert.IsType<MobileShell>(view.Content);

        window.Width = 1100;
        Settle();
        f.Shell.Sidebar.Select(new ArticleStream.OfFeed(f.Wired.Id));
        Settle();
        Assert.Single(phone.NavigationStack);
        window.Close();
    }
}
