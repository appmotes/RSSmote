namespace RSSmote.App.Services;

/// <summary>Opens links in the user's browser (A4). Each head supplies its own.</summary>
public interface ILauncher
{
    void Open(Uri url, bool inBackground);
}
