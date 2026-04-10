using System.Reflection;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class AboutPage : Page
{
    private readonly string _displayVersion;

    public AboutPage()
    {
        _displayVersion = ResolveDisplayVersion();
        InitializeComponent();
        LoadAppInfo();
        LoadPolicyText();
    }

    private void LoadAppInfo()
    {
        VersionText.Text = $"Version {_displayVersion}";
    }

    private void LoadPolicyText()
    {
        var policyPath = Path.Combine(
            AppContext.BaseDirectory,
            "Resources",
            "Policies",
            "retail_store_pos_terms_and_privacy_policy.txt");

        if (File.Exists(policyPath))
        {
            var policyText = File.ReadAllText(policyPath)
                .Replace("{{APP_VERSION}}", _displayVersion, StringComparison.Ordinal)
                .Replace("{{POLICY_EFFECTIVE_DATE}}", GetAssemblyMetadata("RetailStorePOSPolicyEffectiveDate", "Unavailable"), StringComparison.Ordinal)
                .Replace("{{POLICY_LAST_UPDATED_DATE}}", GetAssemblyMetadata("RetailStorePOSPolicyLastUpdatedDate", "Unavailable"), StringComparison.Ordinal)
                .Replace("{{SUPPORT_EMAIL}}", GetAssemblyMetadata("RetailStorePOSSupportEmail", "Unavailable"), StringComparison.Ordinal)
                .Replace("{{PRIVACY_CONTACT_EMAIL}}", GetAssemblyMetadata("RetailStorePOSPrivacyContactEmail", "Unavailable"), StringComparison.Ordinal)
                .Replace("Version: 1.2.2", $"Version: {_displayVersion}", StringComparison.Ordinal);

            PolicyTextBlock.Text = policyText;
        }
        else
        {
            PolicyTextBlock.Text = "Privacy policy file not found.";
        }
    }

    private static string ResolveDisplayVersion()
    {
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+')[0];

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion;
        }

        try
        {
            var packageVersion = Package.Current.Id.Version;
            return $"{packageVersion.Major}.{packageVersion.Minor}.{packageVersion.Build}";
        }
        catch
        {
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is not null
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : "unavailable";
    }

    private static string GetAssemblyMetadata(string key, string fallback)
    {
        var value = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))?
            .Value;

        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}


