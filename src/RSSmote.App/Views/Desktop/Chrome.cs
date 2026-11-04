using Avalonia;
using Avalonia.Input;

namespace RSSmote.App.Views.Desktop;

/// <summary>The title strips' insets and the shortcut hints, which name ⌘ on an Apple keyboard and Ctrl elsewhere (A5).</summary>
public static class Chrome
{
    public static KeyModifiers CommandModifier => App.CommandModifier;

    private static bool IsApple => CommandModifier == KeyModifiers.Meta;

    public static Thickness SidebarStripPadding { get; } = new(8, 0);

    public static Thickness ArticleStripPadding { get; } = new(8, 0);

    /// <summary>The filter box's shortcut hint.</summary>
    public static string FindShortcut => IsApple ? "⌘F" : "Ctrl+F";

    /// <summary>The Settings button's tooltip, with its shortcut.</summary>
    public static string SettingsTip => IsApple ? "Settings (⌘,)" : "Settings (Ctrl+,)";

    /// <summary>The line under the text sizes in Settings (D31).</summary>
    public static string TextSizeHint => (IsApple ? "⌘+ and ⌘−" : "Ctrl++ and Ctrl+−")
        + " change the article, or the pane chosen under Aa. Aa beside the gear opens the same choice while you read.";

    /// <summary>The Add Feed shortcut as the empty state shows it.</summary>
    public static string AddFeedShortcut => IsApple ? "⌘N" : "Ctrl+N";
}
