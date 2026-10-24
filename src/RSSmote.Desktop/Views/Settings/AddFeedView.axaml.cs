using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RSSmote.Desktop.Views.Settings;

public partial class AddFeedView : UserControl
{
    public AddFeedView() => InitializeComponent();

    /// <summary>
    /// The address field takes the keyboard, so typing starts at once; also on the fresh page after a save, which may
    /// reuse this view.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusAddress();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (TopLevel.GetTopLevel(this) is not null)
            FocusAddress();
    }

    private void FocusAddress() => Dispatcher.UIThread.Post(() => AddressBox.Focus(), DispatcherPriority.Loaded);
}
