using System.Xml.Linq;

namespace RSSmote.App.Tests;

public class PaletteTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string ThemesFolder()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "RSSmote.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "src", "RSSmote.App", "Assets", "Themes");
    }

    private static List<string> Keys(string palette) =>
        XDocument.Load(Path.Combine(ThemesFolder(), palette + ".axaml"))
            .Root!.Elements().Select(e => (string?)e.Attribute(X + "Key")).OfType<string>().ToList();

    [Fact]
    public void Every_palette_answers_the_same_keys_in_the_same_order() // D4
    {
        var sand = Keys("Sand");
        Assert.NotEmpty(sand);
        foreach (var palette in Directory.GetFiles(ThemesFolder(), "*.axaml").Select(Path.GetFileNameWithoutExtension))
            Assert.True(sand.SequenceEqual(Keys(palette!)), $"{palette} differs from Sand");
    }

    [Fact]
    public void Every_catalogue_palette_has_a_file() // D22
    {
        var files = Directory.GetFiles(ThemesFolder(), "*.axaml").Select(Path.GetFileNameWithoutExtension).ToHashSet();
        foreach (var theme in RSSmote.App.Theming.ThemeCatalogue.All)
            Assert.Contains(theme.Name, files);
    }
}
