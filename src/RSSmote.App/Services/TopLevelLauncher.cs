using Avalonia.Controls;

namespace RSSmote.App.Services;

/// <summary>Opens links through the platform launcher of the app's top level (A4).</summary>
public sealed class TopLevelLauncher(Func<TopLevel?> topLevel) : ILauncher
{
    /// <remarks>A browser opens a new tab where it chooses, so <paramref name="inBackground"/> is ignored.</remarks>
    public void Open(Uri url, bool inBackground)
    {
        if (url.Scheme is not ("http" or "https" or "mailto"))
            return;
        if (topLevel()?.Launcher is { } launcher)
            _ = launcher.LaunchUriAsync(url);
    }
}
