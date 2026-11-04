using System.Runtime.InteropServices.JavaScript;
using Avalonia;
using Avalonia.Browser;

namespace RSSmote.Browser;

internal static class Program
{
    /// <param name="args">The page's address, from main.js; the server is the folder it was loaded from (A23).</param>
    private static async Task Main(string[] args)
    {
        // Imported before anything calls into it.
        await JSHost.ImportAsync(BrowserStorage.ModuleName, "../interop.js");
        var server = new Uri(new Uri(args[0]), "./");
        await BuildAvaloniaApp(new HttpClient
        {
            BaseAddress = server,
        }).StartBrowserAppAsync("out");
    }

    private static AppBuilder BuildAvaloniaApp(HttpClient http) =>
        AppBuilder.Configure(() => new App.App
        {
            Http = http,
            Storage = new BrowserStorage(),
            AppleKeyboard = BrowserStorage.IsApple(),
        })
            .WithInterFont();
}
