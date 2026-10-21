using Avalonia.Threading;

namespace RSSmote.App.Services;

/// <summary>Runs work on the UI thread (A11). Store and refresher events arrive on any thread.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
            action();
        else
            Dispatcher.UIThread.Post(action);
    }
}
