using Avalonia;
using Avalonia.iOS;
using Foundation;
using RSSmote.Mobile;

namespace RSSmote.iOS;

/// <summary>Starts the shared <see cref="App.App"/> with the phone shell (A15).</summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App.App>
{
    protected override AppBuilder CreateAppBuilder() =>
        AppBuilder.Configure(() => new App.App { Host = new MobileShellHost() }).UseiOS(this);

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();
}
