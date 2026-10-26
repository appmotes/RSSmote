using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using RSSmote.Core.Models;
using RSSmote.Core.Rendering;
using TheArtOfDev.HtmlRenderer.Avalonia;

namespace RSSmote.App.Controls.Headless;

public enum ArticleRenderer
{
    /// <summary>The native web view when the OS has one, otherwise the fallback.</summary>
    Auto,
    Native,
    /// <summary>Avalonia.HtmlRenderer: no scripts, no iframes, drawn by Avalonia itself.</summary>
    Fallback,
}

/// <summary>A shortcut pressed while the web view had keyboard focus (D15).</summary>
public sealed class ArticleShortcutEventArgs(Key key, KeyModifiers modifiers) : RoutedEventArgs(ArticleHost.ShortcutEvent)
{
    public Key Key { get; } = key;
    public KeyModifiers KeyModifiers { get; } = modifiers;
}

/// <summary>How far down the article the reader is: <see cref="Offset"/> in pixels, <see cref="Progress"/> from 0 to 1.</summary>
public sealed class ArticleScrollEventArgs(double offset, double progress) : EventArgs
{
    public double Offset { get; } = offset;
    public double Progress { get; } = progress;
}

public sealed class ArticleLinkEventArgs(Uri url) : RoutedEventArgs(ArticleHost.LinkClickedEvent)
{
    public Uri Url { get; } = url;
}

/// <summary>
/// Shows an <see cref="ArticleDocument"/> through the article template (A9): in <see cref="NativeWebView"/> where
/// the OS provides one, otherwise in an <see cref="HtmlLabel"/> inside a scroll viewer. The page never navigates;
/// clicked links are raised as <see cref="LinkClickedEvent"/>, and shortcuts typed into the web view as
/// <see cref="ShortcutEvent"/>. Colours come from the theme palette and follow theme changes.
/// </summary>
public sealed class ArticleHost : Decorator
{
    /// <summary>Process-wide choice; tests and the RSSMOTE_ARTICLE_RENDERER variable set it before the first host.</summary>
    public static ArticleRenderer DefaultRenderer { get; set; } = ArticleRenderer.Auto;

    private static readonly WebViewAdapterType[] NativeAdapters =
        [WebViewAdapterType.WkWebView, WebViewAdapterType.WebView2, WebViewAdapterType.WpeWebKit, WebViewAdapterType.WebKitGtk];

    public static readonly StyledProperty<ArticleDocument?> DocumentProperty =
        AvaloniaProperty.Register<ArticleHost, ArticleDocument?>(nameof(Document));

    /// <summary>The body's size in pixels (D31).</summary>
    public static readonly StyledProperty<double> ArticleFontSizeProperty =
        AvaloniaProperty.Register<ArticleHost, double>(nameof(ArticleFontSize), ArticleTemplate.StandardFontSize);

    /// <summary>Something is drawn over the article (an overlay panel); the native view hides, as it would cover it (L9).</summary>
    public static readonly StyledProperty<bool> IsObscuredProperty =
        AvaloniaProperty.Register<ArticleHost, bool>(nameof(IsObscured));

    public static readonly RoutedEvent<ArticleLinkEventArgs> LinkClickedEvent =
        RoutedEvent.Register<ArticleHost, ArticleLinkEventArgs>(nameof(LinkClicked), RoutingStrategies.Bubble);

    public static readonly RoutedEvent<ArticleShortcutEventArgs> ShortcutEvent =
        RoutedEvent.Register<ArticleHost, ArticleShortcutEventArgs>(nameof(Shortcut), RoutingStrategies.Bubble);

    private readonly NativeWebView? _web;
    private readonly ScrollViewer? _scroller;
    private readonly HtmlLabel? _label;
    private bool _adapterReady;
    private bool _loading;
    private string? _shown;
    private string? _pending;

    public ArticleHost()
    {
        ActualThemeVariantChanged += (_, _) => Render();
        if (UsesNative(DefaultRenderer))
        {
            _web = new NativeWebView();
            _web.AdapterCreated += (_, _) =>
            {
                _adapterReady = true;
                if (_pending is { } html)
                    Show(html);
            };
            _web.AdapterDestroyed += (_, _) =>
            {
                _adapterReady = false;
                _pending = _shown;
                _shown = null;
            };
            _web.NavigationStarted += OnNavigationStarted;
            _web.NavigationCompleted += (_, _) => _loading = false;
            _web.NewWindowRequested += OnNewWindowRequested;
            _web.WebMessageReceived += OnWebMessage;
            Child = _web;
        }
        else
        {
            _label = new HtmlLabel { AutoSize = false, AutoSizeHeightOnly = true, IsContextMenuEnabled = false };
            _label.LinkClicked += OnFallbackLinkClicked;
            _scroller = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = _label,
            };
            _scroller.ScrollChanged += (_, _) =>
            {
                var max = _scroller.Extent.Height - _scroller.Viewport.Height;
                Scrolled?.Invoke(this, new ArticleScrollEventArgs(_scroller.Offset.Y, max > 0 ? _scroller.Offset.Y / max : 1));
            };
            _scroller.Tapped += (_, _) => PageTapped?.Invoke(this, EventArgs.Empty);
            Child = _scroller;
        }
    }

    public ArticleDocument? Document
    {
        get => GetValue(DocumentProperty);
        set => SetValue(DocumentProperty, value);
    }

    public double ArticleFontSize
    {
        get => GetValue(ArticleFontSizeProperty);
        set => SetValue(ArticleFontSizeProperty, value);
    }

    public bool IsObscured
    {
        get => GetValue(IsObscuredProperty);
        set => SetValue(IsObscuredProperty, value);
    }

    public event EventHandler<ArticleLinkEventArgs>? LinkClicked
    {
        add => AddHandler(LinkClickedEvent, value);
        remove => RemoveHandler(LinkClickedEvent, value);
    }

    public event EventHandler<ArticleShortcutEventArgs>? Shortcut
    {
        add => AddHandler(ShortcutEvent, value);
        remove => RemoveHandler(ShortcutEvent, value);
    }

    /// <summary>The reader scrolled the article, in either renderer.</summary>
    public event EventHandler<ArticleScrollEventArgs>? Scrolled;

    /// <summary>The reader tapped the article anywhere but on a link.</summary>
    public event EventHandler? PageTapped;

    public bool IsNative => _web is not null;

    public static bool UsesNative(ArticleRenderer renderer) => renderer switch
    {
        ArticleRenderer.Native => true,
        ArticleRenderer.Fallback => false,
        // Android ships its system web view, which the package drives without an adapter query.
        _ => OperatingSystem.IsAndroid() || NativeAdapters.Any(IsAvailable),
    };

    /// <summary>An adapter built for another OS throws from its type initializer when asked (P17).</summary>
    private static bool IsAvailable(WebViewAdapterType type)
    {
        try
        {
            return WebViewAdapterInfo.GetAdapterInfo(type) is { IsSupported: true, IsInstalled: true };
        }
        catch (TypeInitializationException)
        {
            return false;
        }
    }

    /// <summary>Space: scrolls a page; false at the end of the article so the caller can move on.</summary>
    public async Task<bool> ScrollPageAsync(bool up)
    {
        if (Document is null)
            return false;
        if (_web is not null)
        {
            if (!_adapterReady)
                return false;
            var result = await _web.InvokeScript(ArticleTemplate.ScrollPageScript(up));
            return result?.Trim('"') == "true";
        }
        var scroller = _scroller!;
        var max = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        var y = scroller.Offset.Y;
        if (!up && y >= max - 2)
            return false;
        var step = scroller.Viewport.Height * 0.85;
        scroller.Offset = new Vector(scroller.Offset.X, Math.Clamp(up ? y - step : y + step, 0, max));
        return true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsObscuredProperty && _web is not null)
            _web.IsVisible = !IsObscured;
        if (change.Property == DocumentProperty || change.Property == ArticleFontSizeProperty)
            Render();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Render();
    }

    private void Render()
    {
        if (VisualRoot is null)
            return;
        if (Document is not { } document)
        {
            Show("");
            return;
        }
        var style = new ArticleStyle(Palette(), ArticleFontSize, Scripted: _web is not null);
        Show(ArticleTemplate.Render(document, style));
    }

    private void Show(string html)
    {
        if (html == _shown)
            return;
        if (_web is not null)
        {
            if (!_adapterReady)
            {
                _pending = html;
                return;
            }
            _pending = null;
            _shown = html;
            _web.Background = Brush("Color.Bg.Article");
            _loading = true;
            _web.NavigateToString(html, null!);
            return;
        }
        _shown = html;
        _label!.Text = html;
        _scroller!.Offset = default;
    }

    private ArticlePalette Palette() => new(
        Css("Color.Bg.Article"),
        Css("Color.Ink.Primary"),
        Css("Color.Ink.Secondary"),
        Css("Color.Accent"),
        Css("Color.Line.Separator"),
        Css("Color.Bg.Hover"),
        Css("Color.Selection.List"));

    private Color PaletteColor(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is Color color ? color : Colors.Transparent;

    private SolidColorBrush Brush(string key) => new(PaletteColor(key));

    /// <summary>
    /// Opaque hex, translucent tokens composited over the article background: HtmlRenderer reads the alpha of
    /// <c>rgba()</c> as 0–255 where browsers read 0–1 (L10).
    /// </summary>
    private string Css(string key)
    {
        var c = PaletteColor(key);
        var bg = PaletteColor("Color.Bg.Article");
        var a = c.A / 255.0;
        byte Mix(byte fg, byte back) => (byte)Math.Round(fg * a + back * (1 - a));
        return string.Create(CultureInfo.InvariantCulture, $"#{Mix(c.R, bg.R):x2}{Mix(c.G, bg.G):x2}{Mix(c.B, bg.B):x2}");
    }

    // MARK: Native

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        // Only the page set by NavigateToString may load; anything else goes to the browser. Engines differ in
        // the address they report for that page, so the load in progress is allowed whatever it says.
        if (_loading || e.Request is not { } url || url.Scheme is "about" or "data")
            return;
        e.Cancel = true;
        RaiseLink(url);
    }

    private void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Request is { } url)
            RaiseLink(url);
    }

    private void OnWebMessage(object? sender, WebMessageReceivedEventArgs e)
    {
        try
        {
            using var json = JsonDocument.Parse(e.Body ?? "");
            var root = json.RootElement;
            switch (root.GetProperty("type").GetString())
            {
                case "link" when Uri.TryCreate(root.GetProperty("href").GetString(), UriKind.Absolute, out var url):
                    RaiseLink(url);
                    break;
                case "scroll":
                    var y = root.GetProperty("y").GetDouble();
                    var max = root.GetProperty("max").GetDouble();
                    Scrolled?.Invoke(this, new ArticleScrollEventArgs(y, max > 0 ? Math.Clamp(y / max, 0, 1) : 1));
                    break;
                case "tap":
                    PageTapped?.Invoke(this, EventArgs.Empty);
                    break;
                case "key" when ToKey(root.GetProperty("key").GetString()) is { } key:
                    var modifiers = KeyModifiers.None;
                    if (root.GetProperty("shift").GetBoolean())
                        modifiers |= KeyModifiers.Shift;
                    if (root.GetProperty("command").GetBoolean())
                        modifiers |= OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
                    RaiseEvent(new ArticleShortcutEventArgs(key, modifiers));
                    break;
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            // Not one of the page's own messages.
        }
    }

    /// <summary>Maps a DOM <c>KeyboardEvent.key</c> from the page's shortcut list to an Avalonia key.</summary>
    public static Key? ToKey(string? key) => key switch
    {
        null or "" => null,
        " " => Key.Space,
        "." => Key.OemPeriod,
        "," => Key.OemComma,
        "+" or "=" => Key.OemPlus,
        "-" => Key.OemMinus,
        "Escape" => Key.Escape,
        [var c and >= 'a' and <= 'z'] => Key.A + (c - 'a'),
        [var c and >= '0' and <= '9'] => Key.D0 + (c - '0'),
        _ => null,
    };

    // MARK: Fallback

    private void OnFallbackLinkClicked(object? sender, HtmlRendererRoutedEventArgs<TheArtOfDev.HtmlRenderer.Core.Entities.HtmlLinkClickedEventArgs> e)
    {
        e.Event.Handled = true;
        if (Uri.TryCreate(e.Event.Link, UriKind.Absolute, out var url))
            RaiseLink(url);
    }

    private void RaiseLink(Uri url)
    {
        if (url.Scheme is "http" or "https" or "mailto")
            RaiseEvent(new ArticleLinkEventArgs(url));
    }
}
