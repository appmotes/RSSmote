using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace RSSmote.App.Controls.Headless;

/// <summary>
/// Gives a scroll viewer the <see cref="Class"/> class while its content moves and for <see cref="Linger"/> after,
/// so its scroll bar shows only then (D32). Covers every scroll viewer in the app once <see cref="Register"/> has run.
/// </summary>
public static class ScrollActivity
{
    public const string Class = "scrolling";
    public static readonly TimeSpan Linger = TimeSpan.FromSeconds(1);

    private static readonly ConditionalWeakTable<ScrollViewer, DispatcherTimer> Timers = [];
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        ScrollViewer.ScrollChangedEvent.AddClassHandler<ScrollViewer>(OnScrollChanged, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static void OnScrollChanged(ScrollViewer viewer, ScrollChangedEventArgs e)
    {
        // A nested viewer's event bubbles through this one; only its own offset counts.
        if (!ReferenceEquals(e.Source, viewer) || e.OffsetDelta == default)
        {
            return;
        }

        viewer.Classes.Set(Class, true);
        var timer = Timers.GetValue(viewer, Hide);
        timer.Stop();
        timer.Start();
    }

    private static DispatcherTimer Hide(ScrollViewer viewer)
    {
        var timer = new DispatcherTimer
        {
            Interval = Linger,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            viewer.Classes.Set(Class, false);
        };
        return timer;
    }
}
