namespace RSSmote.Mobile.Pages;

public partial class NewFolderPage : PhonePage
{
    public NewFolderPage()
    {
        InitializeComponent();
        NavigatedTo += (_, _) => NameField.Focus();
    }
}
