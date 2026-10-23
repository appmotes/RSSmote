using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RSSmote.App.Controls.Headless;

namespace RSSmote.App.Tests;

public class FaviconTests
{
    private static byte[] Pixel(PixelFormat format, byte c0, byte c1, byte c2)
    {
        var bitmap = new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), format, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
            Marshal.Copy(new byte[] { c0, c1, c2, 255 }, 0, buffer.Address, 4);
        var grey = Favicon.Grayscale(bitmap);
        var pixel = new byte[4];
        var handle = GCHandle.Alloc(pixel, GCHandleType.Pinned);
        try
        {
            grey.CopyPixels(new PixelRect(0, 0, 1, 1), handle.AddrOfPinnedObject(), 4, 4);
        }
        finally
        {
            handle.Free();
        }
        return pixel;
    }

    [AvaloniaFact]
    public void Grayscale_weighs_red_alike_in_BGRA_and_RGBA() // D13
    {
        var bgra = Pixel(PixelFormat.Bgra8888, 0, 0, 255);
        var rgba = Pixel(PixelFormat.Rgba8888, 255, 0, 0);
        Assert.Equal([76, 76, 76, 255], bgra);
        Assert.Equal(bgra, rgba);
    }
}
