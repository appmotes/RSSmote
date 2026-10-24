using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RSSmote.Desktop.Views.Settings;

public partial class NewFolderView : UserControl
{
    public NewFolderView() => InitializeComponent();

    /// <summary>The name field takes the keyboard.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => NameBox.Focus(), DispatcherPriority.Loaded);
    }
}
