using Avalonia.Controls;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;
using RSSmote.Mobile.Pages;

namespace RSSmote.Mobile.Shell;

/// <summary>
/// The one navigation stack of the phone shell (D23); its root page carries the tab bar. Picking a stream on the
/// Feeds root pushes the article list, picking an article pushes the article, and going back clears the pick so
/// the same row opens again.
/// </summary>
public partial class MobileShell : NavigationPage
{
    private readonly ShellViewModel? _shell;
    private TopLevel? _topLevel;

    public MobileShell() => InitializeComponent();

    public MobileShell(ShellViewModel shell) : this()
    {
        _shell = shell;
        shell.Sidebar.Layout = SidebarLayout.Phone;
        shell.List.Hide();
        Content = new RootPage(shell);
        shell.Sidebar.StreamSelected += OnStreamSelected;
        shell.List.ArticleSelected += OnArticleSelected;
        Pushed += (_, _) => MatchBackground();
        Popped += OnPopped;
        MatchBackground();
    }

    public RootPage Root => (RootPage)Content!;

    /// <summary>
    /// As the view's root nobody hands this page the safe-area insets or the system back, so it takes both from its
    /// top level: without the insets the top bar sits under the status bar (P15), and without the back Android's
    /// back closes the app from any page (P18).
    /// </summary>
    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
            _topLevel.BackRequested += OnBackRequested;
        if (_topLevel?.InsetsManager is { } insets)
        {
            SafeAreaPadding = insets.SafeAreaPadding;
            // A local zero, so no inset arrives as padding as well and lands on the pages twice.
            Padding = default;
            // On Android the value read during the event is still in device pixels; it settles afterwards (P15).
            insets.SafeAreaChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(
                () => SafeAreaPadding = insets.SafeAreaPadding, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PageTransitionProperty && change.NewValue is Avalonia.Animation.PageSlide slide
            and not LaidOutPageSlide)
            PageTransition = new LaidOutPageSlide(slide);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
            _topLevel.BackRequested -= OnBackRequested;
        _topLevel = null;
    }

    /// <summary>Pops the top page; on the root the back stays unhandled and the system closes the app.</summary>
    private void OnBackRequested(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Handled)
            return;
        var back = new Avalonia.Interactivity.RoutedEventArgs(PageNavigationSystemBackButtonPressedEvent);
        RaiseEvent(back);
        e.Handled = back.Handled;
    }

    private Page? Top => NavigationStack.LastOrDefault();

    private async void OnStreamSelected(object? sender, ArticleStream stream)
    {
        if (_shell is not null && Top is RootPage)
            await PushAsync(new ArticleListPage(_shell));
    }

    private async void OnArticleSelected(object? sender, ArticleRowViewModel? row)
    {
        if (_shell is not null && row is not null && Top is ArticleListPage)
            await PushAsync(new ArticlePage(_shell));
    }

    private void OnPopped(object? sender, NavigationEventArgs e)
    {
        if (e.Page is ArticlePage && _shell is not null)
            _shell.List.SelectedRow = null;
        if (Top is RootPage)
            _shell?.List.Hide();
        MatchBackground();
    }

    /// <summary>The strips under the safe-area insets show this page's background, so it follows the top page (P14).</summary>
    private void MatchBackground()
    {
        if (Top is { } page)
            this[!BackgroundProperty] = page[!BackgroundProperty];
    }
}
