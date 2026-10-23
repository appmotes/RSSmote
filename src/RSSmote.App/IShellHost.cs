using Avalonia.Controls;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;

namespace RSSmote.App;

/// <summary>What a head supplies to the shared <see cref="App"/> (A3): its launcher and its root control.</summary>
public interface IShellHost
{
    ILauncher Launcher { get; }

    /// <summary>The main window under the desktop lifetime; the main view under a mobile one.</summary>
    Control CreateRoot(ShellViewModel shell);

    /// <summary>Desktop columns or the phone stack, known before the shell view-model is built.</summary>
    SidebarLayout Layout { get; }
}
