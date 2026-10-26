using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Pages;
using RSSmote.Mobile.Shell;
using RSSmote.Mobile.Views;

namespace RSSmote.Mobile.Tests;

/// <summary>The Settings root and the pages it pushes (D27, D29).</summary>
public class SettingsFlowTests
{
    private static void Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static (ShellFixture Fixture, MobileShell Shell, Window Window) Open(string palette)
    {
        var f = new ShellFixture(s => s.ExpandedFolders = [1]);
        var shell = new MobileShell(f.Shell) { PageTransition = null, ModalTransition = null };
        var window = PhoneRender.Show(shell, palette);
        shell.Root.Show(RootTab.Settings);
        Settle();
        return (f, shell, window);
    }

    private static T Find<T>(Visual root, Func<T, bool>? match = null) where T : Visual =>
        root.GetVisualDescendants().OfType<T>().First(c => match?.Invoke(c) ?? true);

    private static void Click(Visual root, string text)
    {
        var button = root.GetVisualDescendants().OfType<Button>().First(b =>
            b.IsEffectivelyVisible && (b.Content as string == text
                || b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == text)));
        button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        if (button.Command?.CanExecute(button.CommandParameter) == true)
            button.Command.Execute(button.CommandParameter);
        Settle();
    }

    /// <summary>The app icon sits beside the version line at the end of the Settings root (D13).</summary>
    [AvaloniaFact]
    public void The_version_line_carries_the_app_icon()
    {
        var (f, shell, window) = Open("Sand");
        var version = Find<TextBlock>(shell, t => t.Text == f.Shell.Settings.VersionText);
        var icon = Assert.IsType<Image>(((Panel)version.Parent!).Children[0]);
        Assert.NotNull(icon.Source);
        Assert.True(icon.Bounds.Width > 0);
        window.Close();
        f.Dispose();
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void The_settings_pages_render(string palette)
    {
        var (f, shell, window) = Open(palette);
        Assert.IsType<SettingsView>(shell.Root.Content);
        PhoneRender.Save(window, $"phone-m5-{palette}");

        Click(shell, "Appearance");
        Assert.IsType<AppearancePage>(shell.NavigationStack[^1]);
        PhoneRender.Save(window, $"phone-m9-{palette}");
        _ = shell.PopAsync();
        Settle();

        Click(shell, "Text");
        Assert.IsType<TextPage>(shell.NavigationStack[^1]);
        PhoneRender.Save(window, $"phone-text-{palette}");
        _ = shell.PopAsync();
        Settle();

        Click(shell, "Manage Feeds");
        var manage = Assert.IsType<ManageFeedsPage>(shell.NavigationStack[^1]);
        PhoneRender.Save(window, $"phone-m6-{palette}");

        Click(manage, "Daring Fireball");
        Assert.IsType<EditFeedPage>(shell.NavigationStack[^1]);
        PhoneRender.Save(window, $"phone-m7-{palette}");

        Click(shell.NavigationStack[^1], "Remove Feed…");
        Assert.IsType<ConfirmSheet>(shell.ModalStack[^1]);
        PhoneRender.Save(window, $"phone-confirm-{palette}");
        window.Close();
        f.Dispose();
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public async Task Add_Feed_lists_what_it_found(string palette)
    {
        var (f, shell, window) = Open(palette);
        f.Http.Pages["https://blog.example/feed.xml"] = """
            <rss version="2.0"><channel><title>Example Blog</title><link>https://blog.example/</link>
            <item><title>Hello</title><guid>h1</guid><link>https://blog.example/hello</link></item>
            </channel></rss>
            """;
        Click(shell, "Add Feed");
        var page = Assert.IsType<Pages.AddFeedPage>(shell.NavigationStack[^1]);
        var add = Assert.IsType<AddFeedViewModel>(page.DataContext);
        add.Address = "https://blog.example/feed.xml";
        await add.SearchCommand.ExecuteAsync(null);
        Settle();
        PhoneRender.Save(window, $"phone-m8-{palette}");

        Click(page, "Add Feed");
        Assert.IsType<RootPage>(shell.NavigationStack[^1]);
        Assert.Contains(f.Store.Feeds(), x => x.Title == "Example Blog");
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void Removing_a_feed_asks_then_closes_its_page()
    {
        var (f, shell, window) = Open("Sand");
        Click(shell, "Manage Feeds");
        Click(shell.NavigationStack[^1], "WIRED");
        Click(shell.NavigationStack[^1], "Remove Feed…");

        Click(shell.ModalStack[^1], "Cancel");
        Assert.Empty(shell.ModalStack);
        Assert.IsType<EditFeedPage>(shell.NavigationStack[^1]);

        Click(shell.NavigationStack[^1], "Remove Feed…");
        Click(shell.ModalStack[^1], "Remove Feed");
        Assert.Empty(shell.ModalStack);
        var manage = Assert.IsType<ManageFeedsPage>(shell.NavigationStack[^1]);
        Assert.DoesNotContain(f.Store.Feeds(), x => x.Title == "WIRED");
        Assert.DoesNotContain(((ManageFeedsViewModel)manage.DataContext!).Groups.SelectMany(g => g.Feeds), r => r.Title == "WIRED");
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void A_confirmed_sheet_closes_before_the_page_under_it()
    {
        var (f, shell, window) = Open("Sand");
        Click(shell, "Manage Feeds");
        Click(shell.NavigationStack[^1], "WIRED");
        Click(shell.NavigationStack[^1], "Remove Feed…");
        var held = new HeldTransition();
        shell.PageTransition = held;
        shell.ModalTransition = held;

        Click(shell.ModalStack[^1], "Remove Feed");
        for (var i = 0; i < 3; i++)
        {
            held.Finish();
            Settle();
        }
        Assert.Empty(shell.ModalStack);
        Assert.IsType<ManageFeedsPage>(shell.NavigationStack[^1]);
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void Saving_an_edit_pops_its_page_and_back_discards_it()
    {
        var (f, shell, window) = Open("Sand");
        Click(shell, "Manage Feeds");
        Click(shell.NavigationStack[^1], "WIRED");
        var edit = Assert.IsType<EditFeedViewModel>(shell.NavigationStack[^1].DataContext);
        edit.Name = "Wired Magazine";
        _ = shell.PopAsync();
        Settle();
        Assert.Equal("WIRED", f.Store.Feed(f.Wired.Id)!.Title);

        Click(shell.NavigationStack[^1], "WIRED");
        edit = Assert.IsType<EditFeedViewModel>(shell.NavigationStack[^1].DataContext);
        edit.Name = "Wired Magazine";
        Click(shell, "Save");
        Assert.IsType<ManageFeedsPage>(shell.NavigationStack[^1]);
        Assert.Equal("Wired Magazine", f.Store.Feed(f.Wired.Id)!.Title);
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void Deleting_a_folder_keeps_its_feeds_and_closes_its_page()
    {
        var (f, shell, window) = Open("Sand");
        Click(shell, "Manage Feeds");
        var manage = shell.NavigationStack[^1];
        Find<Button>(manage, b => b.Content as string == "Edit" && b.IsEffectivelyVisible)
            .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Settle();
        Assert.IsType<EditFolderPage>(shell.NavigationStack[^1]);

        Click(shell.NavigationStack[^1], "Delete Folder…");
        Click(shell.ModalStack[^1], "Delete Folder");
        Assert.IsType<ManageFeedsPage>(shell.NavigationStack[^1]);
        Assert.DoesNotContain(f.Store.Folders(), x => x.Title == "Apple");
        Assert.Null(f.Store.Feed(f.Daring.Id)!.FolderId);
        window.Close();
        f.Dispose();
    }

    [AvaloniaFact]
    public void A_switch_writes_its_setting_and_the_system_back_closes_a_sheet_first()
    {
        var (f, shell, window) = Open("Sand");
        var toggle = Find<ToggleSwitch>(shell.Root, t => t.IsEffectivelyVisible);
        Assert.True(toggle.IsChecked);
        toggle.IsChecked = false;
        Assert.False(f.Settings.Current.MarkAsReadOnSelect);

        Click(shell, "Manage Feeds");
        Click(shell.NavigationStack[^1], "WIRED");
        Click(shell.NavigationStack[^1], "Remove Feed…");
        var back = new Avalonia.Interactivity.RoutedEventArgs(TopLevel.BackRequestedEvent);
        window.RaiseEvent(back);
        Settle();
        Assert.True(back.Handled);
        Assert.Empty(shell.ModalStack);
        Assert.IsType<EditFeedPage>(shell.NavigationStack[^1]);
        window.Close();
        f.Dispose();
    }
}
