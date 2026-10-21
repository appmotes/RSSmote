using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using RSSmote.App.Controls.Headless;
using RSSmote.App.Services;
using RSSmote.App.Theming;
using RSSmote.App.ViewModels;
using RSSmote.Core.Models;

namespace RSSmote.App;

public partial class App : Application
{
    private AppServices? _services;
    private ShellViewModel? _shell;

    /// <summary>The head's launcher and main window; required under the desktop lifetime.</summary>
    public IShellHost? Host { get; init; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var host = Host ?? throw new InvalidOperationException("App needs a Host under the desktop lifetime.");
            // RSSMOTE_ARTICLE_RENDERER=fallback shows the renderer used where no web view is installed.
            if (Enum.TryParse<ArticleRenderer>(Environment.GetEnvironmentVariable("RSSMOTE_ARTICLE_RENDERER"), true, out var renderer))
                ArticleHost.DefaultRenderer = renderer;
            _services = new AppServices(() => desktop.MainWindow, host.Launcher);
            // The stored theme applies before the first window, so the first frame is already right.
            ApplyTheme();
            _services.Settings.Changed += OnSettingsChanged;
            if (PlatformSettings is { } platform)
                platform.ColorValuesChanged += OnColorValuesChanged;

            _shell = new ShellViewModel(_services.Store, _services.Settings, _services.Refresher, _services.Finder, _services.Files, _services.Images,
                _services.Launcher, _services.Ui, _services.Clock);
            desktop.MainWindow = host.CreateMainWindow(_shell);
            desktop.ShutdownRequested += (_, _) => Shutdown();
            _services.Refresher.Start();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) => Dispatcher.UIThread.Post(ApplyTheme);

    private void OnColorValuesChanged(object? sender, PlatformColorValues values) => Dispatcher.UIThread.Post(ApplyTheme);

    /// <summary>Shows the palette of the slot the setting names; Automatic asks the OS which slot (D22).</summary>
    private void ApplyTheme()
    {
        if (_services is null)
            return;
        var prefersDark = PlatformSettings?.GetColorValues().ThemeVariant == PlatformThemeVariant.Dark;
        RequestedThemeVariant = ThemeCatalogue.VariantFor(_services.Settings.Current, prefersDark);
    }

    private void Shutdown()
    {
        if (_services is not null)
            _services.Settings.Changed -= OnSettingsChanged;
        if (PlatformSettings is { } platform)
            platform.ColorValuesChanged -= OnColorValuesChanged;
        _shell?.Dispose();
        _services?.Dispose();
        _shell = null;
        _services = null;
    }
}
