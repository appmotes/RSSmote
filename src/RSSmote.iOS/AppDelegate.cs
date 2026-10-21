using Avalonia;
using Avalonia.iOS;
using Foundation;
using RSSmote.Mobile.Spike;

namespace RSSmote.iOS;

/// <summary>Starts the platform spike until the mobile shell exists (A15).</summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<SpikeApp>
{
}
