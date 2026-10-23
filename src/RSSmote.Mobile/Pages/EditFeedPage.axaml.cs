using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Pages;

public partial class EditFeedPage : PhonePage
{
    public EditFeedPage() => InitializeComponent();

    /// <summary>Pushes the page for <paramref name="feed"/>; Save or removing the feed pops it.</summary>
    public static void Open(MobileShell shell, Feed feed, Bitmap? favicon, string backTitle)
    {
        if (shell.ViewModel?.CreateEditFeed(feed) is not { } edit)
            return;
        var page = new EditFeedPage { BackTitle = backTitle };
        page.FeedIcon.Source = favicon;
        _ = shell.PushPanelAsync(page, edit);
    }

    private EditFeedViewModel? Edit => DataContext as EditFeedViewModel;

    private MobileShell? Shell => MobileShell.Of(this);

    private void OnOpenSiteClick(object? sender, RoutedEventArgs e)
    {
        if (Edit is { } edit)
            Shell?.ViewModel?.OpenSiteCommand.Execute(edit.Feed);
    }

    private void OnReloadIconClick(object? sender, RoutedEventArgs e)
    {
        if (Edit is { } edit)
            Shell?.ViewModel?.ReloadIconCommand.Execute(edit.Feed);
    }

    /// <summary>Asks first; once the feed is gone, so is its page.</summary>
    private void OnRemoveClick(object? sender, RoutedEventArgs e)
    {
        if (Edit is { } edit && Shell is { ViewModel: { } vm } shell)
            _ = shell.ConfirmAsync(vm.ConfirmRemoveFeed(edit.Feed), () => shell.Close(this));
    }
}
