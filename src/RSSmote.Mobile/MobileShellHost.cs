using Avalonia.Controls;
using Avalonia.Threading;
using RSSmote.App;
using RSSmote.App.Services;
using RSSmote.App.ViewModels;
using RSSmote.Mobile.Services;
using RSSmote.Mobile.Shell;

namespace RSSmote.Mobile;

/// <summary>The phone shell (A16): one navigation stack whose root page carries the tab bar.</summary>
public sealed class MobileShellHost : IShellHost
{
    private Control? _root;
    private ShellViewModel? _shell;

    public MobileShellHost() => Launcher = new TopLevelLauncher(() => TopLevel.GetTopLevel(_root));

    public ILauncher Launcher { get; }

    /// <summary>The system's text size as a factor; the platform heads set it at start and when it changes (D31).</summary>
    public double SystemTextScale
    {
        get;
        set
        {
            field = value;
            if (_shell is { } shell)
                Dispatcher.UIThread.Post(() => shell.Settings.SystemTextScale = value);
        }
    } = 1;

    public Control CreateRoot(ShellViewModel shell)
    {
        _shell = shell;
        shell.Settings.SystemTextScale = SystemTextScale;
        return _root = new MobileShell(shell);
    }

    public SidebarLayout Layout => SidebarLayout.Phone;
}
