using Avalonia;

namespace RSSmote.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<RSSmote.App.App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
