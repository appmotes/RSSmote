using Avalonia.Controls;
using Avalonia.Interactivity;

namespace RSSmote.App.Views;

public partial class SignInView : UserControl
{
    public SignInView() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        KeyField.Focus();
    }
}
