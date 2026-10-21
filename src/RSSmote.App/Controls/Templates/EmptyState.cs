using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace RSSmote.App.Controls.Templates;

/// <summary>
/// What an empty pane shows (D9). By default a soft box with a title, a subtitle and <see cref="Shortcuts"/> under
/// them, for the article card; with the style class <c>compact</c> an icon in a round tile above a title and subtitle,
/// for an empty list.
/// </summary>
public class EmptyState : TemplatedControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Subtitle));

    public static readonly StyledProperty<Geometry?> IllustrationProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(Illustration));

    public static readonly StyledProperty<object?> ShortcutsProperty =
        AvaloniaProperty.Register<EmptyState, object?>(nameof(Shortcuts));

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

    /// <summary>The compact variant's icon: an icon geometry from Assets/Icons.axaml.</summary>
    public Geometry? Illustration
    {
        get => GetValue(IllustrationProperty);
        set => SetValue(IllustrationProperty, value);
    }

    /// <summary>Shown under the subtitle in the box: the keyboard shortcuts worth knowing.</summary>
    public object? Shortcuts
    {
        get => GetValue(ShortcutsProperty);
        set => SetValue(ShortcutsProperty, value);
    }
}
