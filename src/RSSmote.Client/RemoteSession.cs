using System.Net.ServerSentEvents;
using System.Text.Json;
using RSSmote.Core.Api;

namespace RSSmote.Client;

/// <summary>
/// A signed-in device's view of the server (A20): the mirrored library, the settings, the refresh, and the event
/// stream that keeps them current. <see cref="StartAsync"/> loads the library before anything shows it; the stream
/// then reconnects on its own and fetches the library again each time, so nothing is missed while it was away.
/// </summary>
public sealed class RemoteSession : IDisposable
{
    public static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan LastRetry = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _stop = new();

    /// <param name="settings">Outlives the session: the device's own part shows before anyone signs in.</param>
    public RemoteSession(ApiClient api, RemoteSettings settings)
    {
        Api = api;
        Library = new RemoteLibrary(api);
        Settings = settings;
        Refresher = new RemoteRefresher(api);
        Finder = new RemoteFeedFinder(api);
        Library.Loaded += (_, snapshot) =>
        {
            Settings.Apply(snapshot.Settings);
            Refresher.Apply(snapshot.IsRefreshing);
        };
    }

    public ApiClient Api { get; }
    public RemoteLibrary Library { get; }
    public RemoteSettings Settings { get; }
    public RemoteRefresher Refresher { get; }
    public RemoteFeedFinder Finder { get; }

    /// <summary>Loads the library, then follows the event stream until disposed or signed out.</summary>
    public async Task StartAsync()
    {
        await Library.ReloadAsync(_stop.Token);
        _ = FollowAsync(_stop.Token);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    private async Task FollowAsync(CancellationToken ct)
    {
        var retry = FirstRetry;
        while (true)
        {
            try
            {
                using var response = await Api.OpenEventsAsync(ct);
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                // Open from here on, so fetching now covers whatever changed before it opened.
                await Library.ReloadAsync(ct);
                retry = FirstRetry;
                await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
                    Dispatch(item);
            }
            catch (SignedOutException)
            {
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or JsonException)
            {
            }
            try
            {
                await Task.Delay(retry, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            retry = TimeSpan.FromTicks(Math.Min(retry.Ticks * 2, LastRetry.Ticks));
        }
    }

    private void Dispatch(SseItem<string> item)
    {
        switch (item.EventType)
        {
            case ApiNames.FlagsEvent when JsonSerializer.Deserialize(item.Data, ApiJsonContext.Default.FlagsEvent) is { } flags:
                Library.ApplyFlags(flags.Items);
                break;
            case ApiNames.LibraryEvent:
                _ = ReloadQuietlyAsync();
                break;
            case ApiNames.RefreshEvent:
                Refresher.Apply(item.Data == "true");
                break;
            case ApiNames.SettingsEvent when JsonSerializer.Deserialize(item.Data, ApiJsonContext.Default.LibrarySettings) is { } settings:
                Settings.Apply(settings);
                break;
        }
    }

    private async Task ReloadQuietlyAsync()
    {
        try
        {
            await Library.ReloadAsync(_stop.Token);
        }
        catch (Exception e) when (e is HttpRequestException or SignedOutException or OperationCanceledException or JsonException)
        {
        }
    }
}
