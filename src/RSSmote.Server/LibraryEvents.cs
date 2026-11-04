using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using RSSmote.Core.Api;
using RSSmote.Core.Library;
using RSSmote.Core.Models;
using RSSmote.Data;

namespace RSSmote.Server;

/// <summary>The client a request came from, for as long as the request runs; null for the server's own work.</summary>
public static class RequestOrigin
{
    private static readonly AsyncLocal<string?> Value = new();

    public static string? Current
    {
        get => Value.Value;
        set => Value.Value = value;
    }
}

/// <summary>
/// Tells every connected client what changed (A18): flags with their new values, a structural change as one
/// "library" event per <see cref="Coalesce"/>, the refresh state and the library settings.
/// </summary>
public sealed class LibraryEvents : IDisposable
{
    public static readonly TimeSpan Coalesce = TimeSpan.FromMilliseconds(500);
    public static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(25);

    private readonly Store _store;
    private readonly SettingsStore _settings;
    private readonly Refresher _refresher;
    private readonly ITimer _timer;
    private readonly Lock _gate = new();
    private readonly List<Channel<SseItem<string>>> _subscribers = [];
    private bool _pending;

    public LibraryEvents(Store store, SettingsStore settings, Refresher refresher, TimeProvider clock)
    {
        _store = store;
        _settings = settings;
        _refresher = refresher;
        _timer = clock.CreateTimer(_ => PublishLibrary(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        store.Changed += OnStoreChanged;
        settings.Changed += OnSettingsChanged;
        refresher.StateChanged += OnRefreshStateChanged;
    }

    public void Dispose()
    {
        _store.Changed -= OnStoreChanged;
        _settings.Changed -= OnSettingsChanged;
        _refresher.StateChanged -= OnRefreshStateChanged;
        _timer.Dispose();
    }

    /// <summary>Events from now on, with a ping first and every <see cref="Heartbeat"/> so proxies keep the line open.</summary>
    public async IAsyncEnumerable<SseItem<string>> Subscribe([EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateBounded<SseItem<string>>(new BoundedChannelOptions(256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        lock (_gate)
        {
            _subscribers.Add(channel);
        }

        try
        {
            yield return Ping;
            while (!ct.IsCancellationRequested)
            {
                if (!await ArrivedAsync(channel.Reader, ct))
                {
                    yield return Ping;
                    continue;
                }
                while (channel.Reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    private static SseItem<string> Ping => new("", "ping");

    private static async Task<bool> ArrivedAsync(ChannelReader<SseItem<string>> reader, CancellationToken ct)
    {
        using var beat = CancellationTokenSource.CreateLinkedTokenSource(ct);
        beat.CancelAfter(Heartbeat);
        try
        {
            return await reader.WaitToReadAsync(beat.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
    }

    private void OnStoreChanged(object? sender, LibraryChangedEventArgs e)
    {
        if (e.ItemIds is not { } ids)
        {
            lock (_gate)
            {
                if (_pending)
                {
                    return;
                }

                _pending = true;
                _timer.Change(Coalesce, Timeout.InfiniteTimeSpan);
            }
            return;
        }
        var flags = _store.Flags(ids).Select(f => new ItemFlags(f.Id, f.Unread, f.Starred)).ToList();
        Publish(ApiNames.FlagsEvent, JsonSerializer.Serialize(new FlagsEvent(RequestOrigin.Current, flags), ApiJsonContext.Default.FlagsEvent));
    }

    private void PublishLibrary()
    {
        lock (_gate)
        {
            _pending = false;
        }

        Publish(ApiNames.LibraryEvent, "");
    }

    private void OnSettingsChanged(object? sender, AppSettings settings) =>
        Publish(ApiNames.SettingsEvent, JsonSerializer.Serialize(LibrarySettings.From(settings), ApiJsonContext.Default.LibrarySettings));

    private void OnRefreshStateChanged(object? sender, EventArgs e) =>
        Publish(ApiNames.RefreshEvent, _refresher.IsRefreshing ? "true" : "false");

    private void Publish(string type, string data)
    {
        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(new SseItem<string>(data, type));
            }
        }
    }
}
