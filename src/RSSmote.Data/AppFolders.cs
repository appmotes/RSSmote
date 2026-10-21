namespace RSSmote.Data;

/// <summary>
/// Where the library and settings live (A4). <c>RSSMOTE_DATA_DIR</c> overrides the per-OS folder, for tests and
/// for running a throwaway library.
/// </summary>
public static class AppFolders
{
    public const string DatabaseFile = "rssmote.sqlite";
    public const string SettingsFile = "settings.json";

    public static string Data()
    {
        if (Environment.GetEnvironmentVariable("RSSMOTE_DATA_DIR") is { Length: > 0 } overridden)
            return overridden;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
            // SpecialFolder.ApplicationData is ~/.config on macOS, so build the native path.
            return Path.Combine(home, "Library", "Application Support", "com.ahrentlov.rssmote");
        if (OperatingSystem.IsWindows())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RSSmote");

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return Path.Combine(string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".local", "share") : xdg, "rssmote");
    }

    public static string DatabasePath() => Path.Combine(Data(), DatabaseFile);

    public static string SettingsPath() => Path.Combine(Data(), SettingsFile);
}
