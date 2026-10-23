using Avalonia.Controls;
using RSSmote.App;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;
using RSSmote.Desktop.Services;
using RSSmote.Desktop.Shell;

namespace RSSmote.Desktop;

public sealed class DesktopShellHost : IShellHost
{
    public ILauncher Launcher { get; } = new ProcessLauncher();

    public Control CreateRoot(ShellViewModel shell) => new MainWindow(shell);

    public SidebarLayout Layout => SidebarLayout.Desktop;
}
