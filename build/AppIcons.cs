#:package SkiaSharp
#:package SkiaSharp.NativeAssets.Linux
// Draws the app icon from its SVG master (D13) into PNGs at every size the packages need, and a Windows .ico.
// Usage: dotnet run build/AppIcons.cs -- <AppIcon.svg> <output folder>
// The SVG is the master's own simple shape: a rounded rect, then a group with one transform and one path.
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
var transform = Regex.Match((string)group.Attribute("transform")!, @"translate\(([\d.]+) ([\d.]+)\) scale\(([\d.]+)\)");
var star = SKPath.ParseSvgPathData((string)group.Element(ns + "path")!.Attribute("d")!);
star.Transform(SKMatrix.Concat(
    SKMatrix.CreateTranslation(Num(transform.Groups[1].Value), Num(transform.Groups[2].Value)),
    SKMatrix.CreateScale(Num(transform.Groups[3].Value), Num(transform.Groups[3].Value))));

byte[] Png(int size)
{
    using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(size / side);
    using var paint = new SKPaint { IsAntialias = true, Color = SKColor.Parse((string)rect.Attribute("fill")!) };
    var radius = Num((string)rect.Attribute("rx")!);
    canvas.DrawRoundRect(0, 0, side, side, radius, radius, paint);
    paint.Color = SKColor.Parse((string)group.Attribute("fill")!);
    canvas.DrawPath(star, paint);
    paint.Style = SKPaintStyle.Stroke;
    paint.StrokeJoin = SKStrokeJoin.Round;
    paint.StrokeWidth = Num((string)group.Attribute("stroke-width")!) * Num(transform.Groups[3].Value);
    canvas.DrawPath(star, paint);
    using var image = surface.Snapshot();
    return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
}

int[] sizes = [16, 24, 32, 48, 64, 128, 256, 512, 1024];
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
Console.WriteLine($"Icons written to {output}");
