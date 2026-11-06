using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;

namespace RSSmote.App.Controls.Headless;

/// <summary>
/// Flips between the light and the dark slot (D33). The icon names where it goes: a moon while a light palette shows,
/// a sun while a dark one does. Both icons stay in the button and one is hidden, so it never changes size. The
/// <see cref="Button.CommandParameter"/> is whether a dark palette shows; the call site binds the command. Styled as a
/// <see cref="Button"/>, so it takes the classes of the bar it sits in.
/// </summary>
public sealed class ThemeToggle : Button
{
    private readonly Icon _moon = new();
    private readonly Icon _sun = new();

    public ThemeToggle()
    {
        Content = new Panel
        {
            Children =
            {
                _moon,
                _sun,
            },
        };
        ActualThemeVariantChanged += (_, _) => Update();
        Update();
    }

    protected override Type StyleKeyOverride => typeof(Button);

    /// <summary>True for a dark palette: Dark itself or one built on it, such as Fjord (D22).</summary>
    public static bool IsDark(ThemeVariant? variant)
    {
        for (var v = variant; v is not null; v = v.InheritVariant)
        {
            if (v == ThemeVariant.Dark)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _moon.Data = this.FindResource("Icon.Moon") as Geometry;
        _sun.Data = this.FindResource("Icon.Sun") as Geometry;
    }

    private void Update()
    {
        var dark = IsDark(ActualThemeVariant);
        _moon.IsVisible = !dark;
        _sun.IsVisible = dark;
        CommandParameter = dark;
        ToolTip.SetTip(this, dark ? "Light Theme" : "Dark Theme");
    }
}
