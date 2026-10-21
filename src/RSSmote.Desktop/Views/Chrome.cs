using Avalonia;

namespace RSSmote.Desktop.Views;

/// <summary>
/// Per-OS insets for the title strips (A5). macOS draws its traffic lights over the sidebar strip, Windows its
/// caption buttons over the article strip; Linux keeps system decorations (P2), so the strips need no room.
/// </summary>
public static class Chrome
{
    public static bool ExtendsIntoTitleBar => !OperatingSystem.IsLinux();

    public static Thickness SidebarStripPadding => OperatingSystem.IsMacOS() ? new Thickness(78, 0, 8, 0) : new Thickness(8, 0);

    /// <summary>Windows caption buttons are about 138 wide at 100 % (P3, to be measured).</summary>
    public static Thickness ArticleStripPadding => OperatingSystem.IsWindows() ? new Thickness(8, 0, 146, 0) : new Thickness(8, 0);

    public static bool UsesMenuButton => !OperatingSystem.IsMacOS();

    /// <summary>The filter box's shortcut hint.</summary>
    public static string FindShortcut => OperatingSystem.IsMacOS() ? "⌘F" : "Ctrl+F";

    /// <summary>The Add Feed shortcut as the empty state shows it.</summary>
    public static string AddFeedShortcut => OperatingSystem.IsMacOS() ? "⌘N" : "Ctrl+N";
}
