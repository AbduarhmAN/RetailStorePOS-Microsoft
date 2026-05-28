using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class CrashFeedbackDialog : ContentDialog
{
    public string SelectedCategory { get; private set; } = "checkout";
    public string FeedbackDetails { get; private set; } = string.Empty;
    public bool SuppressFuturePrompts { get; private set; } = false;

    public CrashFeedbackDialog()
    {
        this.InitializeComponent();

        Loc.PropertyChanged += (s, e) =>
        {
            DispatcherQueue.TryEnqueue(() => Bindings.Update());
        };
        
        IssueCategoryRadioGroup.SelectionChanged += (s, e) => 
        {
            if (IssueCategoryRadioGroup.SelectedItem is RadioButton rb && rb.Tag is string tag)
            {
                SelectedCategory = tag;
            }
        };

        DetailsTextBox.TextChanged += (s, e) =>
        {
            FeedbackDetails = DetailsTextBox.Text;
        };

        SuppressPromptCheckBox.Checked += (s, e) => SuppressFuturePrompts = true;
        SuppressPromptCheckBox.Unchecked += (s, e) => SuppressFuturePrompts = false;
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
}
