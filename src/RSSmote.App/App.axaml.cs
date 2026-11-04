using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using RSSmote.App.Theming;
using RSSmote.App.Views;
using RSSmote.Client;
using RSSmote.Core.Models;

namespace RSSmote.App;

public partial class App : Application
{
    private RemoteSettings? _settings;

    /// <summary>The server, as the base address of a client the head made (A23).</summary>
    public HttpClient? Http { get; init; }

    /// <summary>Where this device keeps its own preferences.</summary>
    public IDeviceStorage? Storage { get; init; }

    /// <summary>The head knows the keyboard is Apple's, which a browser does not tell Avalonia.</summary>
    public bool AppleKeyboard { get; init; }

    /// <summary>⌘ on an Apple keyboard, Ctrl elsewhere: the modifier of every command shortcut (D15).</summary>
    public static KeyModifiers CommandModifier =>
        Current is App { AppleKeyboard: true } ? KeyModifiers.Meta
            : Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Applies this device's theme before anything shows, then hands the view to <see cref="SessionView"/>, which signs
    /// in and opens the library (A23). Without a lifetime (headless tests) nothing is created.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
        {
            var http = Http ?? throw new InvalidOperationException("App needs Http, whose base address is the server.");
            var api = new ApiClient(http);
            _settings = new RemoteSettings(api, Storage ?? throw new InvalidOperationException("App needs Storage."));
            ApplyTheme();
            _settings.Changed += OnSettingsChanged;
            if (PlatformSettings is { } platform)
            {
                platform.ColorValuesChanged += OnColorValuesChanged;
            }

            single.MainView = new SessionView(api, _settings, http);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => Dispatcher.UIThread.Post(ApplyTheme);

    private void OnColorValuesChanged(object? sender, PlatformColorValues values) => Dispatcher.UIThread.Post(ApplyTheme);

    /// <summary>Shows the palette of the slot the setting names; Automatic asks the platform which slot (D22).</summary>
    private void ApplyTheme()
    {
        if (_settings is null)
        {
            return;
        }

        var prefersDark = PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
        RequestedThemeVariant = ThemeCatalogue.VariantFor(_settings.Current, prefersDark);
    }
}
