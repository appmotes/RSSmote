using System.Collections.Concurrent;
using Avalonia.Media.Imaging;
using RSSmote.Core.Models;

namespace RSSmote.App.Services;

/// <summary>Loads and caches small images (favicons, thumbnails) decoded to a target pixel width.</summary>
public interface IImageLoader
{
    Task<Bitmap?> LoadAsync(string url, int decodeWidth);

    /// <summary>Drops the cached image for <paramref name="url"/> at every width, so the next load fetches it again.</summary>
    void Forget(string url);
}

public sealed class ImageCache(HttpClient http) : IImageLoader
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _cache = new();

    public Task<Bitmap?> LoadAsync(string url, int decodeWidth) =>
        _cache.GetOrAdd($"{decodeWidth}|{url}", _ => FetchAsync(url, decodeWidth));

    public void Forget(string url)
    {
        foreach (var key in _cache.Keys.Where(k => k.EndsWith("|" + url, StringComparison.Ordinal)))
            _cache.TryRemove(key, out _);
    }

    private async Task<Bitmap?> FetchAsync(string url, int decodeWidth)
    {
        try
        {
            var data = await http.GetByteArrayAsync(url).ConfigureAwait(false);
            using var stream = new MemoryStream(data);
            return Bitmap.DecodeToWidth(stream, decodeWidth, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }
}

public static class Favicons
{
    /// <summary>Favicon source for a feed: the site's host through a public favicon service.</summary>
    public static string? Url(Feed feed) =>
        feed.Host is { } host ? $"https://www.google.com/s2/favicons?sz=64&domain={Uri.EscapeDataString(host)}" : null;

    /// <summary>Pixel width favicons are decoded to: 19 pt at 2x.</summary>
    public const int DecodeWidth = 38;
}
