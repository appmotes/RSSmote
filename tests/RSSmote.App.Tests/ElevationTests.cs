using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using System.Runtime.InteropServices;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using RSSmote.App.Theming;

namespace RSSmote.App.Tests;

public class ElevationTests
{
    public static TheoryData<string> Themes => new(ThemeCatalogue.All.Select(t => t.Id));

    private static ThemeVariant Variant(string id) => ThemeCatalogue.All.Single(t => t.Id == id).Variant;

    /// <summary>
    /// Shadows are palette keys (D20): a <c>BoxShadows</c> resource in each theme file, read with DynamicResource.
    /// Palettes where a shadow would not read (Black, the contrast pair, Phosphor) draw a ring instead.
    /// </summary>
    [AvaloniaTheory]
    [MemberData(nameof(Themes))]
    public void Shadow_keys_reach_a_border_in_every_theme(string theme)
    {
        Application.Current!.RequestedThemeVariant = Variant(theme);
        var card = new Border { Width = 100, Height = 60 };
        card[!Border.BoxShadowProperty] = card.GetResourceObservable("BoxShadows.Card").ToBinding();
        var window = new Window { Width = 200, Height = 120, Content = card };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        Assert.True(card.BoxShadow.Count > 0);
        var first = card.BoxShadow[0];
        Assert.True(first.Blur > 0 || first.Spread > 0);

        foreach (var key in new[] { "BoxShadows.Raised", "BoxShadows.Panel" })
            Assert.True(card.TryFindResource(key, card.ActualThemeVariant, out var value) && value is BoxShadows);
        window.Close();
    }

    private static uint PixelBesideCard(bool clip)
    {
        var card = new Border
        {
            Width = 100, Height = 60, Background = Brushes.White, CornerRadius = new CornerRadius(12),
            BoxShadow = BoxShadows.Parse("0 0 0 6 #FF000000"), ClipToBounds = clip,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var window = new Window { Width = 200, Height = 120, Background = Brushes.White, Content = card };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            frame.CopyPixels(new PixelRect(47, 60, 1, 1), buffer, 4, 4); // 3 px left of the card, inside the shadow
            return (uint)Marshal.ReadInt32(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            window.Close();
        }
    }

    /// <summary>L12: a border that clips its content also clips its own shadow, so a card needs two borders.</summary>
    [AvaloniaFact]
    public void A_clipping_border_clips_its_own_shadow()
    {
        Assert.Equal(0xFF000000u, PixelBesideCard(clip: false));
        Assert.Equal(0xFFFFFFFFu, PixelBesideCard(clip: true));
    }

    /// <summary>L14: the cards sit flush with their views' left edge, so the ring there is drawn outside the view.</summary>
    [AvaloniaFact]
    public void The_card_ring_shows_on_every_edge()
    {
        Application.Current!.RequestedThemeVariant = ThemeCatalogue.ContrastDark;
        using var f = new ShellFixture();
        var window = new Window { Width = 1045, Height = 660, Content = new RSSmote.App.Views.ShellView(), DataContext = f.Shell };
        window.Show();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame()!;
        var buffer = Marshal.AllocHGlobal(4);
        try
        {
            // Near the list card's right edge (653) and the left edges of both cards (232, 660): find the ring at each.
            uint At(int x)
            {
                frame.CopyPixels(new PixelRect(x, 300, 1, 1), buffer, 4, 4);
                return (uint)Marshal.ReadInt32(buffer);
            }
            foreach (var edge in new[] { 653, 232, 660 })
            {
                var seen = Enumerable.Range(edge - 4, 9).Select(x => $"{x}:{At(x):X8}").ToList();
                Assert.True(seen.Any(s => s.EndsWith("FFABABAB")), string.Join(" ", seen));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            window.Close();
            Application.Current.RequestedThemeVariant = ThemeVariant.Light;
        }
    }
}
