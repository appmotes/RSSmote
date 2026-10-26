using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Avalonia.Android;

namespace RSSmote.Android;

/// <summary>
/// The one activity. It handles a change of font size itself instead of restarting, and passes the size on: the pane
/// sizes apply on top of it (D31).
/// </summary>
[Activity(Label = "RSSmote", Theme = "@style/Theme.AppCompat.DayNight.NoActionBar", MainLauncher = true, Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.FontScale)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ShowFontScale(Resources?.Configuration);
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ShowFontScale(newConfig);
    }

    private void ShowFontScale(Configuration? configuration)
    {
        if (configuration is not null && Application is AndroidApp app)
            app.Host.SystemTextScale = configuration.FontScale;
    }
}
