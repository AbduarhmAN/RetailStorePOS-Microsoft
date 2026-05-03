using Microsoft.UI.Xaml.Controls;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class OpenRegisterDialog : ContentDialog
{
    public double OpeningCash => OpeningCashBox.Value;
    public string Note => NoteBox.Text;

    public OpenRegisterDialog()
    {
        this.InitializeComponent();
        this.Opened += OpenRegisterDialog_Opened;
    }

    private void OpenRegisterDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        OpeningCashBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }
}
