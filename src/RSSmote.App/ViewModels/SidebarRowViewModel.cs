using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RSSmote.App.Controls.Templates;
using RSSmote.Core.Models;

namespace RSSmote.App.ViewModels;

/// <summary>One row of the flattened sidebar (D10).</summary>
public sealed partial class SidebarRowViewModel : ObservableObject
{
    public SidebarRowViewModel(SidebarRowKind kind, string title, ArticleStream? stream = null)
    {
        Kind = kind;
        Title = title;
        Stream = stream;
    }

    public SidebarRowKind Kind { get; }

    /// <summary>What selecting the row shows; null for section headers.</summary>
    public ArticleStream? Stream { get; }

    /// <summary>"Folders" or "Feeds" for section rows.</summary>
    public string? SectionKey { get; init; }

    public long? FolderId { get; init; }

    public Feed? Feed { get; init; }

    public bool IsSelectable => Stream is not null;

    public bool IsFolder => Kind == SidebarRowKind.Folder;

    public bool IsFeed => Kind == SidebarRowKind.Feed;

    public bool IsSmart => Kind == SidebarRowKind.Smart;

    /// <summary>A feed listed under its folder, indented one level.</summary>
    public bool IsInFolder => Feed?.FolderId is not null;

    public bool HasCount => Count > 0;

    /// <summary>The feed row's "Open example.com" menu item.</summary>
    public string OpenSiteTitle => Feed?.Host is { } host ? "Open " + host : "Open Website";

    public bool IsFaviconGrayscale { get; init; } = true;

    /// <summary>The filter a smart row stands for; it picks the row's icon.</summary>
    public StreamFilter Filter { get; init; }

    [ObservableProperty]
    public partial string Title { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCount))]
    public partial int Count { get; set; }

    [ObservableProperty]
    public partial Bitmap? Favicon { get; set; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisclosure))]
    public partial bool IsExpandable { get; set; } = true;

    /// <summary>A folder with feeds to show carries a chevron that opens it in place.</summary>
    public bool HasDisclosure => IsFolder && IsExpandable;

    [ObservableProperty]
    public partial bool HasError { get; set; }
}
