using Android.App;
using Android.Runtime;
using Avalonia.Android;
using RSSmote.Mobile.Spike;

namespace RSSmote.Android;

/// <summary>Starts the platform spike once per process until the mobile shell exists (A15).</summary>
[Application]
public class AndroidApp : AvaloniaAndroidApplication<SpikeApp>
{
    protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }
}
