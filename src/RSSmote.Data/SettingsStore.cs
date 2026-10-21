using System.Text.Json;
using System.Text.Json.Serialization;
using RSSmote.Core.Models;

namespace RSSmote.Data;

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

/// <summary>
/// Preferences in one JSON file next to the library (A8). <see cref="Current"/> is an immutable snapshot;
/// <see cref="Update"/> applies a change, normalises it, writes the file atomically and raises <see cref="Changed"/>.
/// A missing or unreadable file starts from the defaults.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private AppSettings _current;

    public event EventHandler<AppSettings>? Changed;

    public SettingsStore(string path)
    {
        _path = path;
        _current = Load(path);
    }

    public AppSettings Current
    {
        get
        {
            lock (_gate)
                return _current;
        }
    }

    public AppSettings Update(Action<AppSettings> change)
    {
        AppSettings next;
        lock (_gate)
        {
            var draft = _current with { };
            change(draft);
            next = draft.Normalized();
            if (next == _current)
                return _current;
            Save(next);
            _current = next;
        }
        Changed?.Invoke(this, next);
        return next;
    }

    private static AppSettings Load(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new AppSettings();
            using var stream = File.OpenRead(path);
            return (JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings) ?? new AppSettings()).Normalized();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new AppSettings();
        }
    }

    private void Save(AppSettings settings)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(_path)) is { } folder)
            Directory.CreateDirectory(folder);
        var temp = _path + ".tmp";
        using (var stream = File.Create(temp))
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        File.Move(temp, _path, overwrite: true);
    }
}
