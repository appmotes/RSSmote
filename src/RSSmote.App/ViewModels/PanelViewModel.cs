using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace RSSmote.App.ViewModels;

/// <summary>A page a flow opens and closes: a Settings page on desktop (A6), a pushed page on the phone (A16).</summary>
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

    /// <summary>Raised after the action ran, just before <see cref="PanelViewModel.Closed"/>.</summary>
    public event EventHandler? Confirmed;

    [RelayCommand]
    private void Confirm()
    {
        confirm();
        Confirmed?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
