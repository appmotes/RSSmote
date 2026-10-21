using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RSSmote.App.Controls.Headless;
using RSSmote.App.ViewModels;
using RSSmote.Desktop.Views;
using RSSmote.Core.Models;

namespace RSSmote.Desktop.Shell;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (Chrome.ExtendsIntoTitleBar)
        {
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = 52;
        }
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(ArticleHost.ShortcutEvent, OnArticleShortcut);
    }

    public MainWindow(ShellViewModel shell) : this()
    {
        DataContext = shell;
        var items = AllItems(NativeMenu.GetMenu(this)).ToList();
        Fill(items, "Appearance", shell.Appearances);
        Fill(items, "Light Theme", shell.LightThemes);
        Fill(items, "Dark Theme", shell.DarkThemes);
    }

    private static IEnumerable<NativeMenuItem> AllItems(NativeMenu? menu) =>
        menu?.Items.OfType<NativeMenuItem>().SelectMany(i => AllItems(i.Menu).Prepend(i)) ?? [];

    private static void Fill(List<NativeMenuItem> items, string header, IReadOnlyList<ThemeChoiceViewModel> choices)
    {
        if (items.FirstOrDefault(i => i.Header == header)?.Menu is { } menu)
            foreach (var item in ChoiceItems(choices))
                menu.Items.Add(item);
    }

    /// <summary>Radio items for the macOS menu bar; a native menu has no ItemsSource, so they follow the choices here.</summary>
    private static IEnumerable<NativeMenuItem> ChoiceItems(IReadOnlyList<ThemeChoiceViewModel> choices)
    {
        foreach (var choice in choices)
        {
            var item = new NativeMenuItem
            {
                Header = choice.Header, ToggleType = MenuItemToggleType.Radio, IsChecked = choice.IsChecked,
                CommandParameter = choice.Key, Command = choice.Command,
            };
            choice.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ThemeChoiceViewModel.IsChecked))
                    item.IsChecked = choice.IsChecked;
            };
            yield return item;
        }
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var textBoxFocused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;
        e.Handled = HandleShortcut(e.Key, e.KeyModifiers, textBoxFocused);
    }

    /// <summary>Keys typed while the article's web view had focus; that view swallows them otherwise (L9).</summary>
    private void OnArticleShortcut(object? sender, ArticleShortcutEventArgs e) =>
        e.Handled = HandleShortcut(e.Key, e.KeyModifiers, textBoxFocused: false);

    /// <summary>Single-key shortcuts (D15). Ignored while a text box has focus (L3).</summary>
    private bool HandleShortcut(Key key, KeyModifiers modifiers, bool textBoxFocused)
    {
        if (Shell is not { } shell)
            return false;
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;

        // An open panel owns the keyboard; Escape closes it.
        if (shell.Panel is { } panel)
        {
            if (key != Key.Escape)
                return false;
            panel.CloseCommand.Execute(null);
            return true;
        }

        if (modifiers == command)
        {
            return key switch
            {
                Key.D1 => Run(() => shell.ShowFilterCommand.Execute(StreamFilter.Starred)),
                Key.D2 => Run(() => shell.ShowFilterCommand.Execute(StreamFilter.Unread)),
                Key.D3 => Run(() => shell.ShowFilterCommand.Execute(StreamFilter.All)),
                Key.F => Run(shell.List.FocusSearch),
                Key.N => Run(shell.ShowAddFeedCommand.Execute),
                Key.OemPlus or Key.Add => Run(() => shell.ChangeFontSizeCommand.Execute(1)),
                Key.OemMinus or Key.Subtract => Run(() => shell.ChangeFontSizeCommand.Execute(-1)),
                Key.D0 => Run(() => shell.ChangeFontSizeCommand.Execute(0)),
                _ => false,
            };
        }
#if DEBUG
        if (key == Key.G && modifiers == (command | KeyModifiers.Shift))
            return Run(ShowGallery);
#endif

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

#if DEBUG
    private static void ShowGallery() =>
        new Window { Title = "Component Gallery", Width = 1100, Height = 900, Content = new RSSmote.App.Gallery.ComponentGallery() }.Show();
#endif
}
