using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace RSSmote.Android;

[Activity(Label = "RSSmote", Theme = "@style/Theme.AppCompat.DayNight.NoActionBar", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
