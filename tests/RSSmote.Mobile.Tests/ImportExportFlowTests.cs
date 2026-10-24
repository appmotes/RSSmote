using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Pages;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Tests;

/// <summary>Import Feeds with its preview and Export Feeds on the phone (D27).</summary>
public class ImportExportFlowTests
{
    private static void Settle()
    {
        for (var i = 0; i < 20; i++)
        {
            Dispatcher.UIThread.RunJobs();
            Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

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

    private static (ShellFixture Fixture, MobileShell Shell, Window Window) Open(string palette)
    {
        var f = new ShellFixture();
        var shell = new MobileShell(f.Shell) { PageTransition = null, ModalTransition = null };
        var window = PhoneRender.Show(shell, palette);
        shell.Root.Show(RootTab.Settings);
        Settle();
        return (f, shell, window);
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Import_previews_the_file_and_adds_its_feeds_on_import(string palette)
    {
        var (f, shell, window) = Open(palette);
        f.Files.NextName = "my-feeds.opml";
        f.Files.NextOpen = """
            <opml version="2.0"><body>
              <outline text="Science"><outline text="Night Sky" type="rss" xmlUrl="https://sky.example/rss" /></outline>
              <outline text="Apple"><outline text="Daring Fireball" type="rss" xmlUrl="https://df.example/rss" /></outline>
              <outline text="Loose" type="rss" xmlUrl="https://loose.example/rss" />
            </body></opml>
            """;
        Click(shell, "Import Feeds");
        var page = Assert.IsType<ImportFeedsPage>(shell.NavigationStack[^1]);
        Click(page, "Choose File…");
        Assert.Equal("Import 2 Feeds", Assert.IsType<ImportFeedsViewModel>(page.DataContext).ImportTitle);
        PhoneRender.Save(window, $"phone-m10-{palette}");

        Click(page, "Import 2 Feeds");
        Assert.IsType<RootPage>(shell.NavigationStack[^1]);
        Assert.Equal(6, f.Store.Feeds().Count);
        window.Close();
        f.Dispose();
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Export_shows_what_the_file_holds(string palette)
    {
        var (f, shell, window) = Open(palette);
        Click(shell, "Export Feeds");
        var page = Assert.IsType<ExportFeedsPage>(shell.NavigationStack[^1]);
        PhoneRender.Save(window, $"phone-m11-{palette}");
        Click(page, "Export…");
        Assert.NotNull(f.Files.Saved);
        window.Close();
        f.Dispose();
    }
}
