using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace RSSmote.App.Views;

public partial class EditFolderView : UserControl
{
    public EditFolderView() => InitializeComponent();

    /// <summary>The name field takes the keyboard with its text selected, ready to be retyped.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }
}
