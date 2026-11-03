using System.Runtime.InteropServices.JavaScript;
using RSSmote.Client;

namespace RSSmote.Browser;

/// <summary>The browser's local storage, guarded in interop.js: unavailable storage reads as empty and refuses writes.</summary>
internal sealed partial class BrowserStorage : IDeviceStorage
{
    public const string ModuleName = "interop";

    public string? Read(string key) => GetItem(key);

    public bool Write(string key, string value) => SetItem(key, value);

    /// <summary>The browser runs on macOS, iOS or iPadOS, whose keyboards command with ⌘.</summary>
    [JSImport("isApple", ModuleName)]
    public static partial bool IsApple();

    [JSImport("getItem", ModuleName)]
    private static partial string? GetItem(string key);

    [JSImport("setItem", ModuleName)]
    private static partial bool SetItem(string key, string value);
}
