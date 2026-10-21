using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;

namespace RSSmote.App.Controls.Templates;

public enum PanelRowAccessory
{
    None,
    Chevron,
    /// <summary>A round check on the right: filled with the accent when the row is on.</summary>
    Check,
}

/// <summary>
/// A clickable row in a <see cref="PanelGroup"/> (D9): title, optional subtitle, and a chevron or a round check.
/// <see cref="IsOn"/> fills the check with the accent and tints the row.
/// </summary>
[PseudoClasses(":centered", ":subtitle", ":chevron", ":check", ":on")]
public class PanelRow : Button
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PanelRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<PanelRow, string?>(nameof(Subtitle));

    public static readonly StyledProperty<PanelRowAccessory> AccessoryProperty =
        AvaloniaProperty.Register<PanelRow, PanelRowAccessory>(nameof(Accessory));

    public static readonly StyledProperty<bool> IsOnProperty =
        AvaloniaProperty.Register<PanelRow, bool>(nameof(IsOn));

    public static readonly StyledProperty<bool> IsCenteredProperty =
        AvaloniaProperty.Register<PanelRow, bool>(nameof(IsCentered));

    protected override Type StyleKeyOverride => typeof(PanelRow);

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public PanelRowAccessory Accessory
    {
        get => GetValue(AccessoryProperty);
        set => SetValue(AccessoryProperty, value);
    }

    public bool IsOn
    {
        get => GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public bool IsCentered
    {
        get => GetValue(IsCenteredProperty);
        set => SetValue(IsCenteredProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SubtitleProperty)
            PseudoClasses.Set(":subtitle", !string.IsNullOrEmpty(Subtitle));
        else if (change.Property == AccessoryProperty)
        {
            PseudoClasses.Set(":chevron", Accessory == PanelRowAccessory.Chevron);
            PseudoClasses.Set(":check", Accessory == PanelRowAccessory.Check);
        }
        else if (change.Property == IsOnProperty)
            PseudoClasses.Set(":on", IsOn);
        else if (change.Property == IsCenteredProperty)
            PseudoClasses.Set(":centered", IsCentered);
    }
}
