using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RSSmote.Client;

namespace RSSmote.App.ViewModels;

/// <summary>The first screen on a device that is not signed in: the key set on the server, and nothing else (A17).</summary>
public sealed partial class SignInViewModel(ApiClient api, Action signedIn) : ObservableObject
{
    public const string WrongKey = "That is not the key set on the server.";
    public const string Unreachable = "The server could not be reached.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial string Key { get; set; } = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? Error { get; set; }

    public bool HasError => Error is not null;

    private bool CanSignIn() => !IsBusy && Key.Length > 0;

    [RelayCommand(CanExecute = nameof(CanSignIn))]
    private async Task SignIn()
    {
        IsBusy = true;
        Error = null;
        try
        {
            if (await api.SignInAsync(Key))
            {
                Key = "";
                signedIn();
            }
            else
            {
                Error = WrongKey;
            }
        }
        catch (HttpRequestException e)
        {
            Error = e.StatusCode is null ? Unreachable : e.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
