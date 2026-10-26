using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace RSSmote.App.Controls.Headless;

/// <summary>
/// Lays its content out at <see cref="FactorProperty"/> times its size: text, icons, spacing and images alike, so a row
/// grows with its pane's text size and still lines up (D31). The factor is inherited; a pane sets it once and wraps
/// each row in one of these.
/// </summary>
public sealed class Scaled : LayoutTransformControl
{
    public static readonly AttachedProperty<double> FactorProperty =
        AvaloniaProperty.RegisterAttached<Scaled, Control, double>("Factor", 1, inherits: true);

    public static double GetFactor(Control control) => control.GetValue(FactorProperty);

    public static void SetFactor(Control control, double value) => control.SetValue(FactorProperty, value);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FactorProperty)
        {
            var factor = GetValue(FactorProperty);
            LayoutTransform = factor == 1 ? null : new ScaleTransform(factor, factor);
        }
    }
}
