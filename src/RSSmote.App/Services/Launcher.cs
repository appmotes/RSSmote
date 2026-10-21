using System.Diagnostics;

namespace RSSmote.App.Services;

/// <summary>Opens links in the user's browser (A4).</summary>
public interface ILauncher
{
    void Open(Uri url, bool inBackground);
}

public sealed class ProcessLauncher : ILauncher
{
    public void Open(Uri url, bool inBackground)
    {
        if (url.Scheme is not ("http" or "https" or "mailto"))
            return;
        try
        {
            // macOS can keep this app in front; elsewhere the browser decides.
            var start = OperatingSystem.IsMacOS()
                ? new ProcessStartInfo("open", inBackground ? ["-g", url.AbsoluteUri] : [url.AbsoluteUri])
                : new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true };
            Process.Start(start)?.Dispose();
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Trace.WriteLine($"Could not open {url}: {e.Message}");
        }
    }
}
