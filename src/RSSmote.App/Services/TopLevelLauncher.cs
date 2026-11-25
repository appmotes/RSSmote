using Avalonia.Controls;

namespace RSSmote.App.Services;

/// <summary>Opens links through the platform launcher of the app's top level (A4).</summary>
public sealed class TopLevelLauncher(Func<TopLevel?> topLevel) : ILauncher
{
    public void Open(Uri url)
    {
        if (url.Scheme is not ("http" or "https" or "mailto"))
        {
            return;
        }

        if (topLevel()?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(url);
        }
    }
}
