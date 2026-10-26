using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile.Pages;

public partial class ArticlePage : ContentPage
{
    /// <summary>How far the reader scrolls in one direction before the bars step aside or come back.</summary>
    private const double Threshold = 24;

    /// <summary>Showing or hiding the bars resizes the article, which moves its offset near the end; not the reader's.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);

    private double _lastOffset;
    private double _travel;
    private DateTime _settledAt;

    public ArticlePage()
    {
        InitializeComponent();
        // The bar is not inside the page, so its button takes the page's data context from here (P21).
        TextButton.Bind(DataContextProperty, this.GetObservable(DataContextProperty));
        Host.Scrolled += OnScrolled;
        Host.PageTapped += (_, _) => ShowBars(true);
        Host.PropertyChanged += (_, e) =>
        {
            if (e.Property == ArticleHost.DocumentProperty)
            {
                _lastOffset = _travel = 0;
                ShowBars(true);
            }
        };
    }

    public ArticlePage(ShellViewModel shell) : this() => DataContext = shell;

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    public bool BarsShown => NavigationPage.GetHasNavigationBar(this);

    /// <summary>Down past the threshold hides the bars, up past it shows them; near the top they always show.</summary>
    private void OnScrolled(object? sender, ArticleScrollEventArgs e)
    {
        Progress.Value = e.Progress;
        var delta = e.Offset - _lastOffset;
        _lastOffset = e.Offset;
        if (DateTime.UtcNow < _settledAt)
        {
            _travel = 0;
            return;
        }
        _travel = Math.Sign(delta) == Math.Sign(_travel) ? _travel + delta : delta;
        if (e.Offset < Threshold)
            ShowBars(true);
        else if (_travel > Threshold)
            ShowBars(false);
        else if (_travel < -Threshold)
            ShowBars(true);
    }

    /// <summary>
    /// Without its top bar the page lies under the status bar and the shell still hands it no top inset (P28), so it
    /// takes the shell's; the progress line then sits right under the status bar.
    /// </summary>
    private void ShowBars(bool shown)
    {
        if (BarsShown == shown)
            return;
        _settledAt = DateTime.UtcNow + Settle;
        NavigationPage.SetHasNavigationBar(this, shown);
        BottomBar.IsVisible = shown;
        Progress.IsVisible = !shown;
        Padding = shown ? default : new Thickness(0, MobileShell.Of(this)?.SafeAreaPadding.Top ?? 0, 0, 0);
    }

    /// <summary>
    /// A native web view is drawn over everything (L9), so while the sheet is open the article ends where the sheet
    /// begins: the reader sees the size change, and nothing covers the sheet (D31).
    /// </summary>
    private void OnTextClick(object? sender, RoutedEventArgs e)
    {
        if (MobileShell.Of(this) is not { } shell || shell.ShowTextSheet(TextPane.Article) is not { } sheet)
            return;
        void Fit(object? s, SizeChangedEventArgs args) =>
            Host.Margin = new Thickness(0, 0, 0, Math.Max(0, args.NewSize.Height - BottomBar.Bounds.Height));
        void Closed(object? s, EventArgs args)
        {
            shell.ModalPopped -= Closed;
            sheet.Body.SizeChanged -= Fit;
            Host.Margin = default;
        }
        sheet.Body.SizeChanged += Fit;
        shell.ModalPopped += Closed;
    }

    /// <summary>Previous and next stay inside the list; at either end the button does nothing (D26).</summary>
    private void OnPreviousClick(object? sender, RoutedEventArgs e) => Shell?.List.SelectAdjacent(-1);

    private void OnNextClick(object? sender, RoutedEventArgs e) => Shell?.List.SelectAdjacent(1);

    private void OnLinkClicked(object? sender, ArticleLinkEventArgs e)
    {
        e.Handled = true;
        Shell?.OpenLinkCommand.Execute(e.Url);
    }
}
