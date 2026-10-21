using System.Globalization;
using System.Xml.Linq;

namespace RSSmote.App.Tests;

/// <summary>
/// The contrast floors every palette must clear (D22), measured as WCAG 2 ratios straight from the theme files.
/// A translucent foreground is composited over its background first.
/// </summary>
public class ContrastTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string> Palettes => new(Directory.GetFiles(ThemesFolder(), "*.axaml").Select(f => Path.GetFileNameWithoutExtension(f)!));

    private static readonly (string Ink, string Background, double Floor)[] Floors =
    [
        ("Ink.Primary", "Bg.List", 7), ("Ink.Primary", "Bg.Article", 7), ("Ink.Primary", "Bg.Panel", 7),
        ("Ink.Primary", "Selection.List", 7), ("Ink.Sidebar", "Bg.Sidebar", 7),
        ("Ink.Secondary", "Bg.List", 4.5), ("Ink.Secondary", "Selection.List", 4.5), ("Ink.Secondary", "Bg.Sidebar", 4.5),
        ("Ink.Secondary", "Bg.Panel", 4.5), ("Ink.Secondary", "Bg.Pill", 4.5),
        ("Ink.Read", "Bg.List", 4),
        ("Accent", "Bg.Article", 4.5), ("Ink.OnAccent", "Accent", 4.5),
        ("Ink.Danger", "Bg.Panel", 4.5), ("Ink.Danger", "Bg.Pill", 4.5),
        ("Ink.Icon", "Bg.List", 4.5), ("Ink.OnTile", "Bg.FaviconTile", 3),
    ];

    private static string ThemesFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RSSmote.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "RSSmote.App", "Assets", "Themes");
    }

    private static Dictionary<string, uint> Colors(string palette) =>
        XDocument.Load(Path.Combine(ThemesFolder(), palette + ".axaml")).Root!.Elements()
            .Where(e => e.Name.LocalName == "Color" && ((string?)e.Attribute(X + "Key"))?.StartsWith("Color.") == true)
            .ToDictionary(e => ((string)e.Attribute(X + "Key")!)["Color.".Length..],
                          e => uint.Parse(e.Value.Trim().TrimStart('#'), NumberStyles.HexNumber));

    private static double Channel(uint argb, int shift) => ((argb >> shift) & 0xFF) / 255.0;

    private static (double R, double G, double B) Over(uint fg, uint bg)
    {
        var a = Channel(fg, 24);
        double Mix(int shift) => Channel(fg, shift) * a + Channel(bg, shift) * (1 - a);
        return (Mix(16), Mix(8), Mix(0));
    }

    private static double Luminance((double R, double G, double B) c)
    {
        static double Linear(double v) => v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
    }

    private static double Ratio(uint fg, uint bg)
    {
        var a = Luminance(Over(fg, bg));
        var b = Luminance(Over(bg, 0xFF000000));
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Every_palette_clears_the_contrast_floors(string palette)
    {
        var colors = Colors(palette);
        var misses = Floors
            .Select(f => (f.Ink, f.Background, f.Floor, Ratio: Ratio(colors[f.Ink], colors[f.Background])))
            .Where(f => f.Ratio < f.Floor)
            .Select(f => $"{f.Ink} on {f.Background}: {f.Ratio:0.00} < {f.Floor}")
            .ToList();
        Assert.True(misses.Count == 0, $"{palette}: {string.Join("; ", misses)}");
    }
}
