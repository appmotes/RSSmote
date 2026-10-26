using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.App.Theming;
using RSSmote.Core.Models;
using RSSmote.Core.Rendering;
using RSSmote.Data;

namespace RSSmote.App.ViewModels;

/// <summary>A pane whose text size the reader sets: the sidebar or Feeds root, the article list, the article (D31).</summary>
public enum TextPane
{
    Feeds,
    List,
    Article,
}

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
        MarkReadDays = [.. AppSettings.MarkReadDays.Select(d => new ChoiceViewModel(MarkReadAfterText(d), d, SetMarkReadAfterDaysCommand))];
        Panes =
        [
            new("Feeds", TextPane.Feeds, SelectPaneCommand),
            new("List", TextPane.List, SelectPaneCommand),
            new("Article", TextPane.Article, SelectPaneCommand),
        ];
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

    /// <summary>Days after which unread articles are marked read.</summary>
    public IReadOnlyList<ChoiceViewModel> MarkReadDays { get; }

    /// <summary>Feeds, List, Article: the pane the Aa popover or sheet sizes.</summary>
    public IReadOnlyList<ChoiceViewModel> Panes { get; }

    /// <summary>The last step of the scale; sliders run from 0 to here.</summary>
    public int MaxTextStep => AppSettings.TextSizes.Length - 1;

    /// <summary>The system's text size as a factor, which the phone heads set (iOS Larger Text, Android font size).</summary>
    [ObservableProperty]
    public partial double SystemTextScale { get; set; } = 1;

    partial void OnSystemTextScaleChanged(double value)
    {
        ApplyTextSizes(_settings.Current);
        OnPropertyChanged(nameof(SystemSizeText));
    }

    /// <summary>"System text size: 120 %", under the switch that follows it.</summary>
    public string SystemSizeText => $"System text size: {Math.Round(SystemTextScale * 100)} %";

    [ObservableProperty]
    public partial bool FollowSystemTextSize { get; set; }

    partial void OnFollowSystemTextSizeChanged(bool value) => Write(s => s.FollowSystemTextSize = value);

    /// <summary>How much larger the sidebar or Feeds root lays out its rows; the system's size included.</summary>
    [ObservableProperty]
    public partial double FeedsScale { get; private set; } = 1;

    [ObservableProperty]
    public partial double ListScale { get; private set; } = 1;

    /// <summary>The article body's size in pixels.</summary>
    [ObservableProperty]
    public partial double ArticleFontSize { get; private set; } = ArticleTemplate.StandardFontSize;

    /// <summary>The panes' steps on the scale, as the sliders show them.</summary>
    [ObservableProperty]
    public partial double FeedsStep { get; set; }

    [ObservableProperty]
    public partial double ListStep { get; set; }

    [ObservableProperty]
    public partial double ArticleStep { get; set; }

    partial void OnFeedsStepChanged(double value) => WriteStep(TextPane.Feeds, value);

    partial void OnListStepChanged(double value) => WriteStep(TextPane.List, value);

    partial void OnArticleStepChanged(double value) => WriteStep(TextPane.Article, value);

    /// <summary>"120 %", or "100 % · Standard".</summary>
    [ObservableProperty]
    public partial string FeedsSizeText { get; private set; } = "";

    [ObservableProperty]
    public partial string ListSizeText { get; private set; } = "";

    [ObservableProperty]
    public partial string ArticleSizeText { get; private set; } = "";

    /// <summary>The pane chosen in the Aa popover or sheet.</summary>
    [ObservableProperty]
    public partial TextPane SelectedPane { get; set; } = TextPane.Article;

    partial void OnSelectedPaneChanged(TextPane value) => ShowSelectedPane();

    /// <summary>The chosen pane's step, for the popover's and the sheet's one slider.</summary>
    [ObservableProperty]
    public partial double PaneStep { get; set; }

    partial void OnPaneStepChanged(double value) => WriteStep(SelectedPane, value);

    [ObservableProperty]
    public partial string PaneSizeText { get; private set; } = "";

    /// <summary>The desktop's Aa popover is open; ⌘/Ctrl + and − then size its pane instead of the article.</summary>
    public bool IsTextSizeOpen { get; set; }

    /// <summary>The pane ⌘/Ctrl + / − / 0 change.</summary>
    public TextPane KeyPane => IsTextSizeOpen ? SelectedPane : TextPane.Article;

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
    public partial string MarkReadText { get; private set; } = "";

    [ObservableProperty]
    public partial bool HideEmptyFeeds { get; set; }

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

    partial void OnHideEmptyFeedsChanged(bool value) => Write(s => s.HideEmptyFeeds = value);

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
            HideEmptyFeeds = settings.HideEmptyFeeds;
            FollowSystemTextSize = settings.FollowSystemTextSize;
            ApplyTextSizes(settings);
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
        Check(MarkReadDays, settings.MarkReadAfterDays);
        AppearanceText = Appearances.FirstOrDefault(c => c.IsChecked)?.Header ?? "";
        RefreshIntervalText = IntervalText(settings.RefreshIntervalMinutes);
        KeepReadText = KeepText(settings.KeepArchivedDays);
        MarkReadText = MarkReadAfterText(settings.MarkReadAfterDays);
    }

    private static void Check(IEnumerable<ChoiceViewModel> choices, object key)
    {
        foreach (var choice in choices)
            choice.IsChecked = Equals(choice.Key, key);
    }

    /// <summary>Steps, labels and scales from <paramref name="settings"/>; the sliders' writes are muted meanwhile.</summary>
    private void ApplyTextSizes(AppSettings settings)
    {
        var applying = _applying;
        _applying = true;
        try
        {
            FeedsStep = StepOf(settings.FeedsTextSize);
            ListStep = StepOf(settings.ListTextSize);
            ArticleStep = StepOf(settings.ArticleTextSize);
            FeedsSizeText = SizeText(settings.FeedsTextSize);
            ListSizeText = SizeText(settings.ListTextSize);
            ArticleSizeText = SizeText(settings.ArticleTextSize);
            var system = settings.FollowSystemTextSize ? SystemTextScale : 1;
            FeedsScale = settings.FeedsTextSize / 100.0 * system;
            ListScale = settings.ListTextSize / 100.0 * system;
            ArticleFontSize = ArticleTemplate.StandardFontSize * settings.ArticleTextSize / 100.0 * system;
            ShowSelectedPane();
        }
        finally
        {
            _applying = applying;
        }
    }

    private void ShowSelectedPane()
    {
        var applying = _applying;
        _applying = true;
        PaneStep = SelectedPane switch { TextPane.Feeds => FeedsStep, TextPane.List => ListStep, _ => ArticleStep };
        PaneSizeText = SelectedPane switch { TextPane.Feeds => FeedsSizeText, TextPane.List => ListSizeText, _ => ArticleSizeText };
        _applying = applying;
        Check(Panes, SelectedPane);
    }

    /// <summary>A slider moves in fractions; the file is written only when it reaches another step.</summary>
    private void WriteStep(TextPane pane, double step)
    {
        var size = AppSettings.TextSizes[Math.Clamp((int)Math.Round(step), 0, MaxTextStep)];
        if (size != SizeOf(_settings.Current, pane))
            Write(s => SetSize(s, pane, size));
    }

    private static int StepOf(int size) => Array.IndexOf(AppSettings.TextSizes, AppSettings.NearestTextSize(size));

    public static string SizeText(int size) => size == AppSettings.StandardTextSize ? $"{size} % · Standard" : $"{size} %";

    private static int SizeOf(AppSettings settings, TextPane pane) => pane switch
    {
        TextPane.Feeds => settings.FeedsTextSize,
        TextPane.List => settings.ListTextSize,
        _ => settings.ArticleTextSize,
    };

    private static void SetSize(AppSettings settings, TextPane pane, int size)
    {
        switch (pane)
        {
            case TextPane.Feeds:
                settings.FeedsTextSize = size;
                break;
            case TextPane.List:
                settings.ListTextSize = size;
                break;
            default:
                settings.ArticleTextSize = size;
                break;
        }
    }

    /// <summary><paramref name="delta"/> steps for <paramref name="pane"/>; 0 returns it to Standard.</summary>
    public void ChangeTextSize(TextPane pane, int delta) =>
        _settings.Update(s => SetSize(s, pane, AppSettings.StepTextSize(SizeOf(s, pane), delta)));

    [RelayCommand]
    private void SelectPane(TextPane pane) => SelectedPane = pane;

    [RelayCommand]
    private void Smaller(TextPane pane) => ChangeTextSize(pane, -1);

    [RelayCommand]
    private void Larger(TextPane pane) => ChangeTextSize(pane, 1);

    [RelayCommand]
    private void StandardSize(TextPane pane) => ChangeTextSize(pane, 0);

    /// <summary>Every pane back to Standard.</summary>
    [RelayCommand]
    private void ResetTextSizes() => _settings.Update(s =>
        s.FeedsTextSize = s.ListTextSize = s.ArticleTextSize = AppSettings.StandardTextSize);

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

    /// <summary>"5 days", or "Never" for 0.</summary>
    public static string MarkReadAfterText(int days) => days == 0 ? "Never" : KeepText(days);

    [RelayCommand]
    private void SetMarkReadAfterDays(int days) => _settings.Update(s => s.MarkReadAfterDays = days);

    [RelayCommand]
    private void ToggleGrayscaleFavicons() => _settings.Update(s => s.GrayscaleFavicons = !s.GrayscaleFavicons);

    [RelayCommand]
    private void SetSortNewestFirst(bool newestFirst) => _settings.Update(s => s.SortNewestFirst = newestFirst);

    [RelayCommand]
    private void SetGroupByDate(bool group) => _settings.Update(s => s.GroupByDate = group);
}
