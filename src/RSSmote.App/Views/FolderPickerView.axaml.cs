using Avalonia.Controls;

namespace RSSmote.App.Views;

public partial class FolderPickerView : UserControl
{
    public FolderPickerView() => InitializeComponent();

    /// <summary>Puts the cursor in the new-folder field.</summary>
    public void FocusNewFolder() => FolderBox.Focus();
}
