using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class PlaceholderPage : Page
{
    public PlaceholderPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var sectionName = (e.Parameter as string) switch
        {
            "reports" => "Reports",
            "users" => "Users",
            "settings" => "Settings",
            _ => "Section"
        };

        SectionTitleText.Text = sectionName;
        SectionDescriptionText.Text = $"{sectionName} is now connected to the new WinUI navigation shell. This page is ready for the next implementation step.";
    }
}


