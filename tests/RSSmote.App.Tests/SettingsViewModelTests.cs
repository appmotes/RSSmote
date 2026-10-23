using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.App.Tests;

/// <summary>The preferences shared by the phone's Settings pages and the desktop menus (D22, D27).</summary>
public sealed class SettingsViewModelTests : IDisposable
{
    private readonly ShellFixture _f = new();

    private SettingsViewModel Settings => _f.Shell.Settings;

    public void Dispose() => _f.Dispose();

    [Fact]
    public void A_switch_writes_the_setting_and_follows_a_change_made_elsewhere()
    {
        Settings.MarkAsReadOnSelect = false;
        Assert.False(_f.Settings.Current.MarkAsReadOnSelect);

        _f.Settings.Update(s => s.GroupByDate = false);
        Assert.False(Settings.GroupByDate);
        Assert.True(Settings.SortNewestFirst);
    }

    [Fact]
    public void Picking_a_palette_shows_it_and_checks_one_row_per_list()
    {
        Assert.Equal("Automatic", Settings.AppearanceText);

        Settings.DarkThemes.Single(t => t.Header == "Fjord").Command.Execute("fjord");
        Assert.Equal(AppTheme.Dark, _f.Settings.Current.Theme);
        Assert.Equal("Dark", Settings.AppearanceText);
        Assert.Equal(["Fjord"], Settings.DarkThemes.Where(t => t.IsChecked).Select(t => t.Header));
        Assert.Equal(["Sand"], Settings.LightThemes.Where(t => t.IsChecked).Select(t => t.Header));
        Assert.Equal("Contrast Dark", Settings.DarkThemes[^1].Header);
        Assert.All(Settings.DarkThemes, t => Assert.NotNull(t.Variant));
    }

    [Fact]
    public void The_refresh_pickers_name_their_values()
    {
        Assert.Equal(["Manually", "15 min", "30 min", "1 hour", "2 hours"], Settings.RefreshIntervals.Select(c => c.Header));
        Assert.Equal(["7 days", "14 days", "30 days", "90 days", "Forever"], Settings.KeepReadDays.Select(c => c.Header));
        Assert.Equal("30 min", Settings.RefreshIntervalText);

        Settings.SetRefreshIntervalCommand.Execute(60);
        Settings.SetKeepReadDaysCommand.Execute(0);
        Assert.Equal(60, _f.Settings.Current.RefreshIntervalMinutes);
        Assert.Equal("1 hour", Settings.RefreshIntervalText);
        Assert.Equal("Forever", Settings.KeepReadText);
        Assert.True(Settings.KeepReadDays[^1].IsChecked);
    }

    [Fact]
    public void The_version_line_names_the_product()
    {
        Assert.Matches(@"^RSSmote \d+\.\d+\.\d+$", Settings.VersionText);
    }
}
