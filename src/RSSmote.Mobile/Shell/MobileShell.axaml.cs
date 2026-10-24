using Avalonia.Controls;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
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
    private readonly Dictionary<Page, PanelViewModel> _panels = [];
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
        PageRemoved += (_, e) => ReleasePanel(e.Page);
        MatchBackground();
        // Back from the background the timer may not have run: refresh if the last pass is too old.
        if (Avalonia.Application.Current?.TryGetFeature(typeof(Avalonia.Controls.ApplicationLifetimes.IActivatableLifetime)) is Avalonia.Controls.ApplicationLifetimes.IActivatableLifetime lifetime)
            lifetime.Activated += (_, e) =>
            {
                if (e.Kind == Avalonia.Controls.ApplicationLifetimes.ActivationKind.Background)
                    shell.RefreshIfStale();
            };
    }

    public RootPage Root => (RootPage)Content!;

    public ShellViewModel? ViewModel => _shell;

    /// <summary>The shell a view inside it belongs to.</summary>
    public static MobileShell? Of(Avalonia.Visual visual) => visual.FindAncestorOfType<MobileShell>();

    /// <summary>
    /// Pushes a page for one of the panels the desktop shows as an overlay (A6): the panel closing (Save, Add Feed)
    /// pops its page, and a page that goes any other way disposes its panel.
    /// </summary>
    public Task PushPanelAsync(Page page, PanelViewModel panel)
    {
        page.DataContext = panel;
        _panels[page] = panel;
        panel.Closed += (_, _) => Close(page);
        return PushAsync(page);
    }

    /// <summary>Takes a page off the stack, whether it is on top or under another.</summary>
    public void Close(Page page)
    {
        if (!NavigationStack.Contains(page))
            return;
        if (ReferenceEquals(Top, page))
            _ = PopAsync();
        else
            RemovePage(page);
    }

    /// <summary>
    /// Asks in a bottom sheet (D29); <paramref name="confirmed"/> runs after the action, once the sheet is gone, since
    /// the stack ignores a pop while another transition runs (P24).
    /// </summary>
    public Task ConfirmAsync(ConfirmViewModel confirm, Action? confirmed = null)
    {
        var sheet = new ConfirmSheet { DataContext = confirm };
        var ran = false;
        confirm.Confirmed += (_, _) => ran = true;
        confirm.Closed += async (_, _) =>
        {
            if (ModalStack.Contains(sheet))
                await PopModalAsync();
            if (ran)
                confirmed?.Invoke();
        };
        return PushModalAsync(sheet);
    }

    /// <summary>Shows a sheet over the stack, as <see cref="ConfirmAsync"/> does; a tap on its scrim or back closes it.</summary>
    public Task ShowSheetAsync(ContentPage sheet) => PushModalAsync(sheet);

    public Task CloseSheetAsync() => ModalStack.Count > 0 ? PopModalAsync() : Task.CompletedTask;

    private void ReleasePanel(Page page)
    {
        if (_panels.Remove(page, out var panel))
            panel.Dispose();
        (page as IDisposable)?.Dispose();
    }

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
        if (ModalStack.Count > 0)
        {
            _ = PopModalAsync();
            e.Handled = true;
            return;
        }
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
        // A native web view takes the page's place when it attaches, partway through a slide, and stays there (P29):
        // an article shown in one appears in place.
        if (_shell is not null && row is not null && Top is ArticleListPage)
            await PushAsync(new ArticlePage(_shell), ArticleHost.UsesNative(ArticleHost.DefaultRenderer) ? null : PageTransition);
    }

    private void OnPopped(object? sender, NavigationEventArgs e)
    {
        ReleasePanel(e.Page);
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
