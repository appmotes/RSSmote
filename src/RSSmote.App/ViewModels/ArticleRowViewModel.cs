using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RSSmote.App.Formatting;
using RSSmote.Core.Models;

namespace RSSmote.App.ViewModels;

/// <summary>A day heading in the article list ("TODAY"); never selectable.</summary>
/// <summary>A day heading in the list: "TODAY" and how many articles that day shows.</summary>
public sealed record DateHeaderViewModel(string Title)
{
    public int Count { get; set; }
}

/// <summary>One article row (D11). Flags update in place when the store reports a change.</summary>
public sealed partial class ArticleRowViewModel : ObservableObject
{
    private bool _imagesRequested;

    public ArticleRowViewModel(Item item, DateTimeOffset now, bool grayscaleFavicon = true)
    {
        IsFaviconGrayscale = grayscaleFavicon;
        Item = item;
        TimeText = ItemFormatting.TimeText(item.Published, now);
        IsUnread = item.Unread;
        IsStarred = item.Starred;
    }

    public Item Item { get; private set; }

    public long Id => Item.Id;
    public string FeedTitle => Item.FeedTitle;
    public string Title => Item.Title;
    public string Summary => Item.Summary;
    public bool HasThumbnail => Item.ImageUrl is not null;
    public string TimeText { get; }
    public bool IsFaviconGrayscale { get; }

    [ObservableProperty]
    public partial bool IsUnread { get; set; }

    [ObservableProperty]
    public partial bool IsStarred { get; set; }

    [ObservableProperty]
    public partial Bitmap? Favicon { get; set; }

    [ObservableProperty]
    public partial Bitmap? Thumbnail { get; set; }

    public void Update(Item item)
    {
        Item = item;
        IsUnread = item.Unread;
        IsStarred = item.Starred;
    }

    /// <summary>True the first time it is asked: the view loads images only for rows it realises.</summary>
    public bool ClaimImageRequest()
    {
        if (_imagesRequested)
            return false;
        _imagesRequested = true;
        return true;
    }
}
