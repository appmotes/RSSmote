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

    /// <summary>The phone shell; <see cref="MainActivity"/> hands it the system's font size (D31).</summary>
    internal MobileShellHost Host { get; } = new();

    protected override AppBuilder CreateAppBuilder() =>
        AppBuilder.Configure(() => new App.App { Host = Host }).UseAndroid();

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder).WithInterFont();
}
