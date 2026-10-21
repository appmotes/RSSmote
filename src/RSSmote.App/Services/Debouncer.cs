namespace RSSmote.App.Services;

/// <summary>
/// Runs an action on the UI thread once things have been quiet for <c>delay</c>. Many store writes during a
/// refresh become one reload.
/// </summary>
public sealed class Debouncer(TimeProvider clock, TimeSpan delay, IUiDispatcher ui, Action action) : IDisposable
{
    private readonly Lock _gate = new();
    private ITimer? _timer;

    public bool IsPending
    {
        get
        {
            lock (_gate)
                return _timer is not null;
        }
    }

    public void Trigger()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = clock.CreateTimer(_ => Fire(), null, delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Runs a pending action now (on the calling thread).</summary>
    public void Flush()
    {
        if (Cancel())
            action();
    }

    public void Dispose() => Cancel();

    private void Fire()
    {
        if (Cancel())
            ui.Post(action);
    }

    private bool Cancel()
    {
        lock (_gate)
        {
            if (_timer is null)
                return false;
            _timer.Dispose();
            _timer = null;
            return true;
        }
    }
}
