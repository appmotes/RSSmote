using Avalonia;

namespace RSSmote.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure(() => new App.App { Host = new DesktopShellHost() })
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
