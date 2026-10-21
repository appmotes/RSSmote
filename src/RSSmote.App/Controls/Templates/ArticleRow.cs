using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace RSSmote.App.Controls.Templates;

/// <summary>
/// One article in the list (D9, D11): favicon · title · time on one line, then "Feed — summary" on at most two, and
/// an optional square thumbnail. Read rows drop the title to <c>Ink.Read</c> Regular.
/// </summary>
[TemplatePart("PART_Summary", typeof(TextBlock))]
[PseudoClasses(":unread", ":starred", ":thumbnail")]
public class ArticleRow : TemplatedControl
{
    public static readonly StyledProperty<string?> FeedTitleProperty =
        AvaloniaProperty.Register<ArticleRow, string?>(nameof(FeedTitle));

    public static readonly StyledProperty<string?> TimeTextProperty =
        AvaloniaProperty.Register<ArticleRow, string?>(nameof(TimeText));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<ArticleRow, string?>(nameof(Title));

    public static readonly StyledProperty<string?> SummaryProperty =
        AvaloniaProperty.Register<ArticleRow, string?>(nameof(Summary));

    public static readonly StyledProperty<bool> IsUnreadProperty =
        AvaloniaProperty.Register<ArticleRow, bool>(nameof(IsUnread));

    public static readonly StyledProperty<bool> IsStarredProperty =
        AvaloniaProperty.Register<ArticleRow, bool>(nameof(IsStarred));

    public static readonly StyledProperty<Bitmap?> FaviconProperty =
        AvaloniaProperty.Register<ArticleRow, Bitmap?>(nameof(Favicon));

    public static readonly StyledProperty<bool> IsFaviconGrayscaleProperty =
        AvaloniaProperty.Register<ArticleRow, bool>(nameof(IsFaviconGrayscale), true);

    public static readonly StyledProperty<Bitmap?> ThumbnailProperty =
        AvaloniaProperty.Register<ArticleRow, Bitmap?>(nameof(Thumbnail));

    public static readonly StyledProperty<bool> HasThumbnailProperty =
        AvaloniaProperty.Register<ArticleRow, bool>(nameof(HasThumbnail));

    public static readonly StyledProperty<IBrush?> SummaryForegroundProperty =
        AvaloniaProperty.Register<ArticleRow, IBrush?>(nameof(SummaryForeground));

    private TextBlock? _summary;

    public ArticleRow() => UpdatePseudoClasses();

    /// <summary>Starts the summary line, in Medium.</summary>
    public string? FeedTitle
    {
        get => GetValue(FeedTitleProperty);
        set => SetValue(FeedTitleProperty, value);
    }

    /// <summary>Already formatted by the view-model: "13.14" today, "13 Oct" otherwise.</summary>
    public string? TimeText
    {
        get => GetValue(TimeTextProperty);
        set => SetValue(TimeTextProperty, value);
    }

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public bool IsUnread
    {
        get => GetValue(IsUnreadProperty);
        set => SetValue(IsUnreadProperty, value);
    }

    public bool IsStarred
    {
        get => GetValue(IsStarredProperty);
        set => SetValue(IsStarredProperty, value);
    }

    public Bitmap? Favicon
    {
        get => GetValue(FaviconProperty);
        set => SetValue(FaviconProperty, value);
    }

    /// <summary>Show the favicon in greyscale ("Grayscale Favicons").</summary>
    public bool IsFaviconGrayscale
    {
        get => GetValue(IsFaviconGrayscaleProperty);
        set => SetValue(IsFaviconGrayscaleProperty, value);
    }

    public Bitmap? Thumbnail
    {
        get => GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    /// <summary>The article has an image: reserve the thumbnail square, even before the bitmap arrives.</summary>
    public bool HasThumbnail
    {
        get => GetValue(HasThumbnailProperty);
        set => SetValue(HasThumbnailProperty, value);
    }

    /// <summary>Colour of the summary line; set by the theme.</summary>
    public IBrush? SummaryForeground
    {
        get => GetValue(SummaryForegroundProperty);
        set => SetValue(SummaryForegroundProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _summary = e.NameScope.Find<TextBlock>("PART_Summary");
        UpdateSummary();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FeedTitleProperty || change.Property == SummaryProperty)
            UpdateSummary();
        if (change.Property == IsUnreadProperty || change.Property == IsStarredProperty
            || change.Property == HasThumbnailProperty)
            UpdatePseudoClasses();
    }

    private void UpdateSummary()
    {
        if (_summary?.Inlines is not { } inlines)
            return;
        inlines.Clear();
        inlines.Add(new Run(FeedTitle ?? "") { FontWeight = FontWeight.Medium });
        if (!string.IsNullOrWhiteSpace(Summary))
            inlines.Add(new Run(" — " + Summary));
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":unread", IsUnread);
        PseudoClasses.Set(":starred", IsStarred);
        PseudoClasses.Set(":thumbnail", HasThumbnail);
    }
}
