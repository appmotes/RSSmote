using Avalonia;
using Avalonia.Controls.Primitives;

namespace RSSmote.App.Controls.Templates;

/// <summary>
/// The thin bar at the foot of a column (D9): status text on the left, a few controls on the right. Under the list and
/// the article it draws a hairline above itself; in the sidebar it sits on the canvas without one.
/// </summary>
public class StatusBar : TemplatedControl
{
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<StatusBar, object?>(nameof(Leading));

    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<StatusBar, object?>(nameof(Trailing));

    public object? Leading
    {
        get => GetValue(LeadingProperty);
        set => SetValue(LeadingProperty, value);
    }

    public object? Trailing
    {
        get => GetValue(TrailingProperty);
        set => SetValue(TrailingProperty, value);
    }
}
