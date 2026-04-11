using Microsoft.UI.Xaml.Controls;

namespace RetailStorePOS.WinUiLogin.Views;

public class ProductVelocityItem
{
    public string ProductName { get; set; } = string.Empty;
    public string VelocityText { get; set; } = string.Empty;
}

public sealed partial class ReportsPage : Page
{
    public ReportsPage()
    {
        InitializeComponent();
        
        // Default to loading the dashboard completely instantly
        Loaded += (_, _) => ReportsContentFrame.Navigate(typeof(ReportsDashboardPage));
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            string tag = item.Tag?.ToString() ?? "";
            
            if (tag == "Dashboard")
                ReportsContentFrame.Navigate(typeof(ReportsDashboardPage));
            else if (tag == "Receipts")
                ReportsContentFrame.Navigate(typeof(ReportsReceiptsPage));
        }
    }
}
