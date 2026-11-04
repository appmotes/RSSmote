using System.Text.Json;
using RSSmote.Core.Api;
using RSSmote.Core.Library;
using RSSmote.Core.Models;

namespace RSSmote.Client;

/// <summary>Where a device keeps its own preferences: the browser's local storage.</summary>
public interface IDeviceStorage
{
    string? Read(string key);

    /// <summary>False when the device would not keep it (a private window, blocked site data).</summary>
    bool Write(string key, string value);
}

/// <summary>
/// Preferences split in two (A21): <see cref="LibrarySettings"/> live on the server and follow the library to
/// every device; everything else (theme, text sizes, filter, folders open) belongs to this device and is kept in
/// its <see cref="IDeviceStorage"/>.
/// </summary>
public sealed class RemoteSettings : ISettingsStore
{
    public const string StorageKey = "rssmote.settings";

    private readonly ApiClient _api;
    private readonly IDeviceStorage _storage;
    private readonly Lock _gate = new();
    private AppSettings _current;

    public RemoteSettings(ApiClient api, IDeviceStorage storage)
    {
        _api = api;
        _storage = storage;
        _current = Load(storage);
    }

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public AppSettings Update(Action<AppSettings> change)
    {
        AppSettings previous, next;
        lock (_gate)
        {
            previous = _current;
            var draft = _current with { };
            change(draft);
            next = draft.Normalized();
            if (next == _current)
            {
                return _current;
            }

            _current = next;
        }
        _storage.Write(StorageKey, JsonSerializer.Serialize(next, ApiJsonContext.Default.AppSettings));
        var library = LibrarySettings.From(next);
        if (library with { LastRefresh = null } != LibrarySettings.From(previous) with { LastRefresh = null })
        {
            _ = SaveAsync(library);
        }

        Changed?.Invoke(this, next);
        return next;
    }

    /// <summary>The server's part, from a snapshot or an event.</summary>
    public void Apply(LibrarySettings library)
    {
        AppSettings next;
        lock (_gate)
        {
            if (LibrarySettings.From(_current) == library)
            {
                return;
            }

            var draft = _current with { };
            library.ApplyTo(draft);
            next = _current = draft.Normalized();
        }
        Changed?.Invoke(this, next);
    }

    /// <summary>A save that fails is undone by the server's next answer.</summary>
    private async Task SaveAsync(LibrarySettings library)
    {
        try
        {
            Apply(await _api.SendAsync(HttpMethod.Put, "api/settings", library,
                ApiJsonContext.Default.LibrarySettings, ApiJsonContext.Default.LibrarySettings));
        }
        catch (Exception e) when (e is HttpRequestException or SignedOutException or TaskCanceledException)
        {
        }
    }

    private static AppSettings Load(IDeviceStorage storage)
    {
        try
        {
            return storage.Read(StorageKey) is { Length: > 0 } json
                ? (JsonSerializer.Deserialize(json, ApiJsonContext.Default.AppSettings) ?? new AppSettings()).Normalized()
                : new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
    }
}
