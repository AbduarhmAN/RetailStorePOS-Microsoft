using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    }

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
