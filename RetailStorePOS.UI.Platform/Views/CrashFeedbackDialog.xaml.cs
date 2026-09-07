using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.UI.Common;

namespace RetailStorePOS.UI.Platform.Views;

public sealed partial class CrashFeedbackDialog : ContentDialog
{
    public string SelectedCategory { get; private set; } = "checkout";
    public string FeedbackDetails { get; private set; } = string.Empty;
    public bool SuppressFuturePrompts { get; private set; } = false;

    public CrashFeedbackDialog()
    {
        this.InitializeComponent();

        this.Opened += CrashFeedbackDialog_Opened;
        this.Closed += CrashFeedbackDialog_Closed;
        
        IssueCategoryRadioGroup.SelectionChanged += IssueCategoryRadioGroup_SelectionChanged;
        DetailsTextBox.TextChanged += DetailsTextBox_TextChanged;

        SuppressPromptCheckBox.Checked += SuppressPromptCheckBox_Checked;
        SuppressPromptCheckBox.Unchecked += SuppressPromptCheckBox_Unchecked;
    }

    private void IssueCategoryRadioGroup_SelectionChanged(object sender, Microsoft.UI.Xaml.Controls.SelectionChangedEventArgs e)
    {
        if (IssueCategoryRadioGroup.SelectedItem is RadioButton rb && rb.Tag is string tag)
        {
            SelectedCategory = tag;
        }
    }

    private void DetailsTextBox_TextChanged(object sender, Microsoft.UI.Xaml.Controls.TextChangedEventArgs e)
    {
        FeedbackDetails = DetailsTextBox.Text;
    }

    private void SuppressPromptCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SuppressFuturePrompts = true;
    }

    private void SuppressPromptCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        SuppressFuturePrompts = false;
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public string CrashFeedbackDialog_Title => Loc["CrashFeedbackDialog_Title.Text"];
    public string CrashFeedbackDialog_IntroText => Loc["CrashFeedbackDialog_IntroText.Text"];
    public string CrashFeedbackDialog_Checkout => Loc["CrashFeedbackDialog_Checkout.Content"];
    public string CrashFeedbackDialog_Products => Loc["CrashFeedbackDialog_Products.Content"];
    public string CrashFeedbackDialog_Reports => Loc["CrashFeedbackDialog_Reports.Content"];
    public string CrashFeedbackDialog_Settings => Loc["CrashFeedbackDialog_Settings.Content"];
    public string CrashFeedbackDialog_Startup => Loc["CrashFeedbackDialog_Startup.Content"];
    public string CrashFeedbackDialog_Other => Loc["CrashFeedbackDialog_Other.Content"];
    public string CrashFeedbackDialog_DetailsTextBoxHeader => Loc["CrashFeedbackDialog_DetailsTextBox.Header"];
    public string CrashFeedbackDialog_DetailsTextBoxPlaceholder => Loc["CrashFeedbackDialog_DetailsTextBox.PlaceholderText"];
    public string CrashFeedbackDialog_SuppressPromptCheckBox => Loc["CrashFeedbackDialog_SuppressPromptCheckBox.Content"];
    public string CrashFeedbackDialog_PrimaryButtonText => Loc["CrashFeedbackDialog.PrimaryButtonText"];
    public string CrashFeedbackDialog_SecondaryButtonText => Loc["CrashFeedbackDialog.CloseButtonText"];

    private void Loc_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => Bindings.Update());
    }

    private void CrashFeedbackDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        Loc.PropertyChanged += Loc_PropertyChanged;
    }

    private void CrashFeedbackDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        Loc.PropertyChanged -= Loc_PropertyChanged;
        IssueCategoryRadioGroup.SelectionChanged -= IssueCategoryRadioGroup_SelectionChanged;
        DetailsTextBox.TextChanged -= DetailsTextBox_TextChanged;
        SuppressPromptCheckBox.Checked -= SuppressPromptCheckBox_Checked;
        SuppressPromptCheckBox.Unchecked -= SuppressPromptCheckBox_Unchecked;
        this.Bindings.StopTracking();
    }
}
