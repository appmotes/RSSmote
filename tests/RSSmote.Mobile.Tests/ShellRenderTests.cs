using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Tests;

public class ShellRenderTests
{
    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Root_renders_the_feeds_tab(string palette)
    {
        using var f = new ShellFixture();
        var shell = new MobileShell(f.Shell);
        var window = PhoneRender.Show(shell, palette);

        Assert.Equal(RootTab.Feeds, shell.Root.Tab);
        Assert.Contains("selected", shell.Root.FeedsTab.Classes);
        PhoneRender.Save(window, $"phone-feeds-{palette}");
        window.Close();
    }

    [AvaloniaTheory]
    [InlineData("Sand")]
    [InlineData("Ember")]
    public void Root_renders_the_settings_tab(string palette)
    {
        using var f = new ShellFixture();
        var shell = new MobileShell(f.Shell);
        var window = PhoneRender.Show(shell, palette);

        shell.Root.Show(RootTab.Settings);
        Assert.Contains("selected", shell.Root.SettingsTab.Classes);
        Assert.DoesNotContain("selected", shell.Root.FeedsTab.Classes);
        PhoneRender.Save(window, $"phone-settings-{palette}");
        window.Close();
    }

    [AvaloniaFact]
    public void Tab_bar_sits_at_the_bottom_at_its_token_height()
    {
        using var f = new ShellFixture();
        var shell = new MobileShell(f.Shell);
        var window = PhoneRender.Show(shell, "Sand");
        PhoneRender.Save(window, "phone-layout");

        var bar = shell.Root.FeedsTab.FindAncestorOfType<Border>()!;
        var top = bar.TranslatePoint(new Point(0, 0), window)!.Value.Y;
        Assert.Equal(49, bar.Bounds.Height);
        Assert.Equal(PhoneRender.Height - 49, top);
        Assert.Equal(22, shell.Root.FeedsTab.FindDescendantOfType<Icon>()!.Bounds.Height);
        window.Close();
    }
}
