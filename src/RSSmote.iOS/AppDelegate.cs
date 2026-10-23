using Avalonia;
using Avalonia.iOS;
using Foundation;
using RSSmote.App.Controls.Headless;
using RSSmote.Mobile;

namespace RSSmote.iOS;

/// <summary>Starts the shared <see cref="App.App"/> with the phone shell (A15).</summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<App.App>
{
    protected override AppBuilder CreateAppBuilder()
    {
        // The native web view shows nothing inside the phone shell yet (P16, N16); the article uses the fallback.
        ArticleHost.DefaultRenderer = ArticleRenderer.Fallback;
        return AppBuilder.Configure(() => new App.App { Host = new MobileShellHost() }).UseiOS(this);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();
}
