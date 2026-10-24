using Avalonia;

namespace RSSmote.Mobile.Tests;

/// <summary>A transition that runs until the test lets it finish, as a slide does on a device.</summary>
internal sealed class HeldTransition : Avalonia.Animation.IPageTransition
{
    private TaskCompletionSource _done = new();

    public void Finish()
    {
        var done = _done;
        _done = new TaskCompletionSource();
        done.TrySetResult();
    }

    public Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken) => _done.Task;
}
