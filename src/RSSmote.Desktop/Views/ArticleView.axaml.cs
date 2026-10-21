using Avalonia.Controls;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;

namespace RSSmote.Desktop.Views;

public partial class ArticleView : UserControl
{
    public ArticleView() => InitializeComponent();

    /// <summary>Space: scroll a page, or report false at the bottom so the caller moves to the next article.</summary>
    public Task<bool> ScrollPageAsync(bool up) => Host.IsVisible ? Host.ScrollPageAsync(up) : Task.FromResult(false);

    private void OnLinkClicked(object? sender, ArticleLinkEventArgs e)
    {
        e.Handled = true;
        (DataContext as ShellViewModel)?.OpenLinkCommand.Execute(e.Url);
    }
}
