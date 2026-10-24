namespace RSSmote.Core.Models;

/// <summary>Which of the two theme slots shows: the light one, the dark one, or the one the OS asks for (D22).</summary>
public enum AppTheme
{
    Automatic,
    Light,
    Dark,
    /// <summary>Read from older settings only: normalised to <see cref="Dark"/> with the Black palette.</summary>
    Black,
}

public enum ArticleFont
{
    System,
    Serif,
    Rounded,
    Mono,
}

/// <summary>
/// User preferences, persisted as JSON by the settings store (A8). Collections are replaced, never mutated,
/// so a <c>with</c> copy is a safe snapshot.
/// </summary>
public sealed record AppSettings
{
    public static readonly int[] RefreshIntervals = [0, 15, 30, 60, 120];
    public static readonly int[] RetentionDays = [7, 14, 30, 90, 0];
    public static readonly int[] MarkReadDays = [1, 3, 5, 7, 14, 30, 0];
    public const int MinFontSize = 11;
    public const int MaxFontSize = 32;
    public const int DefaultFontSize = 16;

    /// <summary>Minutes between automatic refreshes; 0 refreshes only on request.</summary>
    public int RefreshIntervalMinutes { get; set; } = 30;

    /// <summary>Read, unstarred items older than this many days are deleted; 0 keeps everything.</summary>
    public int KeepArchivedDays { get; set; } = 30;

    /// <summary>Unread items older than this many days are marked read; 0 leaves them unread.</summary>
    public int MarkReadAfterDays { get; set; } = 5;

    /// <summary>The sidebars list only folders and feeds with articles to show (D10, D24).</summary>
    public bool HideEmptyFeeds { get; set; } = true;

    public bool SortNewestFirst { get; set; } = true;
    public bool MarkAsReadOnSelect { get; set; } = true;
    public bool OpenLinksInBackground { get; set; }
    public bool DisplayFavicons { get; set; } = true;
    public bool GrayscaleFavicons { get; set; } = true;
    public bool GroupByDate { get; set; } = true;
    public AppTheme Theme { get; set; } = AppTheme.Automatic;

    /// <summary>Palette id for the light slot; an id the app does not know falls back to its default (D22).</summary>
    public string LightTheme { get; set; } = DefaultLightTheme;

    /// <summary>Palette id for the dark slot.</summary>
    public string DarkTheme { get; set; } = DefaultDarkTheme;

    public const string DefaultLightTheme = "sand";
    public const string DefaultDarkTheme = "ember";
    public const string BlackTheme = "black";
    public ArticleFont ArticleFont { get; set; } = ArticleFont.System;
    public int ArticleFontSize { get; set; } = DefaultFontSize;
    public StreamFilter Filter { get; set; } = StreamFilter.Unread;
    public long[] ExpandedFolders { get; set; } = [];
    public string[] CollapsedSections { get; set; } = [];
    public DateTimeOffset? LastRefresh { get; set; }

    /// <summary>A copy with every value inside its allowed range.</summary>
    public AppSettings Normalized() => this with
    {
        RefreshIntervalMinutes = RefreshIntervals.Contains(RefreshIntervalMinutes) ? RefreshIntervalMinutes : 30,
        KeepArchivedDays = Math.Max(0, KeepArchivedDays),
        MarkReadAfterDays = Math.Max(0, MarkReadAfterDays),
        ArticleFontSize = Math.Clamp(ArticleFontSize, MinFontSize, MaxFontSize),
        Theme = Theme == AppTheme.Black ? AppTheme.Dark : Enum.IsDefined(Theme) ? Theme : AppTheme.Automatic,
        LightTheme = string.IsNullOrWhiteSpace(LightTheme) ? DefaultLightTheme : LightTheme,
        DarkTheme = Theme == AppTheme.Black ? BlackTheme : string.IsNullOrWhiteSpace(DarkTheme) ? DefaultDarkTheme : DarkTheme,
        ArticleFont = Enum.IsDefined(ArticleFont) ? ArticleFont : ArticleFont.System,
        Filter = Enum.IsDefined(Filter) ? Filter : StreamFilter.Unread,
        ExpandedFolders = ExpandedFolders ?? [],
        CollapsedSections = CollapsedSections ?? [],
    };
}
