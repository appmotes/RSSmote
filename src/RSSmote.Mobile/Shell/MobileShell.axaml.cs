using Avalonia.Controls;
using RSSmote.App.ViewModels;

namespace RSSmote.Mobile.Shell;

/// <summary>The one navigation stack of the phone shell (D23); its root page carries the tab bar.</summary>
public partial class MobileShell : NavigationPage
{
    public MobileShell() => InitializeComponent();

    public MobileShell(ShellViewModel shell) : this() => Content = new RootPage(shell);

    public RootPage Root => (RootPage)Content!;
}
