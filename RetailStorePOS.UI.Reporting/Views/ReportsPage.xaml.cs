using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using global::RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.UI.Common.Services;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Reporting.Views.AdvancedReports;
namespace RetailStorePOS.UI.Reporting.Views;

public class ProductVelocityItem
{
    public string ProductName { get; set; } = string.Empty;
    public string VelocityText { get; set; } = string.Empty;
}

public sealed partial class ReportsPage : Page
{
    private static readonly WorkflowBoundary ReportingWorkflow = ReportingWorkflowContract.ReportingRefreshBoundary;
    private bool _hasInitializedNavigation;

    public LocalizationService Loc => LocalizationService.Instance;

    public string Reports_Nav_Dashboard => Loc["Reports_Nav_Dashboard.Content"];
    public string Reports_Nav_Receipts => Loc["Reports_Nav_Receipts.Content"];

    public ReportsPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        ReportsContentFrame.CacheSize = 2;
        Loc.PropertyChanged += (s, e) => Bindings.Update();
        Loaded += ReportsPage_Loaded;
    }

    private void ReportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyFeatureGating();

        if (_hasInitializedNavigation && ReportsContentFrame.Content is not null)
        {
            return;
        }

        _hasInitializedNavigation = true;
        NavigateReportingSection(typeof(ReportsDashboardPage), ReportingWorkflowContract.DashboardReadModelQuery);
    }

    private void ApplyFeatureGating()
    {
        var canUseAdvanced = LoginRuntime.FeatureAccess?.CanUse(FeatureAccessService.Features.AdvancedReports) ?? false;
        AdvancedAnalyticsNavItem.Visibility = canUseAdvanced ? Visibility.Visible : Visibility.Collapsed;
        ProductPerformanceNavItem.Visibility = canUseAdvanced ? Visibility.Visible : Visibility.Collapsed;
        OperationsNavItem.Visibility = canUseAdvanced ? Visibility.Visible : Visibility.Collapsed;
        AbcXyzNavItem.Visibility = canUseAdvanced ? Visibility.Visible : Visibility.Collapsed;
        BasketAffinityNavItem.Visibility = canUseAdvanced ? Visibility.Visible : Visibility.Collapsed;
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
            else if (tag == "AdvancedAnalytics")
                NavigateReportingSection(typeof(RevenueDashboardPage), ReportingWorkflowContract.DashboardReadModelQuery);
            else if (tag == "ProductPerformance")
                NavigateReportingSection(typeof(ProductPerformancePage), ReportingWorkflowContract.DashboardReadModelQuery);
            else if (tag == "Operations")
                NavigateReportingSection(typeof(OperationsPage), ReportingWorkflowContract.DashboardReadModelQuery);
            else if (tag == "AbcXyz")
                NavigateReportingSection(typeof(AbcXyzPage), ReportingWorkflowContract.DashboardReadModelQuery);
            else if (tag == "BasketAffinity")
                NavigateReportingSection(typeof(BasketAffinityPage), ReportingWorkflowContract.DashboardReadModelQuery);
        }
    }

    private void NavigateReportingSection(Type pageType, ModuleContract contract)
    {
        _ = ReportingWorkflow;
        _ = contract;

        if (ReportsContentFrame.CurrentSourcePageType == pageType)
        {
            return;
        }

        ReportsContentFrame.Navigate(pageType);
    }
}

