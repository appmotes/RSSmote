using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(RSSmote.App.Tests.TestAppBuilder))]

namespace RSSmote.App.Tests;

public static class TestAppBuilder
{
    // Real Skia drawing, so frames can be captured; the article uses the renderer Avalonia draws itself.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<RSSmote.App.App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false,
            });
}
