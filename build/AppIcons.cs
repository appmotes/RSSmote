#:package SkiaSharp
#:package SkiaSharp.NativeAssets.Linux
// Draws the app icon from its SVG master (D13) into PNGs at every size the packages and phone heads need, and a
// Windows .ico: rounded tiles (icon-N.png), an opaque square for iOS, which rounds it itself (ios-1024.png), and
// Android's adaptive foreground, the marks alone on 108 dp with the tile's 120 units on its visible 72
// (android-foreground-N.png, background colour in android-background.txt).
// Usage: dotnet run build/AppIcons.cs -- <AppIcon.svg> <output folder>
// The SVG is the master's own simple shape: a rounded rect, then one group with a scale and paths that are filled or
// stroked, each with an optional opacity; the group gives the defaults.
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SkiaSharp;

var svg = XDocument.Load(args[0]).Root!;
var output = Directory.CreateDirectory(args[1]).FullName;
XNamespace ns = "http://www.w3.org/2000/svg";
float Num(string s) => float.Parse(s, CultureInfo.InvariantCulture);

var side = Num((string)svg.Attribute("width")!);
var rect = svg.Element(ns + "rect")!;
var group = svg.Element(ns + "g")!;
var scale = Num(Regex.Match((string)group.Attribute("transform")!, @"scale\(([\d.]+)\)").Groups[1].Value);
string? Style(XElement path, string name) => (string?)path.Attribute(name) ?? (string?)group.Attribute(name);

var background = SKColor.Parse((string)rect.Attribute("fill")!);

byte[] Png(int size) => Draw(size, Tile.Rounded);

byte[] Draw(int size, Tile tile)
{
    var info = new SKImageInfo(size, size, SKColorType.Rgba8888, tile == Tile.Square ? SKAlphaType.Opaque : SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    using var paint = new SKPaint { IsAntialias = true, Color = background };
    if (tile == Tile.None)
    {
        // 108 dp, of which the middle 72 show: the tile's side maps onto those 72.
        canvas.Translate(size * 18f / 108, size * 18f / 108);
        canvas.Scale(size * 72f / 108 / side);
    }
    else
    {
        canvas.Scale(size / side);
        var radius = tile == Tile.Rounded ? Num((string)rect.Attribute("rx")!) : 0;
        canvas.DrawRoundRect(0, 0, side, side, radius, radius, paint);
    }
    canvas.Scale(scale);
    foreach (var element in group.Elements(ns + "path"))
    {
        using var path = SKPath.ParseSvgPathData((string)element.Attribute("d")!);
        var alpha = (byte)Math.Round(255 * Num(Style(element, "opacity") ?? "1"));
        if (Style(element, "fill") is { } fill and not "none")
        {
            using var filled = new SKPaint { IsAntialias = true, Color = SKColor.Parse(fill).WithAlpha(alpha) };
            canvas.DrawPath(path, filled);
        }
        if (Style(element, "stroke") is { } stroke and not "none")
        {
            using var stroked = new SKPaint
            {
                IsAntialias = true, Style = SKPaintStyle.Stroke, Color = SKColor.Parse(stroke).WithAlpha(alpha),
                StrokeWidth = Num(Style(element, "stroke-width") ?? "1"),
                StrokeCap = Style(element, "stroke-linecap") == "round" ? SKStrokeCap.Round : SKStrokeCap.Butt,
                StrokeJoin = SKStrokeJoin.Round,
            };
            canvas.DrawPath(path, stroked);
        }
    }
    using var image = surface.Snapshot();
    return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
}

int[] sizes = [16, 24, 32, 48, 64, 72, 96, 128, 144, 192, 256, 512, 1024];
var pngs = sizes.ToDictionary(s => s, Png);
foreach (var (size, data) in pngs)
    File.WriteAllBytes(Path.Combine(output, $"icon-{size}.png"), data);

// An .ico holds PNG entries from 16 to 256; a width or height of 256 is written as 0.
int[] icoSizes = [16, 24, 32, 48, 64, 128, 256];
using (var ico = new BinaryWriter(File.Create(Path.Combine(output, "RSSmote.ico"))))
{
    ico.Write((short)0);
    ico.Write((short)1);
    ico.Write((short)icoSizes.Length);
    var offset = 6 + 16 * icoSizes.Length;
    foreach (var size in icoSizes)
    {
        ico.Write((byte)(size % 256));
        ico.Write((byte)(size % 256));
        ico.Write((byte)0);
        ico.Write((byte)0);
        ico.Write((short)1);
        ico.Write((short)32);
        ico.Write(pngs[size].Length);
        ico.Write(offset);
        offset += pngs[size].Length;
    }
    foreach (var size in icoSizes)
        ico.Write(pngs[size]);
}
File.WriteAllBytes(Path.Combine(output, "ios-1024.png"), Draw(1024, Tile.Square));
foreach (var size in new[] { 108, 162, 216, 324, 432 })
    File.WriteAllBytes(Path.Combine(output, $"android-foreground-{size}.png"), Draw(size, Tile.None));
File.WriteAllText(Path.Combine(output, "android-background.txt"), (string)rect.Attribute("fill")!);
Console.WriteLine($"Icons written to {output}");

enum Tile { Rounded, Square, None }
