using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RSSmote.App.ViewModels;

/// <summary>An overlay panel (A6): one is open at a time, on <see cref="ShellViewModel.Panel"/>.</summary>
public abstract partial class PanelViewModel : ObservableObject, IDisposable
{
    /// <summary>The panel asks to close.</summary>
    public event EventHandler? Closed;

    public virtual void Dispose()
    {
    }

    [RelayCommand]
    protected void Close()
    {
        Dispose();
        Closed?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Asks before something that cannot be undone.</summary>
public sealed partial class ConfirmViewModel(string title, string message, string actionTitle, Action confirm) : PanelViewModel
{
    public string Title { get; } = title;
    public string Message { get; } = message;
    public string ActionTitle { get; } = actionTitle;

    [RelayCommand]
    private void Confirm()
    {
        confirm();
        Close();
    }
}
