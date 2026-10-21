using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RSSmote.App.Controls.Headless;

/// <summary>
/// A feed's favicon in a rounded square (D13): the bitmap, optionally greyscale, or a letter tile from
/// <see cref="Title"/> when there is no bitmap.
/// </summary>
public sealed class Favicon : Control
{
    private static readonly ConditionalWeakTable<Bitmap, Bitmap> GrayscaleCache = new();

    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<Favicon, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Favicon, string?>(nameof(Title));

    public static readonly StyledProperty<bool> IsGrayscaleProperty =
        AvaloniaProperty.Register<Favicon, bool>(nameof(IsGrayscale), true);

    public static readonly StyledProperty<double> SizeProperty =
        AvaloniaProperty.Register<Favicon, double>(nameof(Size), 19);

    public static readonly StyledProperty<double> RadiusProperty =
        AvaloniaProperty.Register<Favicon, double>(nameof(Radius), 3);

    public static readonly StyledProperty<IBrush?> TileBrushProperty =
        AvaloniaProperty.Register<Favicon, IBrush?>(nameof(TileBrush));

    public static readonly StyledProperty<IBrush?> LetterBrushProperty =
        AvaloniaProperty.Register<Favicon, IBrush?>(nameof(LetterBrush));

    static Favicon()
    {
        AffectsRender<Favicon>(SourceProperty, TitleProperty, IsGrayscaleProperty, RadiusProperty, TileBrushProperty, LetterBrushProperty);
        AffectsMeasure<Favicon>(SizeProperty);
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public bool IsGrayscale
    {
        get => GetValue(IsGrayscaleProperty);
        set => SetValue(IsGrayscaleProperty, value);
    }

    public double Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public double Radius
    {
        get => GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    public IBrush? TileBrush
    {
        get => GetValue(TileBrushProperty);
        set => SetValue(TileBrushProperty, value);
    }

    public IBrush? LetterBrush
    {
        get => GetValue(LetterBrushProperty);
        set => SetValue(LetterBrushProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize) => new(Size, Size);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect((Bounds.Width - Size) / 2, (Bounds.Height - Size) / 2, Size, Size);
        using var clip = context.PushClip(new RoundedRect(rect, Radius));

        if (Source is { } bitmap)
        {
            var image = IsGrayscale ? Grayscale(bitmap) : bitmap;
            context.DrawImage(image, new Rect(image.Size), rect);
            return;
        }

        if (TileBrush is { } tile)
            context.FillRectangle(tile, rect);

        var title = Title?.Trim();
        var letter = string.IsNullOrEmpty(title) ? "•" : char.ToLowerInvariant(title[0]).ToString();
        var text = new FormattedText(letter, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Medium), Size * 0.72, LetterBrush ?? Brushes.White);
        context.DrawText(text, new Point(rect.X + (Size - text.Width) / 2, rect.Y + (Size - text.Height) / 2));
    }

    /// <summary>Luminance copy of a BGRA bitmap, cached per source. Other pixel formats are returned unchanged.</summary>
    internal static Bitmap Grayscale(Bitmap source)
    {
        if (GrayscaleCache.TryGetValue(source, out var cached))
            return cached;
        if (source.Format is not { } format || format != PixelFormat.Bgra8888)
            return source;

        var size = source.PixelSize;
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            source.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), pixels.Length, stride);
        }
        finally
        {
            handle.Free();
        }

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var y = (byte)(0.114 * pixels[i] + 0.587 * pixels[i + 1] + 0.299 * pixels[i + 2]);
            pixels[i] = pixels[i + 1] = pixels[i + 2] = y;
        }

        var result = new WriteableBitmap(size, source.Dpi, PixelFormat.Bgra8888, source.AlphaFormat ?? AlphaFormat.Premul);
        using (var buffer = result.Lock())
        {
            for (var row = 0; row < size.Height; row++)
                Marshal.Copy(pixels, row * stride, buffer.Address + row * buffer.RowBytes, stride);
        }

        GrayscaleCache.AddOrUpdate(source, result);
        return result;
    }
}
