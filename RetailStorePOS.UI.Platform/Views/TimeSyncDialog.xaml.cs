using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Platform.Views;

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
        
        this.Opened += TimeSyncDialog_Opened;
        this.Closed += TimeSyncDialog_Closed;
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

    private void Loc_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => Bindings.Update());
    }

    private void TimeSyncDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        Loc.PropertyChanged += Loc_PropertyChanged;
    }

    private void TimeSyncDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        Loc.PropertyChanged -= Loc_PropertyChanged;
        this.Bindings.StopTracking();
    }
}
