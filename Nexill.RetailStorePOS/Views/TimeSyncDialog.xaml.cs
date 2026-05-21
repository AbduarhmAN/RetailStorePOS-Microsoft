using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

public enum TimeSyncDialogResult
{
    Retry,
    Continue
}

public sealed partial class TimeSyncDialog : ContentDialog
{
    public TimeSyncDialogResult Result { get; private set; } = TimeSyncDialogResult.Retry;

    public TimeSyncDialog()
    {
        this.InitializeComponent();
        
        Loc.PropertyChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(() => Bindings.Update());
        };
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public string TimeSyncDialog_Title => Loc["TimeSyncDialog_Title.Text"];
    public string TimeSyncDialog_Description => Loc["TimeSyncDialog_Description.Text"];
    public string TimeSyncDialog_ContinueButton => Loc["TimeSyncDialog_ContinueButton.Content"];
    public string TimeSyncDialog_RetryButton => Loc["TimeSyncDialog_RetryButton.Content"];

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        Result = TimeSyncDialogResult.Retry;
        this.Hide();
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        Result = TimeSyncDialogResult.Continue;
        this.Hide();
    }
}
