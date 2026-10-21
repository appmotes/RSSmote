using Avalonia.Controls;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;

namespace RSSmote.App;

/// <summary>What a head supplies to the shared <see cref="App"/> (A3): its launcher and its main window.</summary>
public interface IShellHost
{
    ILauncher Launcher { get; }

    Window CreateMainWindow(ShellViewModel shell);
}
