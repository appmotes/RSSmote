using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RSSmote.Desktop.Views.Settings;

public partial class ConfirmView : UserControl
{
    public ConfirmView() => InitializeComponent();

    /// <summary>Cancel takes the keyboard, so Enter or Space never confirms by accident.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => CancelButton.Focus(), DispatcherPriority.Loaded);
    }
}
