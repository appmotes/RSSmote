using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using RSSmote.Mobile;

namespace RSSmote.Android;

/// <summary>Starts the shared <see cref="App.App"/> with the phone shell once per process (A15).</summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<App.App>
{
    protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CreateAppBuilder() =>
        AppBuilder.Configure(() => new App.App { Host = new MobileShellHost() }).UseAndroid();

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();
}
