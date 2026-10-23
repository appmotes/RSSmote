using Avalonia.Controls;
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

    public MobileShellHost() => Launcher = new TopLevelLauncher(() => TopLevel.GetTopLevel(_root));

    public ILauncher Launcher { get; }

    public Control CreateRoot(ShellViewModel shell) => _root = new MobileShell(shell);

    public SidebarLayout Layout => SidebarLayout.Phone;
}
