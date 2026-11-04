namespace RSSmote.App.Views.Phone.Pages;

public partial class NewFolderPage : PhonePage
{
    public NewFolderPage()
    {
        InitializeComponent();
        NavigatedTo += (_, _) => NameField.Focus();
    }
}
