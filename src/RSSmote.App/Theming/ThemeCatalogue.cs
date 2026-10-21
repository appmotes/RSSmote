using Avalonia.Styling;
using RSSmote.Core.Models;

namespace RSSmote.App.Theming;

/// <summary>One palette under Assets/Themes/ (D22). <see cref="Id"/> is what the settings store.</summary>
public sealed record ThemeOption(string Id, string Name, bool IsDark, ThemeVariant Variant);

/// <summary>
/// The palettes the app ships, in two slots (D22). Adding one is a file under Assets/Themes/, its ThemeDictionaries
/// entry in App.axaml and a line here. Sand and Ember are the built-in Light and Dark variants; every other palette
/// is a variant inheriting from one of them, so Fluent's own resources still resolve.
/// </summary>
public static class ThemeCatalogue
{
    public static readonly ThemeVariant Newsprint = new("Newsprint", ThemeVariant.Light);
    public static readonly ThemeVariant Fern = new("Fern", ThemeVariant.Light);
    public static readonly ThemeVariant Harbor = new("Harbor", ThemeVariant.Light);
    public static readonly ThemeVariant Contrast = new("Contrast", ThemeVariant.Light);
    public static readonly ThemeVariant Black = new("Black", ThemeVariant.Dark);
    public static readonly ThemeVariant Fjord = new("Fjord", ThemeVariant.Dark);
    public static readonly ThemeVariant Phosphor = new("Phosphor", ThemeVariant.Dark);
    public static readonly ThemeVariant ContrastDark = new("ContrastDark", ThemeVariant.Dark);

    /// <summary>Each slot's default first; <see cref="ThemeOption.Name"/> is also the palette's file name.</summary>
    public static readonly ThemeOption[] All =
    [
        new(AppSettings.DefaultLightTheme, "Sand", false, ThemeVariant.Light),
        new("newsprint", "Newsprint", false, Newsprint),
        new("fern", "Fern", false, Fern),
        new("harbor", "Harbor", false, Harbor),
        new("contrast", "Contrast", false, Contrast),
        new(AppSettings.DefaultDarkTheme, "Ember", true, ThemeVariant.Dark),
        new(AppSettings.BlackTheme, "Black", true, Black),
        new("fjord", "Fjord", true, Fjord),
        new("phosphor", "Phosphor", true, Phosphor),
        new("contrast-dark", "ContrastDark", true, ContrastDark),
    ];

    public static ThemeOption[] Light { get; } = [.. All.Where(t => !t.IsDark)];

    public static ThemeOption[] Dark { get; } = [.. All.Where(t => t.IsDark)];

    /// <summary>An id this build does not know resolves to the slot's default rather than failing.</summary>
    public static ThemeOption Resolve(string? id, bool dark)
    {
        var slot = dark ? Dark : Light;
        return slot.FirstOrDefault(t => t.Id == id) ?? slot[0];
    }

    /// <summary>The variant to show: the slot the setting names, or for Automatic the slot the OS asks for.</summary>
    public static ThemeVariant VariantFor(AppSettings settings, bool osPrefersDark) => settings.Theme switch
    {
        AppTheme.Light => Resolve(settings.LightTheme, dark: false).Variant,
        AppTheme.Dark => Resolve(settings.DarkTheme, dark: true).Variant,
        _ => osPrefersDark ? Resolve(settings.DarkTheme, dark: true).Variant : Resolve(settings.LightTheme, dark: false).Variant,
    };
}
