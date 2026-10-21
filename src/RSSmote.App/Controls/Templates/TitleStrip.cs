using Avalonia;
using Avalonia.Controls.Primitives;

namespace RSSmote.App.Controls.Templates;

/// <summary>
/// The chrome strip on top of a column (A5, D9): draggable like a title bar, with a leading and a trailing slot.
/// Use <c>Padding</c> to keep clear of the window's own caption buttons.
/// </summary>
public class TitleStrip : TemplatedControl
{
    public static readonly StyledProperty<object?> LeadingProperty =
        AvaloniaProperty.Register<TitleStrip, object?>(nameof(Leading));

    public static readonly StyledProperty<object?> TrailingProperty =
        AvaloniaProperty.Register<TitleStrip, object?>(nameof(Trailing));

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
