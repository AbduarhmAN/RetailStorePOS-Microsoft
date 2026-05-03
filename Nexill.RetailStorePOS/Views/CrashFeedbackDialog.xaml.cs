using Microsoft.UI.Xaml.Controls;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class CrashFeedbackDialog : ContentDialog
{
    public string SelectedCategory { get; private set; } = "checkout";
    public string FeedbackDetails { get; private set; } = string.Empty;
    public bool SuppressFuturePrompts { get; private set; } = false;

    public CrashFeedbackDialog()
    {
        this.InitializeComponent();
        
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
}
