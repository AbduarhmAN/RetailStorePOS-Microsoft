using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using global::RetailStorePOS.Data.Modules.Contracts;
using global::RetailStorePOS.Data.Modules.Reporting;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.Views;

public class ProductVelocityItem
{
    public string ProductName { get; set; } = string.Empty;
    public string VelocityText { get; set; } = string.Empty;
}

public sealed partial class ReportsPage : Page
{
    private static readonly WorkflowBoundary ReportingWorkflow = ReportingWorkflowContract.ReportingRefreshBoundary;

    public ReportsPage()
    {
        InitializeComponent();
        Loaded += ReportsPage_Loaded;
    }

    private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        NavigateReportingSection(typeof(ReportsDashboardPage), ReportingWorkflowContract.DashboardReadModelQuery);
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            string tag = item.Tag?.ToString() ?? "";

            if (tag == "Dashboard")
                NavigateReportingSection(typeof(ReportsDashboardPage), ReportingWorkflowContract.DashboardReadModelQuery);
            else if (tag == "Receipts")
                NavigateReportingSection(typeof(ReportsReceiptsPage), ReportingWorkflowContract.ReceiptHistoryQuery);
        }
    }

    private void NavigateReportingSection(Type pageType, ModuleContract contract)
    {
        _ = ReportingWorkflow;
        _ = contract;
        ReportsContentFrame.Navigate(pageType);
    }

}
