using RSSmote.Core.Models;

namespace RSSmote.Data.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "rssmote-settings-" + Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void A_missing_file_gives_the_defaults()
    {
        var settings = new SettingsStore(SettingsPath).Current;
        Assert.Equal(new AppSettings(), settings);
        Assert.Equal(30, settings.RefreshIntervalMinutes);
        Assert.Equal(StreamFilter.Unread, settings.Filter);
        Assert.Equal((5, true), (settings.MarkReadAfterDays, settings.HideEmptyFeeds));
        Assert.False(File.Exists(SettingsPath));
    }

    [Fact]
    public void Updates_persist_with_enums_as_text_and_leave_no_temp_file()
    {
        var store = new SettingsStore(SettingsPath);
        store.Update(s =>
        {
            s.Theme = AppTheme.Dark;
            s.DarkTheme = "fjord";
            s.ExpandedFolders = [3, 5];
            s.LastRefresh = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
        });

        var reloaded = new SettingsStore(SettingsPath).Current;
        Assert.Equal(AppTheme.Dark, reloaded.Theme);
        Assert.Equal("fjord", reloaded.DarkTheme);
        Assert.Equal(AppSettings.DefaultLightTheme, reloaded.LightTheme);
        Assert.Equal([3L, 5L], reloaded.ExpandedFolders);
        Assert.Equal(store.Current.LastRefresh, reloaded.LastRefresh);
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Fact]
    public void A_stored_black_theme_becomes_the_dark_slot_with_the_black_palette() // D22
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, """{ "theme": "Black" }""");

        var settings = new SettingsStore(SettingsPath).Current;
        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(AppSettings.BlackTheme, settings.DarkTheme);
        Assert.Equal(AppSettings.DefaultLightTheme, settings.LightTheme);
    }

    [Fact]
    public void Out_of_range_values_are_normalised()
    {
        var store = new SettingsStore(SettingsPath);
        store.Update(s =>
        {
            s.ArticleFontSize = 99;
            s.RefreshIntervalMinutes = 7;
            s.KeepArchivedDays = -3;
        });
        Assert.Equal(AppSettings.MaxFontSize, store.Current.ArticleFontSize);
        Assert.Equal(30, store.Current.RefreshIntervalMinutes);
        Assert.Equal(0, store.Current.KeepArchivedDays);
    }

    [Fact]
    public void An_unreadable_file_starts_from_the_defaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "{ not json");
        Assert.Equal(new AppSettings(), new SettingsStore(SettingsPath).Current);
    }

    [Fact]
    public void A_change_that_changes_nothing_is_not_written_or_announced()
    {
        var store = new SettingsStore(SettingsPath);
        var raised = 0;
        store.Changed += (_, _) => raised++;

        store.Update(s => s.Theme = AppTheme.Automatic);
        Assert.Equal(0, raised);
        Assert.False(File.Exists(SettingsPath));

        store.Update(s => s.Theme = AppTheme.Dark);
        Assert.Equal(1, raised);
    }
}
