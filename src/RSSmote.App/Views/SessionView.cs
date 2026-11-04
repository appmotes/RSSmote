using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;
using RSSmote.Client;

namespace RSSmote.App.Views;

/// <summary>
/// Signs the device in and opens the library (A23): <see cref="SignInView"/> until the server knows this device, then
/// <see cref="MainView"/> over a <see cref="RemoteSession"/>. Signed out anywhere (a new key on the server, the
/// cookie gone), it closes the session and asks for the key again.
/// </summary>
public sealed class SessionView : UserControl
{
    public const string SignedOutText = "Signed out. Enter the key again.";

    private readonly ApiClient _api;
    private readonly RemoteSettings _settings;
    private readonly HttpClient _http;
    private RemoteSession? _session;
    private ShellViewModel? _shell;

    public SessionView(ApiClient api, RemoteSettings settings, HttpClient http)
    {
        _api = api;
        _settings = settings;
        _http = http;
        api.SignedOut += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (_session is not null)
            {
                ShowSignIn(SignedOutText);
            }
        });
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Content is null)
        {
            _ = StartAsync();
        }
    }

    private async Task StartAsync()
    {
        try
        {
            if (await _api.IsSignedInAsync())
            {
                await OpenAsync();
            }
            else
            {
                ShowSignIn(null);
            }
        }
        catch (HttpRequestException)
        {
            ShowSignIn(SignInViewModel.Unreachable);
        }
    }

    private void ShowSignIn(string? message)
    {
        Close();
        Content = new SignInView
        {
            DataContext = new SignInViewModel(_api, () => _ = OpenAsync())
            {
                Error = message,
            },
        };
    }

    private async Task OpenAsync()
    {
        var session = new RemoteSession(_api, _settings);
        try
        {
            await session.StartAsync();
        }
        catch (Exception e) when (e is HttpRequestException or SignedOutException or TaskCanceledException)
        {
            session.Dispose();
            ShowSignIn(e is SignedOutException ? SignedOutText : SignInViewModel.Unreachable);
            return;
        }
        Close();
        _session = session;
        TopLevel? Top() => TopLevel.GetTopLevel(this);
        _shell = new ShellViewModel(session.Library, session.Settings, session.Refresher, session.Finder,
            new StorageFilePicker(Top), new ImageCache(_http, ImageCache.ThroughServer), new TopLevelLauncher(Top),
            new AvaloniaUiDispatcher(), TimeProvider.System);
        Content = new MainView
        {
            DataContext = _shell,
        };
    }

    private void Close()
    {
        Content = null;
        _shell?.Dispose();
        _session?.Dispose();
        _shell = null;
        _session = null;
    }
}
