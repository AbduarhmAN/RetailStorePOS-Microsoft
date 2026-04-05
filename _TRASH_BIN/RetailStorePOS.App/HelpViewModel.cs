using System.IO;
using System.Windows;

namespace RetailStorePOS.App;

public class HelpViewModel : ViewModelBase
{
    private string _policyText = string.Empty;
    public string PolicyText
    {
        get => _policyText;
        set => SetProperty(ref _policyText, value);
    }

    public HelpViewModel()
    {
        LoadPolicyText();
    }

    private void LoadPolicyText()
    {
        try
        {
            // Read the embedded resource directly from the assembly
            var uri = new Uri("pack://application:,,,/Retail Store POS;component/Resources/Policies/retail_store_pos_terms_and_privacy_policy.txt", UriKind.Absolute);
            var streamInfo = Application.GetResourceStream(uri);
            if (streamInfo != null)
            {
                using var reader = new StreamReader(streamInfo.Stream);
                PolicyText = reader.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load policy text: {ex.Message}");
            AppServices.ReportException(ex, "HelpViewModel.LoadPolicyText");
            PolicyText = "Could not load the policy document. Please contact support.";
        }
    }
}
