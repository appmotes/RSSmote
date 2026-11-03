using RSSmote.Core.Models;

namespace RSSmote.Core.Library;

/// <summary>
/// Preferences: <see cref="Current"/> is an immutable snapshot, <see cref="Update"/> is the only way to write, and
/// <see cref="Changed"/> follows every change, wherever it came from.
/// </summary>
public interface ISettingsStore
{
    event EventHandler<AppSettings>? Changed;

    AppSettings Current { get; }

    AppSettings Update(Action<AppSettings> change);
}

/// <summary>The library's refresh as the view-models see it: whether one runs, and a way to start one.</summary>
public interface IRefresher
{
    /// <summary>Raised when a pass starts and after it ends, on any thread.</summary>
    event EventHandler? StateChanged;

    bool IsRefreshing { get; }

    /// <summary>Refreshes every feed; completes when the pass ends.</summary>
    Task RefreshAllAsync();
}
