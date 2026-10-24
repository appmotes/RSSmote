using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Theming;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>
/// The preferences the phone's Settings pages and the desktop menus show (D22, D27). Each switch and choice writes
/// straight to the settings store; <see cref="Apply"/> brings them back in line when settings change elsewhere.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private bool _applying;

    public SettingsViewModel(SettingsStore settings)
    {
        _settings = settings;
        Appearances =
        [
            new("Automatic", AppTheme.Automatic, SetThemeCommand),
            new("Light", AppTheme.Light, SetThemeCommand),
            new("Dark", AppTheme.Dark, SetThemeCommand),
        ];
        LightThemes = [.. ThemeCatalogue.Light.Select(t => new ChoiceViewModel(DisplayName(t), t.Id, SetLightThemeCommand, t.Variant))];
        DarkThemes = [.. ThemeCatalogue.Dark.Select(t => new ChoiceViewModel(DisplayName(t), t.Id, SetDarkThemeCommand, t.Variant))];
        RefreshIntervals = [.. AppSettings.RefreshIntervals.Select(m => new ChoiceViewModel(IntervalText(m), m, SetRefreshIntervalCommand))];
        KeepReadDays = [.. AppSettings.RetentionDays.Select(d => new ChoiceViewModel(KeepText(d), d, SetKeepReadDaysCommand))];
        Fonts = [.. Enum.GetValues<ArticleFont>().Select(f => new ChoiceViewModel(f.ToString(), f, SetArticleFontCommand))];
        Apply(settings.Current);
    }

    /// <summary>Automatic, Light, Dark: which theme slot shows (D22).</summary>
    public IReadOnlyList<ChoiceViewModel> Appearances { get; }

    /// <summary>Palettes for the light slot.</summary>
    public IReadOnlyList<ChoiceViewModel> LightThemes { get; }

    /// <summary>Palettes for the dark slot.</summary>
    public IReadOnlyList<ChoiceViewModel> DarkThemes { get; }

    /// <summary>Minutes between automatic refreshes.</summary>
    public IReadOnlyList<ChoiceViewModel> RefreshIntervals { get; }

    /// <summary>Days read articles are kept.</summary>
    public IReadOnlyList<ChoiceViewModel> KeepReadDays { get; }

    /// <summary>System, Serif, Rounded, Mono: the article's typeface (D26).</summary>
    public IReadOnlyList<ChoiceViewModel> Fonts { get; }

    public int MinFontSize => AppSettings.MinFontSize;

    public int MaxFontSize => AppSettings.MaxFontSize;

    /// <summary>The article's text size in points; the Aa sheet's slider writes it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizeText))]
    public partial double ArticleFontSize { get; set; }

    partial void OnArticleFontSizeChanged(double value)
    {
        var size = Math.Clamp((int)Math.Round(value), AppSettings.MinFontSize, AppSettings.MaxFontSize);
        // A slider moves in small steps; the file is written only when the whole point changes.
        if (size != _settings.Current.ArticleFontSize)
            Write(s => s.ArticleFontSize = size);
    }

    /// <summary>"17 pt".</summary>
    public string FontSizeText => $"{(int)Math.Round(ArticleFontSize)} pt";

    /// <summary>"RSSmote 0.1.0".</summary>
    public string VersionText { get; } =
        "RSSmote " + (typeof(SettingsViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion.Split('+')[0] ?? "");

    [ObservableProperty]
    public partial string AppearanceText { get; private set; } = "";

    [ObservableProperty]
    public partial string RefreshIntervalText { get; private set; } = "";

    [ObservableProperty]
    public partial string KeepReadText { get; private set; } = "";

    [ObservableProperty]
    public partial bool MarkAsReadOnSelect { get; set; }

    [ObservableProperty]
    public partial bool SortNewestFirst { get; set; }

    [ObservableProperty]
    public partial bool GroupByDate { get; set; }

    [ObservableProperty]
    public partial bool DisplayFavicons { get; set; }

    [ObservableProperty]
    public partial bool GrayscaleFavicons { get; set; }

    partial void OnMarkAsReadOnSelectChanged(bool value) => Write(s => s.MarkAsReadOnSelect = value);

    partial void OnSortNewestFirstChanged(bool value) => Write(s => s.SortNewestFirst = value);

    partial void OnGroupByDateChanged(bool value) => Write(s => s.GroupByDate = value);

    partial void OnDisplayFaviconsChanged(bool value) => Write(s => s.DisplayFavicons = value);

    partial void OnGrayscaleFaviconsChanged(bool value) => Write(s => s.GrayscaleFavicons = value);

    private void Write(Action<AppSettings> change)
    {
        if (!_applying)
            _settings.Update(change);
    }

    /// <summary>Shows <paramref name="settings"/>; on the UI thread.</summary>
    public void Apply(AppSettings settings)
    {
        _applying = true;
        try
        {
            MarkAsReadOnSelect = settings.MarkAsReadOnSelect;
            SortNewestFirst = settings.SortNewestFirst;
            GroupByDate = settings.GroupByDate;
            DisplayFavicons = settings.DisplayFavicons;
            GrayscaleFavicons = settings.GrayscaleFavicons;
            ArticleFontSize = settings.ArticleFontSize;
        }
        finally
        {
            _applying = false;
        }

        var light = ThemeCatalogue.Resolve(settings.LightTheme, dark: false).Id;
        var dark = ThemeCatalogue.Resolve(settings.DarkTheme, dark: true).Id;
        Check(Appearances, settings.Theme);
        Check(LightThemes, light);
        Check(DarkThemes, dark);
        Check(RefreshIntervals, settings.RefreshIntervalMinutes);
        Check(KeepReadDays, settings.KeepArchivedDays);
        Check(Fonts, settings.ArticleFont);
        AppearanceText = Appearances.FirstOrDefault(c => c.IsChecked)?.Header ?? "";
        RefreshIntervalText = IntervalText(settings.RefreshIntervalMinutes);
        KeepReadText = KeepText(settings.KeepArchivedDays);
    }

    private static void Check(IEnumerable<ChoiceViewModel> choices, object key)
    {
        foreach (var choice in choices)
            choice.IsChecked = Equals(choice.Key, key);
    }

    /// <summary>"Contrast Dark": the palette's name with its words apart.</summary>
    private static string DisplayName(ThemeOption theme) => theme.Name == "ContrastDark" ? "Contrast Dark" : theme.Name;

    public static string IntervalText(int minutes) => minutes switch
    {
        0 => "Manually",
        60 => "1 hour",
        _ when minutes % 60 == 0 => $"{minutes / 60} hours",
        _ => $"{minutes} min",
    };

    public static string KeepText(int days) => days switch
    {
        0 => "Forever",
        1 => "1 day",
        _ => $"{days} days",
    };

    [RelayCommand]
    private void SetTheme(AppTheme theme) => _settings.Update(s => s.Theme = theme);

    /// <summary>Picks the light slot's palette and shows it: the appearance leaves Automatic for Light (D22).</summary>
    [RelayCommand]
    private void SetLightTheme(string id) => _settings.Update(s =>
    {
        s.LightTheme = id;
        s.Theme = AppTheme.Light;
    });

    /// <summary>Picks the dark slot's palette and shows it: the appearance leaves Automatic for Dark (D22).</summary>
    [RelayCommand]
    private void SetDarkTheme(string id) => _settings.Update(s =>
    {
        s.DarkTheme = id;
        s.Theme = AppTheme.Dark;
    });

    [RelayCommand]
    private void SetRefreshInterval(int minutes) => _settings.Update(s => s.RefreshIntervalMinutes = minutes);

    [RelayCommand]
    private void SetKeepReadDays(int days) => _settings.Update(s => s.KeepArchivedDays = days);

    [RelayCommand]
    private void SetArticleFont(ArticleFont font) => _settings.Update(s => s.ArticleFont = font);

    /// <summary>One point smaller or larger, within 11–32.</summary>
    [RelayCommand]
    private void StepFontSize(int delta) => _settings.Update(s =>
        s.ArticleFontSize = Math.Clamp(s.ArticleFontSize + delta, AppSettings.MinFontSize, AppSettings.MaxFontSize));

    [RelayCommand]
    private void ToggleGrayscaleFavicons() => _settings.Update(s => s.GrayscaleFavicons = !s.GrayscaleFavicons);

    [RelayCommand]
    private void SetSortNewestFirst(bool newestFirst) => _settings.Update(s => s.SortNewestFirst = newestFirst);

    [RelayCommand]
    private void SetGroupByDate(bool group) => _settings.Update(s => s.GroupByDate = group);
}
