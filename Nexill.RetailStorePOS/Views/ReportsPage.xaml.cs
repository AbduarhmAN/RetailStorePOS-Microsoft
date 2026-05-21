using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using global::RetailStorePOS.Data.Modules.Contracts;
using global::RetailStorePOS.Data.Modules.Reporting;
using RetailStorePOS.App.Services.Licensing;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Views.AdvancedReports;
namespace RetailStorePOS.WinUiLogin.Views;

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

    // -----------------------------------------------------------------
    // Readiness pass: runs ReadinessService.ExecuteReadinessPassAsync,
    // refreshes Last Refresh, then shows a results dialog with per-scenario
    // detail loaded from the persisted report.
    // -----------------------------------------------------------------
    private bool _isReadinessRunning;

    private async void RunReadinessButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isReadinessRunning) return;
        _isReadinessRunning = true;

        try
        {
            RunReadinessButton.IsEnabled = false;
            ReadinessProgressRing.IsActive = true;
            ReadinessProgressRing.Visibility = Visibility.Visible;
            ReadinessStatusText.Text = LocalizationHelper.GetString("ReportsPage_Readiness_Running");

            // Run the pass off the UI thread so the ring keeps spinning.
            var run = await Task.Run(() => LoginRuntime.Readiness.ExecuteReadinessPassAsync());

            ReadinessStatusText.Text = LocalizationHelper.Format(
                "ReportsPage_Readiness_StatusSummary",
                run.RunId,
                run.FailedScenarios,
                Math.Max(0, run.TotalScenarios - run.PassedScenarios - run.FailedScenarios));

            LastRefreshText.Text = LocalizationHelper.Format(
                "ReportsPage_LastRefresh_Format",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            await ShowReadinessResultsAsync(run);
        }
        catch (Exception ex)
        {
            ReadinessStatusText.Text = LocalizationHelper.Format(
                "Operations_Status_FailedFormat", ex.Message);
        }
        finally
        {
            ReadinessProgressRing.IsActive = false;
            ReadinessProgressRing.Visibility = Visibility.Collapsed;
            RunReadinessButton.IsEnabled = true;
            _isReadinessRunning = false;
        }
    }

    private async Task ShowReadinessResultsAsync(ReadinessRun run)
    {
        // Load the persisted report so we can list per-scenario detail in the
        // dialog body. If the file is missing for any reason, fall back to a
        // summary-only view.
        var report = LoginRuntime.Readiness.GetLatestReport(run.RunId);
        var detailLines = report?.Results
            .OrderBy(r => r.Severity)
            .ThenBy(r => r.ScenarioKey)
            .Select(r => $"[{r.Status}] {r.ScenarioKey}: {r.Summary}")
            .ToList() ?? new System.Collections.Generic.List<string>();

        var status = run.FailedScenarios == 0
            ? LocalizationHelper.GetString("ReportsPage_LastRefresh_Unknown")
            : run.OverallStatus;
        if (run.FailedScenarios == 0 && run.PassedScenarios > 0) status = "Passed";
        else if (run.FailedScenarios > 0) status = "Failed";

        var body = LocalizationHelper.Format(
            "ReportsPage_Readiness_DialogBody",
            status,
            run.StartedAt,
            run.CompletedAt ?? "—",
            run.FailedScenarios,
            Math.Max(0, run.TotalScenarios - run.PassedScenarios - run.FailedScenarios),
            detailLines.Count > 0 ? string.Join("\n", detailLines) : string.Empty);

        var dialog = new ContentDialog
        {
            Title = LocalizationHelper.GetString("ReportsPage_Readiness_DialogTitle"),
            Content = new ScrollViewer
            {
                MaxHeight = 480,
                Content = new TextBlock
                {
                    Text = body,
                    TextWrapping = TextWrapping.Wrap,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                }
            },
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }
}

