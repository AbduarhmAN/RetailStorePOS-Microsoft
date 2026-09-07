using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Sales.Views;

public sealed partial class OpenRegisterDialog : ContentDialog
{
    public double OpeningCash => OpeningCashBox.Value;
    public string Note => NoteBox.Text;

    public OpenRegisterDialog()
    {
        this.InitializeComponent();
        this.Opened += OpenRegisterDialog_Opened;
        
        Loc.PropertyChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(() => Bindings.Update());
        };
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public string OpenRegisterDialog_Title => Loc["OpenRegisterDialog_Title.Text"];
    public string OpenRegisterDialog_IntroText => Loc["OpenRegisterDialog_IntroText.Text"];
    public string OpenRegisterDialog_OpeningCashHeader => Loc["OpenRegisterDialog_OpeningCashHeader.Text"];
    public string OpenRegisterDialog_OpeningCashBox => Loc["OpenRegisterDialog_OpeningCashBox.PlaceholderText"];
    public string OpenRegisterDialog_NotesHeader => Loc["OpenRegisterDialog_NotesHeader.Text"];
    public string OpenRegisterDialog_NoteBox => Loc["OpenRegisterDialog_NoteBox.PlaceholderText"];
    public string OpenRegisterDialog_PrimaryButtonText => Loc["OpenRegisterDialog.PrimaryButtonText"];
    public string OpenRegisterDialog_SecondaryButtonText => Loc["OpenRegisterDialog.SecondaryButtonText"];

    private void OpenRegisterDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        OpeningCashBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
    }
}
