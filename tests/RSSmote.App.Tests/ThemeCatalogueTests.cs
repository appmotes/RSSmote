using Avalonia.Styling;
using RSSmote.App.Theming;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

public class ThemeCatalogueTests
{
    [Fact]
    public void Each_slot_has_its_default_first() // D22
    {
        Assert.Equal(AppSettings.DefaultLightTheme, ThemeCatalogue.Light[0].Id);
        Assert.Equal(AppSettings.DefaultDarkTheme, ThemeCatalogue.Dark[0].Id);
        Assert.All(ThemeCatalogue.Light, t => Assert.False(t.IsDark));
        Assert.All(ThemeCatalogue.Dark, t => Assert.True(t.IsDark));
    }

    [Fact]
    public void An_unknown_id_falls_back_to_the_slot_default()
    {
        Assert.Equal(AppSettings.DefaultLightTheme, ThemeCatalogue.Resolve("no-such-theme", dark: false).Id);
        Assert.Equal(AppSettings.DefaultDarkTheme, ThemeCatalogue.Resolve(null, dark: true).Id);
        Assert.Equal(AppSettings.DefaultLightTheme, ThemeCatalogue.Resolve(AppSettings.BlackTheme, dark: false).Id);
    }

    [Fact]
    public void Automatic_shows_the_slot_the_os_asks_for()
    {
        var settings = new AppSettings { Theme = AppTheme.Automatic, DarkTheme = AppSettings.BlackTheme };
        Assert.Equal(ThemeVariant.Light, ThemeCatalogue.VariantFor(settings, osPrefersDark: false));
        Assert.Equal(ThemeCatalogue.Black, ThemeCatalogue.VariantFor(settings, osPrefersDark: true));
    }

    [Fact]
    public void Light_and_dark_ignore_the_os()
    {
        var light = new AppSettings { Theme = AppTheme.Light };
        var dark = new AppSettings { Theme = AppTheme.Dark };
        Assert.Equal(ThemeVariant.Light, ThemeCatalogue.VariantFor(light, osPrefersDark: true));
        Assert.Equal(ThemeVariant.Dark, ThemeCatalogue.VariantFor(dark, osPrefersDark: false));
    }
}
