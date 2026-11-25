#:package SkiaSharp
#:package SkiaSharp.NativeAssets.Linux
// Draws the app icon from its SVG master (D13) into rounded-tile PNGs from 16 to 1024 (icon-N.png).
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

byte[] Png(int size)
{
    var info = new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var surface = SKSurface.Create(info);
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    using var paint = new SKPaint { IsAntialias = true, Color = background };
    canvas.Scale(size / side);
    var radius = Num((string)rect.Attribute("rx")!);
    canvas.DrawRoundRect(0, 0, side, side, radius, radius, paint);
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
foreach (var size in sizes)
{
    File.WriteAllBytes(Path.Combine(output, $"icon-{size}.png"), Png(size));
}

Console.WriteLine($"Icons written to {output}");
