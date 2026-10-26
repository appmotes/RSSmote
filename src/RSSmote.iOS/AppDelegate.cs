using Avalonia;
using Avalonia.iOS;
using Foundation;
using RSSmote.Mobile;
using UIKit;

namespace RSSmote.iOS;

/// <summary>Starts the shared <see cref="App.App"/> with the phone shell (A15).</summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App.App>
{
    private readonly MobileShellHost _host = new();
    private NSObject? _sizeObserver;

    protected override AppBuilder CreateAppBuilder()
    {
        // The pane sizes apply on top of the reader's Larger Text setting, and follow it when it changes (D31).
        _host.SystemTextScale = SystemTextScale();
        _sizeObserver = UIApplication.Notifications.ObserveContentSizeCategoryChanged((_, _) => _host.SystemTextScale = SystemTextScale());
        return AppBuilder.Configure(() => new App.App { Host = _host }).UseiOS(this);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();

    /// <summary>How much larger body text is than at the default Larger Text setting, where it is 17 pt.</summary>
    private static double SystemTextScale() => (double)UIFontMetrics.DefaultMetrics.GetScaledValue(17) / 17;
}
