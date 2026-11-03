using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;
using RSSmote.App.Views.Desktop;
using RSSmote.App.Views.Phone.Shell;
using RSSmote.Core.Models;

namespace RSSmote.App.Views;

/// <summary>
/// The root on every head (A22): the desktop columns from <see cref="DesktopMinWidth"/> up, the phone stack below.
/// Both show the one <see cref="ShellViewModel"/> in its <see cref="Control.DataContext"/>, so a resize keeps what is
/// being read. The keyboard shortcuts (D15) belong to the desktop columns and are taken from the top level, which
/// receives the keys when nothing has focus.
/// </summary>
public sealed class MainView : UserControl
{
    /// <summary>The three columns' minimum widths with their splitters.</summary>
    public const double DesktopMinWidth = 900;

    private TopLevel? _topLevel;

    public MainView() => AddHandler(ArticleHost.ShortcutEvent, OnArticleShortcut);

    public SidebarLayout? Layout { get; private set; }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Layout = null;
        Choose(Bounds.Width);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Choose(e.NewSize.Width);
    }

    private void Choose(double width)
    {
        if (Shell is not { } shell || width <= 0)
            return;
        var layout = width >= DesktopMinWidth ? SidebarLayout.Desktop : SidebarLayout.Phone;
        if (layout == Layout)
            return;
        Layout = layout;
        Content = null;
        if (layout == SidebarLayout.Phone)
        {
            Content = new MobileShell(shell);
            return;
        }
        shell.UseLayout(SidebarLayout.Desktop);
        Content = new ShellView();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _topLevel = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled)
            return;
        var textBoxFocused = _topLevel?.FocusManager?.GetFocusedElement() is TextBox;
        e.Handled = HandleShortcut(e.Key, e.KeyModifiers, textBoxFocused);
    }

    /// <summary>Keys typed while the article's web view had focus; that view swallows them otherwise (L9).</summary>
    private void OnArticleShortcut(object? sender, ArticleShortcutEventArgs e) =>
        e.Handled = HandleShortcut(e.Key, e.KeyModifiers, textBoxFocused: false);

    /// <summary>Single-key shortcuts (D15). Ignored while a text box has focus (L3).</summary>
    private bool HandleShortcut(Key key, KeyModifiers modifiers, bool textBoxFocused)
    {
        if (Shell is not { } shell || Layout != SidebarLayout.Desktop)
            return false;
        var command = Chrome.CommandModifier;

        if (shell.SettingsMode is { } settings)
            return HandleSettingsShortcut(shell, settings, key, modifiers == command);

        if (modifiers == command)
        {
            return key switch
            {
                Key.D1 => Run(() => shell.ShowFilterCommand.Execute(StreamFilter.Starred)),
                Key.D2 => Run(() => shell.ShowFilterCommand.Execute(StreamFilter.Unread)),
                Key.D3 => Run(() => shell.ShowFilterCommand.Execute(StreamFilter.All)),
                Key.F => Run(shell.List.FocusSearch),
                Key.N => Run(() => shell.OpenSettingsCommand.Execute(SettingsSection.AddFeed)),
                Key.OemComma => Run(shell.OpenSettingsCommand.Execute),
                Key.OemPlus or Key.Add => Run(() => shell.ChangeFontSizeCommand.Execute(1)),
                Key.OemMinus or Key.Subtract => Run(() => shell.ChangeFontSizeCommand.Execute(-1)),
                Key.D0 => Run(() => shell.ChangeFontSizeCommand.Execute(0)),
                _ => false,
            };
        }

        if (textBoxFocused)
        {
            if (key != Key.Escape)
                return false;
            shell.List.EndSearch();
            return true;
        }
        if (modifiers is not (KeyModifiers.None or KeyModifiers.Shift))
            return false;

        return key switch
        {
            Key.J => Run(shell.NextArticleCommand.Execute),
            Key.K => Run(shell.PreviousArticleCommand.Execute),
            Key.Space => Run(() => _ = ScrollOrNextAsync(shell, up: modifiers == KeyModifiers.Shift)),
            Key.M => Run(shell.ToggleReadCommand.Execute),
            Key.S => Run(shell.ToggleStarredCommand.Execute),
            Key.B => Run(shell.OpenInBrowserCommand.Execute),
            Key.R => Run(shell.RefreshCommand.Execute),
            Key.A => Run(shell.MarkAllReadCommand.Execute),
            Key.N => Run(shell.NextFeedCommand.Execute),
            Key.P => Run(shell.PreviousFeedCommand.Execute),
            Key.OemPeriod or Key.Escape => Run(shell.GoBackCommand.Execute),
            _ => false,
        };
    }

    /// <summary>
    /// Settings own the keyboard (D30): Escape cancels a question or leaves Settings, ⌘/Ctrl+N moves to Add Feed and
    /// ⌘/Ctrl+, stays; every other key goes to the page.
    /// </summary>
    private static bool HandleSettingsShortcut(ShellViewModel shell, SettingsModeViewModel settings, Key key, bool command)
    {
        if (key == Key.Escape && !command)
        {
            if (settings.Confirm is { } confirm)
                confirm.CloseCommand.Execute(null);
            else
                shell.CloseSettings();
            return true;
        }
        if (!command)
            return false;
        return key switch
        {
            Key.N => Run(() => settings.Show(SettingsSection.AddFeed)),
            Key.OemComma => true,
            _ => false,
        };
    }

    private async Task ScrollOrNextAsync(ShellViewModel shell, bool up)
    {
        var article = this.GetVisualDescendants().OfType<ArticleView>().FirstOrDefault();
        if (article is not null && await article.ScrollPageAsync(up))
            return;
        if (!up)
            shell.NextArticleCommand.Execute(null);
    }

    private static bool Run(Action action)
    {
        action();
        return true;
    }

    private static bool Run(Action<object?> execute) => Run(() => execute(null));
}
