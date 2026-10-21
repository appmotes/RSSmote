using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using RSSmote.App.Shell;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

public class ThemeMenuTests
{
    private static List<NativeMenuItem> Items(NativeMenu? menu) =>
        menu?.Items.OfType<NativeMenuItem>().SelectMany(i => Items(i.Menu).Prepend(i)).ToList() ?? [];

    /// <summary>The macOS menu's theme items are built in code (D22); every one must be clickable and pick its slot.</summary>
    /// <remarks>The three submenus come from XAML, so the native menu exports them as submenus before code fills them (L13).</remarks>
    [AvaloniaFact]
    public void Theme_items_in_the_menu_bar_are_enabled_and_switch_the_theme()
    {
        using var f = new ShellFixture();
        var window = new MainWindow(f.Shell);
        var items = Items(NativeMenu.GetMenu(window));

        foreach (var header in new[] { "Appearance", "Light Theme", "Dark Theme" })
        {
            var submenu = items.Single(i => i.Header == header);
            Assert.NotNull(submenu.Menu);
            var choices = submenu.Menu!.Items.OfType<NativeMenuItem>().ToList();
            Assert.NotEmpty(choices);
            Assert.All(choices, c => Assert.True(c.IsEnabled, $"{header} › {c.Header} is disabled"));
        }

        NativeMenuItem Choice(string submenu, string header) =>
            items.Single(i => i.Header == submenu).Menu!.Items.OfType<NativeMenuItem>().Single(i => i.Header == header);
        void Click(NativeMenuItem item) => item.Command!.Execute(item.CommandParameter);

        // Picking a palette also shows it: the appearance leaves Automatic for that palette's slot (D22).
        Assert.Equal(AppTheme.Automatic, f.Settings.Current.Theme);
        Click(Choice("Light Theme", "Newsprint"));
        Assert.Equal(("newsprint", AppTheme.Light), (f.Settings.Current.LightTheme, f.Settings.Current.Theme));
        Assert.True(Choice("Appearance", "Light").IsChecked);

        Click(Choice("Dark Theme", "Fjord"));
        Assert.Equal(("fjord", AppTheme.Dark), (f.Settings.Current.DarkTheme, f.Settings.Current.Theme));
        Assert.True(Choice("Dark Theme", "Fjord").IsChecked);
        Assert.True(Choice("Appearance", "Dark").IsChecked);

        Click(Choice("Appearance", "Automatic"));
        Assert.Equal(AppTheme.Automatic, f.Settings.Current.Theme);
        Assert.Equal("fjord", f.Settings.Current.DarkTheme);
    }
}
