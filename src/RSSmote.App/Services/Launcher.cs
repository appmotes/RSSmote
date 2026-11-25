namespace RSSmote.App.Services;

/// <summary>Opens links in a new browser tab (A4).</summary>
public interface ILauncher
{
    void Open(Uri url);
}
