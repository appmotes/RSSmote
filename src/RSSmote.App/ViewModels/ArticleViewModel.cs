using System.Globalization;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RSSmote.Core.Models;
using RSSmote.Core.Rendering;

namespace RSSmote.App.ViewModels;

/// <summary>
/// The article column: the selected article, or the empty state with the stream's name and count. The rendered
/// <see cref="Document"/> is rebuilt only when another article is selected, so toggling read or starred does not
/// reload the page.
/// </summary>
public sealed partial class ArticleViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasArticle), nameof(IsUnread), nameof(IsStarred), nameof(Link), nameof(FeedTitle))]
    public partial Item? Item { get; set; }

    [ObservableProperty]
    public partial ArticleDocument? Document { get; private set; }

    /// <summary>The article's favicon in the strip's path; set with the selection.</summary>
    [ObservableProperty]
    public partial Bitmap? Favicon { get; set; }

    /// <summary>"3 of 9": where the article sits in the list.</summary>
    [ObservableProperty]
    public partial string PositionText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsFaviconGrayscale { get; set; } = true;

    [ObservableProperty]
    public partial string EmptyTitle { get; set; } = "No article selected";

    [ObservableProperty]
    public partial string EmptySubtitle { get; set; } = "";

    [ObservableProperty]
    public partial ArticleFont Font { get; set; }

    [ObservableProperty]
    public partial int FontSize { get; set; } = AppSettings.DefaultFontSize;

    public bool HasArticle => Item is not null;

    public string FeedTitle => Item?.FeedTitle ?? "";

    public bool IsUnread => Item?.Unread == true;

    public bool IsStarred => Item?.Starred == true;

    public Uri? Link => ArticleSanitizer.Resolve(Item?.Url, null);

    public void ApplySettings(AppSettings settings)
    {
        Font = settings.ArticleFont;
        FontSize = settings.ArticleFontSize;
    }

    partial void OnItemChanged(Item? oldValue, Item? newValue)
    {
        if (newValue is null)
            Document = null;
        else if (oldValue?.Id != newValue.Id || Document is null)
            Document = ArticleDocument.Create(newValue, DateText(newValue.Published));
    }

    public static string DateText(DateTimeOffset published)
    {
        var culture = CultureInfo.CurrentCulture;
        var local = published.ToLocalTime();
        var date = local.ToString(culture.DateTimeFormat.LongDatePattern, culture);
        var time = local.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
        return $"{date} · {time}";
    }
}
