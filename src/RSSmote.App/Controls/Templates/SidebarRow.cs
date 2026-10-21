using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media.Imaging;
using RSSmote.Core.Models;

namespace RSSmote.App.Controls.Templates;

public enum SidebarRowKind
{
    /// <summary>The filter-named stream at the top: "Unread", "Starred" or "All Items".</summary>
    Smart,
    /// <summary>A collapsible section header: "Folders", "Feeds".</summary>
    Section,
    Folder,
    Feed,
}

/// <summary>
/// One row of the sidebar (D9, D10): an icon for the smart row, a disclosure chevron for folders, a favicon for feeds,
/// the label and a count badge; sections carry their chevron on the right. Selection is drawn by the list item.
/// </summary>
[PseudoClasses(":smart", ":section", ":folder", ":feed", ":expanded", ":expandable", ":error", ":count", ":starred", ":all")]
public class SidebarRow : TemplatedControl
{
    public static readonly StyledProperty<SidebarRowKind> KindProperty =
        AvaloniaProperty.Register<SidebarRow, SidebarRowKind>(nameof(Kind), SidebarRowKind.Feed);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<SidebarRow, string?>(nameof(Title));

    public static readonly StyledProperty<int> CountProperty =
        AvaloniaProperty.Register<SidebarRow, int>(nameof(Count));

    public static readonly StyledProperty<Bitmap?> FaviconProperty =
        AvaloniaProperty.Register<SidebarRow, Bitmap?>(nameof(Favicon));

    public static readonly StyledProperty<bool> IsFaviconGrayscaleProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsFaviconGrayscale), true);

    /// <summary>Show the favicon in greyscale ("Grayscale Favicons").</summary>
    public bool IsFaviconGrayscale
    {
        get => GetValue(IsFaviconGrayscaleProperty);
        set => SetValue(IsFaviconGrayscaleProperty, value);
    }

    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsExpanded));

    public static readonly StyledProperty<bool> IsExpandableProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(IsExpandable), true);

    public static readonly StyledProperty<bool> HasErrorProperty =
        AvaloniaProperty.Register<SidebarRow, bool>(nameof(HasError));

    public static readonly StyledProperty<StreamFilter> FilterProperty =
        AvaloniaProperty.Register<SidebarRow, StreamFilter>(nameof(Filter), StreamFilter.Unread);

    public static readonly DirectProperty<SidebarRow, string> CountTextProperty =
        AvaloniaProperty.RegisterDirect<SidebarRow, string>(nameof(CountText), r => r.CountText);

    private string _countText = "";

    public SidebarRow() => UpdatePseudoClasses();

    public SidebarRowKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public int Count
    {
        get => GetValue(CountProperty);
        set => SetValue(CountProperty, value);
    }

    public Bitmap? Favicon
    {
        get => GetValue(FaviconProperty);
        set => SetValue(FaviconProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>False for a folder that is currently empty: no disclosure chevron.</summary>
    public bool IsExpandable
    {
        get => GetValue(IsExpandableProperty);
        set => SetValue(IsExpandableProperty, value);
    }

    /// <summary>The feed failed to refresh: the label is dimmed.</summary>
    public bool HasError
    {
        get => GetValue(HasErrorProperty);
        set => SetValue(HasErrorProperty, value);
    }

    /// <summary>The filter a smart row stands for: it picks the row's icon (circle-dot, star or lines).</summary>
    public StreamFilter Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    /// <summary>The count as shown: empty when zero.</summary>
    public string CountText
    {
        get => _countText;
        private set => SetAndRaise(CountTextProperty, ref _countText, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CountProperty)
            CountText = Count > 0 ? Count.ToString() : "";
        if (change.Property == KindProperty || change.Property == IsExpandedProperty || change.Property == CountProperty
            || change.Property == IsExpandableProperty || change.Property == HasErrorProperty || change.Property == FilterProperty)
            UpdatePseudoClasses();
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":smart", Kind == SidebarRowKind.Smart);
        PseudoClasses.Set(":section", Kind == SidebarRowKind.Section);
        PseudoClasses.Set(":folder", Kind == SidebarRowKind.Folder);
        PseudoClasses.Set(":feed", Kind == SidebarRowKind.Feed);
        PseudoClasses.Set(":expanded", IsExpanded);
        PseudoClasses.Set(":expandable", IsExpandable);
        PseudoClasses.Set(":error", HasError);
        PseudoClasses.Set(":count", Count > 0);
        PseudoClasses.Set(":starred", Filter == StreamFilter.Starred);
        PseudoClasses.Set(":all", Filter == StreamFilter.All);
    }
}
