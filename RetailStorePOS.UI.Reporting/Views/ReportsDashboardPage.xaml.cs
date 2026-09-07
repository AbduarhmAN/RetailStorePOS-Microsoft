using RetailStorePOS.UI.Common.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using global::RetailStorePOS.Data.Modules.Contracts;
using global::RetailStorePOS.Data.Modules.Products;
using global::RetailStorePOS.Data.Modules.Sales;
using RetailStorePOS.UI.Common;
using SkiaSharp;
using global::RetailStorePOS.Data.Modules.Reporting;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Xaml.Media.Animation;
using RetailStorePOS.UI.Common;


namespace RetailStorePOS.UI.Reporting.Views;

// --- Data models ---
public sealed class DashboardProductItem
{
    public string Name { get; set; } = string.Empty;
    public string Revenue { get; set; } = string.Empty;
    public double Pct { get; set; }
}

public sealed class DashboardStockItem
{
    public string Name { get; set; } = string.Empty;
    public string StockStatus { get; set; } = string.Empty;
    public SolidColorBrush StatusBrush { get; set; } = new(Color.FromArgb(255, 143, 150, 163));
    public string PrimaryLabelText { get; set; } = string.Empty;
    public string PrimaryStatusText { get; set; } = string.Empty;
    public SolidColorBrush PrimaryStatusBrush { get; set; } = new(Color.FromArgb(255, 143, 150, 163));
    public string SecondaryLabelText { get; set; } = string.Empty;
    public string SecondaryStatusText { get; set; } = string.Empty;
    public SolidColorBrush SecondaryStatusBrush { get; set; } = new(Color.FromArgb(255, 143, 150, 163));
    public Visibility SimpleStatusVisibility { get; set; } = Visibility.Visible;
    public Visibility DetailedStatusVisibility { get; set; } = Visibility.Collapsed;
    public string BadgeText { get; set; } = string.Empty;
    public Visibility SimpleBadgeTextVisibility { get; set; } = Visibility.Visible;
    public Visibility BadgeVisible { get; set; } = Visibility.Visible;
}

public sealed class DashboardAlertItem
{
    public string Name { get; set; } = string.Empty;
    public string CountText { get; set; } = string.Empty;
    public SolidColorBrush BadgeBackground { get; set; } = new(Color.FromArgb(255, 245, 247, 250));
    public SolidColorBrush BadgeForeground { get; set; } = new(Color.FromArgb(255, 95, 103, 117));
}

internal enum DashboardDateRangePreset
{
    Today,
    Last7Days,
    Last30Days,
    Custom
}

internal sealed class DashboardDateRangeSelection
{
    private DashboardDateRangeSelection(DashboardDateRangePreset preset, DateTime startLocalDate, DateTime endLocalDate)
    {
        Preset = preset;
        StartLocalDate = startLocalDate.Date;
        EndLocalDate = endLocalDate.Date;
    }

    public DashboardDateRangePreset Preset { get; }
    public DateTime StartLocalDate { get; }
    public DateTime EndLocalDate { get; }

    public static DashboardDateRangeSelection Today()
        => new(DashboardDateRangePreset.Today, DateTime.Today, DateTime.Today);

    public static DashboardDateRangeSelection LastDays(int days)
    {
        var clampedDays = Math.Max(1, days);
        return new(
            clampedDays == 30 ? DashboardDateRangePreset.Last30Days : DashboardDateRangePreset.Last7Days,
            DateTime.Today.AddDays(-(clampedDays - 1)),
            DateTime.Today);
    }

    public static DashboardDateRangeSelection Custom(DateTime startLocalDate, DateTime endLocalDate)
    {
        var start = startLocalDate.Date;
        var end = endLocalDate.Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }

        return new(DashboardDateRangePreset.Custom, start, end);
    }
}

internal sealed class DashboardReportingPeriod
{
    public required DateTime CurrentStartLocal { get; init; }
    public required DateTime CurrentEndLocalExclusive { get; init; }
    public required DateTime ComparisonStartLocal { get; init; }
    public required DateTime ComparisonEndLocalExclusive { get; init; }
    public required DateTime ComparisonFullEndLocalExclusive { get; init; }
    public required bool IsPaidRange { get; init; }
    public required bool UsesTodayPacingComparison { get; init; }

    public DateTime CurrentStartUtc => CurrentStartLocal.ToUniversalTime();
    public DateTime CurrentEndUtc => CurrentEndLocalExclusive.ToUniversalTime();
    public DateTime ComparisonStartUtc => ComparisonStartLocal.ToUniversalTime();
    public DateTime ComparisonEndUtc => ComparisonEndLocalExclusive.ToUniversalTime();
    public DateTime ComparisonFullEndUtc => ComparisonFullEndLocalExclusive.ToUniversalTime();
    public int DayCount => Math.Max(1, (int)(CurrentEndLocalExclusive.Date - CurrentStartLocal.Date).TotalDays);
}

public sealed partial class ReportsDashboardPage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static readonly WorkflowBoundary ReportingWorkflow = ReportingWorkflowContract.ReportingRefreshBoundary;
    private static SaleRepository ReportingSalesModule => ReportingWorkflowContract.ResolveSalesReadModel(LoginRuntime.Sales);
    private static ProductRepository ReportingInventoryCatalog => ReportingWorkflowContract.ResolveInventoryReadModel(LoginRuntime.Products);
    private readonly DispatcherTimer _skeletonTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private LinearGradientBrush? _skeletonShimmerBrush;
    private bool _isDashboardLoading;
    private bool _hasLoadedDashboard;
    private bool _isRefreshQueued;
    private double _skeletonPhase = -0.35;
    private double _todayBarPct;
    private double _yesterdayBarPct;
    private (double Pct, Color Color)[]? _donutSegments;
    private DispatcherTimer? _donutAnimTimer;
    private DateTime _donutAnimStart;
    private DateTimeOffset _lastRefreshCompletedAt = DateTimeOffset.MinValue;
    private InventorySummary? _lastInventorySummary;
    private DashboardDateRangeSelection _selectedDateRange = DashboardDateRangeSelection.LastDays(7);
    private bool _canUseDashboardDateRange;
    private static readonly TimeSpan DashboardRefreshInterval = TimeSpan.FromMinutes(1);


    private Task EnqueueOnUI(Action action)
    {
        var tcs = new TaskCompletionSource();
        DispatcherQueue.TryEnqueue(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        return tcs.Task;
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public string ReportsDashboard_Subtitle => Loc["ReportsDashboard_Subtitle.Text"];
    public string ReportsDashboard_KPI_SalesToday => _canUseDashboardDateRange ? Loc["ReportsDashboard_KPI_SalesPeriod.Text"] : Loc["ReportsDashboard_KPI_SalesToday.Text"];
    public string ReportsDashboard_KPI_NetProfit => _canUseDashboardDateRange ? Loc["ReportsDashboard_KPI_NetProfitPeriod.Text"] : Loc["ReportsDashboard_KPI_NetProfit.Text"];
    public string ReportsDashboard_KPI_InvoiceCount => Loc["ReportsDashboard_KPI_InvoiceCount.Text"];
    public string ReportsDashboard_KPI_AvgInvoiceValue => Loc["ReportsDashboard_KPI_AvgInvoiceValue.Text"];
    public string ReportsDashboard_MedianLabel => Loc["ReportsDashboard_MedianLabel.Text"];
    public string AvgInvoiceSubLabel => _canUseDashboardDateRange ? Loc["ReportsDashboard_AvgInvoice_ComparisonLabel.Text"] : Loc["ReportsDashboard_MedianLabel.Text"];
    public string ReportsDashboard_Target_Title => Loc["ReportsDashboard_Target_Title.Text"];
    public string ReportsDashboard_Target_TodayLabel => _canUseDashboardDateRange ? Loc["ReportsDashboard_Target_PeriodLabel.Text"] : Loc["ReportsDashboard_Target_TodayLabel.Text"];
    public string ReportsDashboard_Target_GoalLabel => _canUseDashboardDateRange ? Loc["ReportsDashboard_Target_PeriodGoalLabel.Text"] : Loc["ReportsDashboard_Target_GoalLabel.Text"];
    public string ReportsDashboard_Alerts_Title => Loc["ReportsDashboard_Alerts_Title.Text"];
    public string ReportsDashboard_TopProducts_Title => Loc["ReportsDashboard_TopProducts_Title.Text"];
    public string ReportsDashboard_StockStatus_Title => Loc["ReportsDashboard_StockStatus_Title.Text"];
    public string ReportsDashboard_Discounts_Title => _canUseDashboardDateRange ? Loc["ReportsDashboard_Discounts_PeriodTitle.Text"] : Loc["ReportsDashboard_Discounts_Title.Text"];
    public string ReportsDashboard_StockHealth_Title => Loc["ReportsDashboard_StockHealth_Title.Text"];
    public string ReportsDashboard_TeachingTip_Title => Loc["ReportsDashboard_TeachingTip_Title.Title"];
    public string Generic_Close => Loc["Generic_Close"];


    private string _greetingValue = string.Empty;
    public string GreetingValue { get => _greetingValue; set { _greetingValue = value; OnPropertyChanged(nameof(GreetingValue)); } }

    private string _todayDateValue = string.Empty;
    public string TodayDateValue { get => _todayDateValue; set { _todayDateValue = value; OnPropertyChanged(nameof(TodayDateValue)); } }

    private string _salesTodayValue = string.Empty;
    public string SalesTodayValue { get => _salesTodayValue; set { _salesTodayValue = value; OnPropertyChanged(nameof(SalesTodayValue)); } }

    private string _salesDeltaValue = string.Empty;
    public string SalesDeltaValue { get => _salesDeltaValue; set { _salesDeltaValue = value; OnPropertyChanged(nameof(SalesDeltaValue)); } }

    private string _netProfitValue = string.Empty;
    public string NetProfitValue { get => _netProfitValue; set { _netProfitValue = value; OnPropertyChanged(nameof(NetProfitValue)); } }

    private string _profitDeltaValue = string.Empty;
    public string ProfitDeltaValue { get => _profitDeltaValue; set { _profitDeltaValue = value; OnPropertyChanged(nameof(ProfitDeltaValue)); } }

    private string _invoiceCountValue = string.Empty;
    public string InvoiceCountValue { get => _invoiceCountValue; set { _invoiceCountValue = value; OnPropertyChanged(nameof(InvoiceCountValue)); } }

    private string _invoiceDeltaValue = string.Empty;
    public string InvoiceDeltaValue { get => _invoiceDeltaValue; set { _invoiceDeltaValue = value; OnPropertyChanged(nameof(InvoiceDeltaValue)); } }

    private string _avgInvoiceValue = string.Empty;
    public string AvgInvoiceValue { get => _avgInvoiceValue; set { _avgInvoiceValue = value; OnPropertyChanged(nameof(AvgInvoiceValue)); } }

    private string _medianValue = string.Empty;
    public string MedianValue { get => _medianValue; set { _medianValue = value; OnPropertyChanged(nameof(MedianValue)); } }

    private string _salesAchievedPctValue = string.Empty;
    public string SalesAchievedPctValue { get => _salesAchievedPctValue; set { _salesAchievedPctValue = value; OnPropertyChanged(nameof(SalesAchievedPctValue)); } }

    private string _currentSalesValue = string.Empty;
    public string CurrentSalesValue { get => _currentSalesValue; set { _currentSalesValue = value; OnPropertyChanged(nameof(CurrentSalesValue)); } }

    private string _targetValueText = string.Empty;
    public string TargetValueText { get => _targetValueText; set { _targetValueText = value; OnPropertyChanged(nameof(TargetValueText)); } }

    private string _targetSubValue = string.Empty;
    public string TargetSubValue { get => _targetSubValue; set { _targetSubValue = value; OnPropertyChanged(nameof(TargetSubValue)); } }

    private string _healthyLegendValue = "0% Healthy";
    public string HealthyLegendValue { get => _healthyLegendValue; set { _healthyLegendValue = value; OnPropertyChanged(nameof(HealthyLegendValue)); } }

    private string _lowStockLegendValue = "0% Low Stock";
    public string LowStockLegendValue { get => _lowStockLegendValue; set { _lowStockLegendValue = value; OnPropertyChanged(nameof(LowStockLegendValue)); } }

    private string _outOfStockLegendValue = "0% Out of Stock";
    public string OutOfStockLegendValue { get => _outOfStockLegendValue; set { _outOfStockLegendValue = value; OnPropertyChanged(nameof(OutOfStockLegendValue)); } }

    private string _donutCenterValue = "0%";
    public string DonutCenterValue { get => _donutCenterValue; set { _donutCenterValue = value; OnPropertyChanged(nameof(DonutCenterValue)); } }

    public ReportsDashboardPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        this.InitializeComponent();
        _ = ReportingWorkflow;
        _skeletonShimmerBrush = Resources["SkeletonShimmerBrush"] as LinearGradientBrush;
        _skeletonTimer.Tick += SkeletonTimer_Tick;
        SalesBarContainer.SizeChanged += SalesBarContainer_SizeChanged;
        Loc.PropertyChanged += (s, e) => {
            RefreshLocalizedProperties();
            Bindings.Update();
            if (_donutSegments != null) {
                // Redraw chart immediately on language change to ensure proper rendering in new FlowDirection
                DrawDonutChart(StockHealthCanvas, DonutCenterText, _donutSegments, 140, 56, 24);
            }
        };

        // Subscribe ONCE for the page's lifetime. Loaded/Unloaded fires on every
        // navigation in/out, but NavigationCacheMode=Required keeps this instance
        // alive across the whole session. If we subscribed in Loaded and the user
        // navigated to Settings to change the daily target, we would Unsubscribe
        // before the SavePreferences event fires - and miss it.
        if (LoginRuntime.LocalPreferences is { } prefs)
        {
            prefs.PreferencesChanged += OnLocalPreferencesChanged;
        }

        // Same lifetime rationale: re-evaluate paid-feature visibility when the
        // license snapshot or developer override changes, so toggling a license
        // in Settings updates this page without a restart.
        if (LoginRuntime.FeatureAccess is { } featureAccess)
        {
            featureAccess.FeatureAccessChanged += OnFeatureAccessChanged;
        }

        Loaded += ReportsDashboardPage_Loaded;
        Unloaded += ReportsDashboardPage_Unloaded;
    }

    private void RefreshLocalizedProperties()
    {
        GreetingValue = GetGreeting();
        TodayDateValue = GetDatePillText();
        
        // Re-run inventory status formatting to pick up new language strings
        if (_lastInventorySummary != null)
        {
            ApplyInventorySummaryToUI(_lastInventorySummary);
        }

        OnPropertyChanged(nameof(GreetingValue));
        OnPropertyChanged(nameof(TodayDateValue));
        OnPropertyChanged(nameof(ReportsDashboard_Subtitle));
        OnPropertyChanged(nameof(ReportsDashboard_KPI_SalesToday));
        OnPropertyChanged(nameof(ReportsDashboard_KPI_NetProfit));
        OnPropertyChanged(nameof(ReportsDashboard_KPI_InvoiceCount));
        OnPropertyChanged(nameof(ReportsDashboard_KPI_AvgInvoiceValue));
        OnPropertyChanged(nameof(ReportsDashboard_MedianLabel));
        OnPropertyChanged(nameof(AvgInvoiceSubLabel));
        OnPropertyChanged(nameof(ReportsDashboard_Target_Title));
        OnPropertyChanged(nameof(ReportsDashboard_Target_TodayLabel));
        OnPropertyChanged(nameof(ReportsDashboard_Target_GoalLabel));
        OnPropertyChanged(nameof(ReportsDashboard_Alerts_Title));
        OnPropertyChanged(nameof(ReportsDashboard_TopProducts_Title));
        OnPropertyChanged(nameof(ReportsDashboard_StockStatus_Title));
        OnPropertyChanged(nameof(ReportsDashboard_Discounts_Title));
        OnPropertyChanged(nameof(ReportsDashboard_StockHealth_Title));
        OnPropertyChanged(nameof(ReportsDashboard_TeachingTip_Title));
        OnPropertyChanged(nameof(Generic_Close));
        
        // Explicitly update legends as they are not properties themselves but used in bindings
        OnPropertyChanged(nameof(HealthyLegendValue));
        OnPropertyChanged(nameof(LowStockLegendValue));
        OnPropertyChanged(nameof(OutOfStockLegendValue));
    }

    public ISeries[] SalesSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 0, 7, 4, 12, 8, 20, 15], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(25, 118, 210)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(25, 118, 210, 40)), GeometrySize = 0 }
    ];

    public ISeries[] ProfitSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 0, 7, 4, 12, 8, 20, 15], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(67, 160, 71)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(67, 160, 71, 40)), GeometrySize = 0 }
    ];

    public ISeries[] InvoiceSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 0, 7, 4, 12, 8, 20, 15], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(25, 118, 210)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(25, 118, 210, 40)), GeometrySize = 0 }
    ];

    public ISeries[] AvgInvoiceSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 0, 7, 4, 12, 8, 20, 15], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(249, 168, 37)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(249, 168, 37, 40)), GeometrySize = 0 }
    ];

    public IEnumerable<ICartesianAxis> HiddenXAxes { get; set; } = new ICartesianAxis[] { new Axis { IsVisible = false } };
    public IEnumerable<ICartesianAxis> HiddenYAxes { get; set; } = new ICartesianAxis[] { new Axis { IsVisible = false } };

    private void ReportsDashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyFeatureGating();
        QueueRefreshAfterNavigation(forceInitialLoad: !_hasLoadedDashboard);
    }

    private void OnFeatureAccessChanged(object? sender, System.EventArgs e)
    {
        // Re-evaluate paid-feature visibility on the UI thread; the event may
        // fire from a background path when a license snapshot is refreshed.
        DispatcherQueue.TryEnqueue(ApplyFeatureGating);
    }

    private void ApplyFeatureGating()
    {
        // The date pill remains visible for everyone. Paid users can open it
        // as a period selector; free users keep the original static today pill.
        var previousAccess = _canUseDashboardDateRange;
        _canUseDashboardDateRange = LoginRuntime.FeatureAccess?.CanUse(FeatureAccessService.Features.DashboardDatePill) ?? false;

        if (TodayDatePill is not null)
        {
            TodayDatePill.Visibility = Visibility.Visible;
            TodayDatePill.Opacity = _canUseDashboardDateRange ? 1.0 : 0.92;
        }

        if (DatePillChevron is not null)
        {
            DatePillChevron.Visibility = _canUseDashboardDateRange ? Visibility.Visible : Visibility.Collapsed;
        }

        RefreshLocalizedProperties();

        if (previousAccess != _canUseDashboardDateRange && _hasLoadedDashboard)
        {
            _ = RefreshAsync();
        }
    }

    private void OnLocalPreferencesChanged(object? sender, LocalPreferencesChangedEventArgs e)
    {
        // Daily target / quick-cash / theme changes should reflow the dashboard
        // without an app restart. Marshal to UI thread; the refresh itself
        // dispatches its DB work to a background task.
        DispatcherQueue.TryEnqueue(() =>
        {
            _ = RefreshAsync();
        });
    }

    public async Task RefreshAsync(bool isInitialLoad = false)
    {
        if (_isDashboardLoading) return;
        _isDashboardLoading = true;

        try
        {
            if (isInitialLoad && !_hasLoadedDashboard)
            {
                SetDashboardLoadingState(true);
            }

            await PopulateDashboardAsync();
            _hasLoadedDashboard = true;
            _lastRefreshCompletedAt = DateTimeOffset.UtcNow;

            // Refresh succeeded — clear stale tag and persist a fresh snapshot
            // so a future open-cycle that fails can fall back to these values.
            DispatcherQueue.TryEnqueue(() => StaleTagVisibility = Visibility.Collapsed);
            _ = Task.Run(TrySaveDashboardSnapshot);
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"Reports dashboard refresh failed: {ex}");

            // Live refresh failed. If a previous snapshot exists, surface a
            // stale indicator so the user knows they are looking at cached
            // values; otherwise just reveal the (possibly empty) cards.
            DispatcherQueue.TryEnqueue(() =>
            {
                RevealContentContainers();
                if (HasUsableDashboardSnapshot())
                {
                    StaleTagVisibility = Visibility.Visible;
                }
            });
        }
        finally
        {
            _isDashboardLoading = false;
            SetDashboardLoadingState(false);
        }
    }

    // -----------------------------------------------------------------
    // Dashboard snapshot freshness (spec 008 US3, FR-016)
    // -----------------------------------------------------------------
    private static readonly DashboardSnapshotStore _dashboardSnapshotStore = new();

    private Visibility _staleTagVisibility = Visibility.Collapsed;
    public Visibility StaleTagVisibility
    {
        get => _staleTagVisibility;
        private set
        {
            if (_staleTagVisibility == value) return;
            _staleTagVisibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StaleTagVisibility)));
        }
    }

    /// <summary>
    /// Builds a payload from the most recently rendered dashboard fields and
    /// writes it to the local snapshot file. Off the UI thread; failures are
    /// logged for diagnostics because the live UI is already authoritative.
    /// </summary>
    private void TrySaveDashboardSnapshot()
    {
        try
        {
            var payload = new DashboardMetricsPayload
            {
                Inventory = _lastInventorySummary ?? new InventorySummary()
            };

            var snapshot = new DashboardSnapshot
            {
                SnapshotKey = "dashboard-default",
                CapturedAtUtc = DateTime.UtcNow,
                MetricsPayload = payload,
                IsStale = false
            };
            _dashboardSnapshotStore.SaveSnapshot(snapshot);
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"Reports dashboard snapshot save failed: {ex}");
        }
    }

    private static bool HasUsableDashboardSnapshot()
    {
        try
        {
            return _dashboardSnapshotStore.LoadSnapshot() is not null;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"Reports dashboard snapshot load failed: {ex}");
            return false;
        }
    }

    private string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        return hour < 12 ? LocalizationHelper.GetString("ReportsDashboard_Greeting_Morning")
            : hour < 17 ? LocalizationHelper.GetString("ReportsDashboard_Greeting_Afternoon")
            : LocalizationHelper.GetString("ReportsDashboard_Greeting_Evening");
    }

    private void UpdateDeltaText(TextBlock textBlock, decimal current, decimal previous, bool isCurrency = true)
    {
        double delta = previous > 0 ? (double)((current - previous) / previous) * 100.0 : (current > 0 ? 100.0 : 0);
        var valStr = CurrencyDisplayHelper.FormatNumber((decimal)Math.Abs(delta), "0.#");
        textBlock.Text = delta >= 0 
            ? string.Format(LocalizationHelper.GetString(isCurrency ? "ReportsDashboard_Sales_Delta_Positive" : "ReportsDashboard_Invoice_Delta_Positive"), valStr)
            : string.Format(LocalizationHelper.GetString(isCurrency ? "ReportsDashboard_Sales_Delta_Negative" : "ReportsDashboard_Invoice_Delta_Negative"), valStr);
        textBlock.Foreground = new SolidColorBrush(delta >= 0 ? Color.FromArgb(255, 46, 125, 50) : Color.FromArgb(255, 198, 40, 40));
    }

    private static string GetDeltaPositiveKey(DashboardReportingPeriod period)
        => period.IsPaidRange && !period.UsesTodayPacingComparison
            ? "ReportsDashboard_Period_Delta_Positive"
            : "ReportsDashboard_Sales_Delta_Positive";

    private static string GetDeltaNegativeKey(DashboardReportingPeriod period)
        => period.IsPaidRange && !period.UsesTodayPacingComparison
            ? "ReportsDashboard_Period_Delta_Negative"
            : "ReportsDashboard_Sales_Delta_Negative";

    private void ApplyPercentDelta(
        TextBlock textBlock,
        decimal current,
        decimal previous,
        string positiveKey,
        string negativeKey,
        Action<string> assignText)
    {
        var delta = previous > 0
            ? (double)((current - previous) / previous) * 100.0
            : (current > 0 ? 100.0 : 0.0);
        var value = CurrencyDisplayHelper.FormatNumber((decimal)Math.Abs(delta), "0.#");

        assignText(string.Format(
            LocalizationHelper.GetString(delta >= 0 ? positiveKey : negativeKey),
            value));
        textBlock.Foreground = new SolidColorBrush(delta >= 0
            ? Color.FromArgb(255, 46, 125, 50)
            : Color.FromArgb(255, 198, 40, 40));
    }

    private void RevealContentContainers()
    {
        SalesTodaySkeleton.Visibility = Visibility.Collapsed; SalesTodayContent.Visibility = Visibility.Visible;
        NetProfitSkeleton.Visibility = Visibility.Collapsed; NetProfitContent.Visibility = Visibility.Visible;
        InvoiceCountSkeleton.Visibility = Visibility.Collapsed; InvoiceCountContent.Visibility = Visibility.Visible;
        AvgInvoiceSkeleton.Visibility = Visibility.Collapsed; AvgInvoiceContent.Visibility = Visibility.Visible;
        SalesTargetSkeleton.Visibility = Visibility.Collapsed; SalesTargetContent.Visibility = Visibility.Visible;
        TopProdSkeleton.Visibility = Visibility.Collapsed; TopProdContent.Visibility = Visibility.Visible;
        DiscountsSkeleton.Visibility = Visibility.Collapsed; DiscountsContent.Visibility = Visibility.Visible;
        CrAlertsSkeleton.Visibility = Visibility.Collapsed; CrAlertsContent.Visibility = Visibility.Visible;
        LowStockSkeleton.Visibility = Visibility.Collapsed; LowStockContent.Visibility = Visibility.Visible;
        StockHealthSkeleton.Visibility = Visibility.Collapsed; StockHealthContent.Visibility = Visibility.Visible;
        StopSkeletonShimmer();
    }

    private void ApplyInventorySummaryToUI(InventorySummary inv)
    {
        _lastInventorySummary = inv;
        var alertGreenForeground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));

        var alertRedBackground = new SolidColorBrush(Color.FromArgb(255, 252, 232, 230));
        var alertRedForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
        var alertAmberBackground = new SolidColorBrush(Color.FromArgb(255, 255, 244, 229));
        var alertAmberForeground = new SolidColorBrush(Color.FromArgb(255, 239, 108, 0));

        DashboardAlertItem CreateAlertItem(string n, int c, SolidColorBrush aB, SolidColorBrush aF)
        {
            var isHealthy = c == 0;
            return new DashboardAlertItem { 
                Name = n, 
                CountText = CurrencyDisplayHelper.FormatInt(c), 
                BadgeBackground = isHealthy ? new SolidColorBrush(Color.FromArgb(255, 232, 245, 233)) : aB, 
                BadgeForeground = isHealthy ? alertGreenForeground : aF 
            };
        }

        var alerts = new[]
        {
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Shelf"), LocalizationHelper.GetString("ReportsDashboard_Stock_LowStock")), inv.ShelfLow, alertAmberBackground, alertAmberForeground),
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Warehouse"), LocalizationHelper.GetString("ReportsDashboard_Stock_LowStock")), inv.WarehouseLow, alertAmberBackground, alertAmberForeground),
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Shelf"), LocalizationHelper.GetString("ReportsDashboard_Stock_Empty")), inv.ShelfEmpty, alertRedBackground, alertRedForeground),
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Warehouse"), LocalizationHelper.GetString("ReportsDashboard_Stock_Empty")), inv.WarehouseEmpty, alertRedBackground, alertRedForeground)
        };
        CriticalAlertsList.Items.Clear();
        foreach (var a in alerts) CriticalAlertsList.Items.Add(a);

        if (inv.TotalProducts > 0)
        {
            int healthyCnt = inv.TotalProducts - (inv.ShelfLow + inv.WarehouseLow + inv.ShelfEmpty + inv.WarehouseEmpty);
            if (healthyCnt < 0) healthyCnt = 0;
            
            double pctHealthy = (double)healthyCnt / inv.TotalProducts;
            double pctLow = (double)(inv.ShelfLow + inv.WarehouseLow) / inv.TotalProducts;
            double pctOut = (double)(inv.ShelfEmpty + inv.WarehouseEmpty) / inv.TotalProducts;

            HealthyLegendValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_StockHealth_HealthyFormat"), CurrencyDisplayHelper.FormatNumber((decimal)(pctHealthy * 100), "0"));
            LowStockLegendValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_StockHealth_LowStockFormat"), CurrencyDisplayHelper.FormatNumber((decimal)(pctLow * 100), "0"));
            OutOfStockLegendValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_StockHealth_OutOfStockFormat"), CurrencyDisplayHelper.FormatNumber((decimal)(pctOut * 100), "0"));

            var colorHealthy = Color.FromArgb(255, 25, 118, 210);
            var colorLow = Color.FromArgb(255, 249, 168, 37);
            var colorOut = Color.FromArgb(255, 229, 57, 53);

            _donutSegments = new[] { (pctHealthy, colorHealthy), (pctLow, colorLow), (pctOut, colorOut) };
            DonutCenterValue = CurrencyDisplayHelper.FormatPercent(pctHealthy * 100, "0");
        }
    }

    private void ReportsDashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        StopSkeletonShimmer();
    }

    private void TodayDatePill_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (!_canUseDashboardDateRange)
        {
            return;
        }

        var flyout = new MenuFlyout();
        AddDateRangeFlyoutItem(flyout, LocalizationHelper.GetString("ReportsDashboard_DateRange_Today"), DashboardDateRangeSelection.Today());
        AddDateRangeFlyoutItem(flyout, LocalizationHelper.GetString("ReportsDashboard_DateRange_Last7Days"), DashboardDateRangeSelection.LastDays(7));
        AddDateRangeFlyoutItem(flyout, LocalizationHelper.GetString("ReportsDashboard_DateRange_Last30Days"), DashboardDateRangeSelection.LastDays(30));

        var customItem = new MenuFlyoutItem
        {
            Text = LocalizationHelper.GetString("ReportsDashboard_DateRange_Custom")
        };
        customItem.Click += CustomDateRangeMenuItem_Click;
        flyout.Items.Add(customItem);

        flyout.ShowAt(TodayDatePill);
    }

    private void AddDateRangeFlyoutItem(MenuFlyout flyout, string text, DashboardDateRangeSelection selection)
    {
        var item = new MenuFlyoutItem { Text = text };
        item.Click += async (_, _) => await SelectDateRangeAsync(selection);
        flyout.Items.Add(item);
    }

    private async void CustomDateRangeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await ShowCustomDateRangeDialogAsync();
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"Reports dashboard custom date dialog failed: {ex}");
            LoginRuntime.ReportException(ex, "ReportsDashboard.CustomDateRangeDialog");
        }
    }

    private async Task ShowCustomDateRangeDialogAsync()
    {
        var startPicker = new CalendarDatePicker
        {
            Header = LocalizationHelper.GetString("ReportsDashboard_DateRange_From"),
            Date = new DateTimeOffset(_selectedDateRange.StartLocalDate),
            MaxDate = new DateTimeOffset(DateTime.Today),
            MinWidth = 240
        };
        var endPicker = new CalendarDatePicker
        {
            Header = LocalizationHelper.GetString("ReportsDashboard_DateRange_To"),
            Date = new DateTimeOffset(_selectedDateRange.EndLocalDate),
            MaxDate = new DateTimeOffset(DateTime.Today),
            MinWidth = 240
        };

        var panel = new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = LocalizationHelper.GetString("ReportsDashboard_DateRange_CustomDescription"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 95, 103, 117))
                },
                startPicker,
                endPicker
            }
        };

        var dialog = new ContentDialog
        {
            Title = LocalizationHelper.GetString("ReportsDashboard_DateRange_CustomTitle"),
            Content = panel,
            PrimaryButtonText = LocalizationHelper.GetString("ReportsDashboard_DateRange_Apply"),
            CloseButtonText = LocalizationHelper.GetString("Generic_Close"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary || startPicker.Date is null || endPicker.Date is null)
        {
            return;
        }

        await SelectDateRangeAsync(DashboardDateRangeSelection.Custom(startPicker.Date.Value.Date, endPicker.Date.Value.Date));
    }

    private async Task SelectDateRangeAsync(DashboardDateRangeSelection selection)
    {
        _selectedDateRange = selection;
        TodayDateValue = GetDatePillText();
        await RefreshAsync();
    }

    private string GetDatePillText()
    {
        if (!_canUseDashboardDateRange)
        {
            return CurrencyDisplayHelper.FormatDate(DateTime.Now);
        }

        return _selectedDateRange.Preset switch
        {
            DashboardDateRangePreset.Today => LocalizationHelper.GetString("ReportsDashboard_DateRange_Today"),
            DashboardDateRangePreset.Last7Days => LocalizationHelper.GetString("ReportsDashboard_DateRange_Last7Days"),
            DashboardDateRangePreset.Last30Days => LocalizationHelper.GetString("ReportsDashboard_DateRange_Last30Days"),
            _ when _selectedDateRange.StartLocalDate == _selectedDateRange.EndLocalDate
                => CurrencyDisplayHelper.FormatDate(_selectedDateRange.StartLocalDate),
            _ => string.Format(
                LocalizationHelper.GetString("ReportsDashboard_DateRange_CustomFormat"),
                CurrencyDisplayHelper.FormatDate(_selectedDateRange.StartLocalDate),
                CurrencyDisplayHelper.FormatDate(_selectedDateRange.EndLocalDate))
        };
    }

    private DashboardReportingPeriod GetActiveReportingPeriod()
    {
        if (!_canUseDashboardDateRange)
        {
            return CreateTodayReportingPeriod(isPaidRange: false);
        }

        var today = DateTime.Today;
        var start = _selectedDateRange.StartLocalDate.Date;
        var endInclusive = _selectedDateRange.EndLocalDate.Date;
        var endExclusive = endInclusive.AddDays(1);

        if (start == today && endInclusive == today)
        {
            return CreateTodayReportingPeriod(isPaidRange: true);
        }

        var days = Math.Max(1, (int)(endExclusive - start).TotalDays);
        var comparisonStart = start.AddDays(-days);

        return new DashboardReportingPeriod
        {
            CurrentStartLocal = start,
            CurrentEndLocalExclusive = endExclusive,
            ComparisonStartLocal = comparisonStart,
            ComparisonEndLocalExclusive = start,
            ComparisonFullEndLocalExclusive = start,
            IsPaidRange = true,
            UsesTodayPacingComparison = false
        };
    }

    private static DashboardReportingPeriod CreateTodayReportingPeriod(bool isPaidRange)
    {
        var today = DateTime.Today;
        var yesterday = today.AddDays(-1);
        var nowOffset = DateTime.Now.TimeOfDay;

        return new DashboardReportingPeriod
        {
            CurrentStartLocal = today,
            CurrentEndLocalExclusive = today.AddDays(1),
            ComparisonStartLocal = yesterday,
            ComparisonEndLocalExclusive = yesterday.Add(nowOffset),
            ComparisonFullEndLocalExclusive = today,
            IsPaidRange = isPaidRange,
            UsesTodayPacingComparison = true
        };
    }


    private async Task PopulateDashboardAsync()
    {
        RefreshLocalizedProperties();

        var period = GetActiveReportingPeriod();

        // Reveal the main Grid instantly so skeletons appear
        DashboardScrollViewer.Visibility = Visibility.Visible;

        // Perform the heavy DB queries completely off the main thread, exactly once.
        var metricsData = await Task.Run(() =>
        {
            return new
            {
                Current = ReportingSalesModule.GetDashboardMetrics(period.CurrentStartUtc, period.CurrentEndUtc),
                Comparison = ReportingSalesModule.GetDashboardMetrics(period.ComparisonStartUtc, period.ComparisonEndUtc),
                ComparisonFull = ReportingSalesModule.GetDashboardMetrics(period.ComparisonStartUtc, period.ComparisonFullEndUtc)
            };
        });

        // Fire all remaining data fetches in parallel
        await Task.WhenAll(
            LoadSalesCardAsync(metricsData.Current, metricsData.Comparison, period),
            LoadProfitCardAsync(metricsData.Current, metricsData.Comparison, period),
            LoadInvoiceCountCardAsync(metricsData.Current, metricsData.Comparison, period),
            LoadAvgInvoiceCardAsync(metricsData.Current, metricsData.Comparison, period),
            LoadSalesTargetCardAsync(metricsData.Current, metricsData.ComparisonFull, period),
            LoadDiscountsCardAsync(metricsData.Current, metricsData.ComparisonFull, period),
            LoadTopProductsCardAsync(period),
            LoadInventoryCardsAsync(),
            LoadSparklinesAsync(period)
        );

        // All data is loaded – now reveal everything and animate
        DispatcherQueue.TryEnqueue(RevealAndAnimateDashboard);
    }

    private async void RevealAndAnimateDashboard()
    {
        try
        {
            // Reveal all cards at once
            RevealContentContainers();

            // Wait one frame for WinUI to measure & layout
            await Task.Delay(50);

            // Animate Sales Target bars
            UpdateSalesBar(_todayBarPct, _yesterdayBarPct);

            // Animate Top Products progress bars
            foreach (var item in TopProductsList.Items)
            {
                var container = TopProductsList.ContainerFromItem(item) as FrameworkElement;
                if (container != null)
                {
                    var progressBar = FindVisualChild<ProgressBar>(container);
                    if (progressBar != null)
                    {
                        var targetValue = progressBar.Value;
                        progressBar.Value = 0;
                        var anim = new DoubleAnimation
                        {
                            From = 0,
                            To = targetValue,
                            Duration = new Duration(TimeSpan.FromSeconds(3)),
                            EnableDependentAnimation = true
                        };
                        var sb = new Storyboard();
                        sb.Children.Add(anim);
                        Storyboard.SetTarget(anim, progressBar);
                        Storyboard.SetTargetProperty(anim, "Value");
                        sb.Begin();
                    }
                }
            }

            // Animate Stock Health donut
            if (_donutSegments != null)
            {
                if (_donutAnimTimer != null)
                {
                    _donutAnimTimer.Stop();
                }
                _donutAnimStart = DateTime.UtcNow;
                _donutAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _donutAnimTimer.Tick += DonutAnimTimer_Tick;
                _donutAnimTimer.Start();
            }
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"RevealAndAnimateDashboard failed: {ex}");
        }
    }

    private void QueueRefreshAfterNavigation(bool forceInitialLoad)
    {
        if (!forceInitialLoad &&
            _hasLoadedDashboard &&
            _lastRefreshCompletedAt != DateTimeOffset.MinValue &&
            DateTimeOffset.UtcNow - _lastRefreshCompletedAt < DashboardRefreshInterval)
        {
            return;
        }

        if (_isRefreshQueued)
        {
            return;
        }

        _isRefreshQueued = true;
        DispatcherQueue.TryEnqueue(async () =>
        {
            _isRefreshQueued = false;
            await RefreshAsync(isInitialLoad: forceInitialLoad && !_hasLoadedDashboard);
        });
    }

    private void DonutAnimTimer_Tick(object? sender, object e)
    {
        if (_donutSegments == null) return;

        const double durationMs = 2000.0;
        double elapsed = (DateTime.UtcNow - _donutAnimStart).TotalMilliseconds;
        double progress = Math.Min(1.0, elapsed / durationMs);

        // Ease-out for a smooth deceleration
        double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3);

        // Scale each segment's percentage by the animation progress
        var animatedSegments = _donutSegments
            .Select(s => (Pct: s.Pct * easedProgress, s.Color))
            .ToArray();

        DrawDonutChart(StockHealthCanvas, DonutCenterText, animatedSegments, 140, 56, 24);

        if (progress >= 1.0)
        {
            _donutAnimTimer?.Stop();
            _donutAnimTimer = null;
            // Final clean draw at exact values
            DrawDonutChart(StockHealthCanvas, DonutCenterText, _donutSegments, 140, 56, 24);
        }
    }

    private async Task LoadSalesCardAsync(DashboardMetrics currentMetrics, DashboardMetrics comparisonMetrics, DashboardReportingPeriod period)
    {
        await EnqueueOnUI(() =>
        {
            var currentTotal = currentMetrics.Revenue;
            var comparisonTotal = comparisonMetrics.Revenue;
            SalesTodayValue = CurrencyDisplayHelper.FormatConfiguredAmount(currentTotal);

            double salesDelta = comparisonTotal > 0
                ? (double)((currentTotal - comparisonTotal) / comparisonTotal) * 100.0
                : (currentTotal > 0 ? 100.0 : 0);

            if (salesDelta >= 0)
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)salesDelta, "0.#");
                SalesDeltaValue = string.Format(LocalizationHelper.GetString(GetDeltaPositiveKey(period)), valStr);
                SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)Math.Abs(salesDelta), "0.#");
                SalesDeltaValue = string.Format(LocalizationHelper.GetString(GetDeltaNegativeKey(period)), valStr);
                SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            }
        });
    }

    private async Task LoadProfitCardAsync(DashboardMetrics currentMetrics, DashboardMetrics comparisonMetrics, DashboardReportingPeriod period)
    {
        await EnqueueOnUI(() =>
        {
            var currentTotal = currentMetrics.Revenue;
            var currentProfit = currentMetrics.Profit;
            NetProfitValue = CurrencyDisplayHelper.FormatConfiguredAmount(currentProfit);

            if (period.IsPaidRange)
            {
                ApplyPercentDelta(ProfitDeltaText, currentProfit, comparisonMetrics.Profit, GetDeltaPositiveKey(period), GetDeltaNegativeKey(period), value => ProfitDeltaValue = value);
                return;
            }

            if (currentTotal > 0)
            {
                var marginPct = (double)(currentProfit / currentTotal) * 100.0;
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)marginPct, "0.#");
                ProfitDeltaValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_Profit_MarginFormat"), valStr);
                ProfitDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                ProfitDeltaValue = LocalizationHelper.GetString("ReportsDashboard_Profit_NoSales");
                ProfitDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163));
            }
        });
    }

    private async Task LoadInvoiceCountCardAsync(DashboardMetrics currentMetrics, DashboardMetrics comparisonMetrics, DashboardReportingPeriod period)
    {
        await EnqueueOnUI(() =>
        {
            var currentInvoiceCount = currentMetrics.InvoiceCount;
            var comparisonInvoiceCount = comparisonMetrics.InvoiceCount;
            InvoiceCountValue = CurrencyDisplayHelper.FormatInt(currentInvoiceCount);

            double invoiceDelta = comparisonInvoiceCount > 0
                ? ((double)(currentInvoiceCount - comparisonInvoiceCount) / comparisonInvoiceCount) * 100.0
                : (currentInvoiceCount > 0 ? 100.0 : 0);

            if (invoiceDelta >= 0)
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)invoiceDelta, "0.#");
                InvoiceDeltaValue = string.Format(LocalizationHelper.GetString(period.IsPaidRange ? GetDeltaPositiveKey(period) : "ReportsDashboard_Invoice_Delta_Positive"), valStr);
                InvoiceDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)Math.Abs(invoiceDelta), "0.#");
                InvoiceDeltaValue = string.Format(LocalizationHelper.GetString(period.IsPaidRange ? GetDeltaNegativeKey(period) : "ReportsDashboard_Invoice_Delta_Negative"), valStr);
                InvoiceDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            }
        });
    }

    private async Task LoadAvgInvoiceCardAsync(DashboardMetrics currentMetrics, DashboardMetrics comparisonMetrics, DashboardReportingPeriod period)
    {
        await EnqueueOnUI(() =>
        {
            var avgInvoice = currentMetrics.InvoiceCount > 0 ? currentMetrics.Revenue / currentMetrics.InvoiceCount : 0m;
            AvgInvoiceValue = CurrencyDisplayHelper.FormatConfiguredAmount(avgInvoice);

            if (period.IsPaidRange)
            {
                var comparisonAverage = comparisonMetrics.InvoiceCount > 0 ? comparisonMetrics.Revenue / comparisonMetrics.InvoiceCount : 0m;
                ApplyPercentDelta(MedianText, avgInvoice, comparisonAverage, GetDeltaPositiveKey(period), GetDeltaNegativeKey(period), value => MedianValue = value);
            }
            else if (currentMetrics.InvoiceCount > 0)
            {
                MedianValue = CurrencyDisplayHelper.FormatConfiguredAmount(currentMetrics.MedianInvoice);
                MedianText.Foreground = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163));
            }
            else
            {
                MedianValue = string.Empty;
                MedianText.Foreground = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163));
            }
        });
    }

    private async Task LoadSalesTargetCardAsync(DashboardMetrics currentMetrics, DashboardMetrics comparisonMetrics, DashboardReportingPeriod period)
    {
        var prefs = await LoginRuntime.LocalPreferences.LoadPreferencesAsync();
        var targetAmount = period.IsPaidRange
            ? prefs.DailyTarget * period.DayCount
            : prefs.DailyTarget;

        await EnqueueOnUI(() =>
        {
            var currentTotal = currentMetrics.Revenue;
            var comparisonTotal = comparisonMetrics.Revenue;

            var achievedPct = targetAmount > 0 ? (double)(currentTotal / targetAmount) * 100.0 : 0;
            
            CurrentSalesValue = CurrencyDisplayHelper.FormatConfiguredAmount(currentTotal);
            TargetValueText = CurrencyDisplayHelper.FormatConfiguredAmount(targetAmount);
            SalesAchievedPctValue = CurrencyDisplayHelper.FormatPercent(achievedPct, "0");
            
            TargetSubValue = string.Format(
                LocalizationHelper.GetString(period.IsPaidRange ? "ReportsDashboard_Target_PreviousPeriodPerformance" : "ReportsDashboard_Target_YesterdayPerformance"),
                CurrencyDisplayHelper.FormatConfiguredAmount(comparisonTotal));

            _todayBarPct = targetAmount > 0 ? Math.Min(1.0, Math.Max(0, (double)(currentTotal / targetAmount))) : 0;
            _yesterdayBarPct = targetAmount > 0 ? Math.Min(1.0, Math.Max(0, (double)(comparisonTotal / targetAmount))) : 0;
        });
    }

    private void SalesBarContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_salesBarAnimated) return;
        double w = SalesBarContainer.ActualWidth;
        if (w <= 0) return;
        YesterdayBar.Width = w * _yesterdayBarPct;
        TodayBar.Width = w * _todayBarPct;
    }

    private bool _salesBarAnimated;
    private DispatcherTimer? _salesBarAnimTimer;
    private DateTime _salesBarAnimStart;

    private void UpdateSalesBar(double todayPct, double yesterdayPct)
    {
        double containerWidth = SalesBarContainer.ActualWidth;
        if (containerWidth <= 0) return;

        _salesBarAnimated = true;

        if (_salesBarAnimTimer != null)
        {
            _salesBarAnimTimer.Stop();
        }

        // Reset bars to 0
        YesterdayBar.Width = 0;
        TodayBar.Width = 0;

        _salesBarAnimStart = DateTime.UtcNow;
        _salesBarAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _salesBarAnimTimer.Tick += (s, e) =>
        {
            double w = SalesBarContainer.ActualWidth;
            if (w <= 0) return;

            double elapsed = (DateTime.UtcNow - _salesBarAnimStart).TotalMilliseconds;

            // Phase 1 (0-2s): Yesterday bar animates alone
            double yesterdayProgress = Math.Min(1.0, elapsed / 2000.0);
            double yesterdayEased = 1.0 - Math.Pow(1.0 - yesterdayProgress, 3);
            YesterdayBar.Width = w * yesterdayPct * yesterdayEased;

            // Phase 2 (2s-5s): Today bar starts after yesterday finishes
            if (elapsed > 2000.0)
            {
                double todayElapsed = elapsed - 2000.0;
                double todayProgress = Math.Min(1.0, todayElapsed / 3000.0);
                double todayEased = 1.0 - Math.Pow(1.0 - todayProgress, 3);
                TodayBar.Width = w * todayPct * todayEased;
            }

            // Stop when both are done
            if (elapsed >= 5000.0)
            {
                _salesBarAnimTimer?.Stop();
                _salesBarAnimTimer = null;
                YesterdayBar.Width = w * yesterdayPct;
                TodayBar.Width = w * todayPct;
            }
        };
        _salesBarAnimTimer.Start();
    }

    private async Task LoadTopProductsCardAsync(DashboardReportingPeriod period)
    {
        var topProducts = await Task.Run(() => ReportingSalesModule.GetTopProducts(period.CurrentStartUtc, period.CurrentEndUtc, 5));
        await EnqueueOnUI(() =>
        {
            var topProductsTotalRevenue = topProducts.Sum(item => item.Revenue);
            var products = topProducts.Count > 0
                    ? topProducts.Select(item => new DashboardProductItem
                    {
                        Name = item.Name,
                        Revenue = CurrencyDisplayHelper.FormatConfiguredAmount(item.Revenue),
                        Pct = topProductsTotalRevenue > 0
                            ? Math.Min(100, Math.Max(0, (double)(item.Revenue / topProductsTotalRevenue) * 100.0))
                            : 0
                    })
                    : new[] { new DashboardProductItem { Name = LocalizationHelper.GetString(period.IsPaidRange ? "ReportsDashboard_TopProducts_None_Period" : "ReportsDashboard_TopProducts_None"), Revenue = CurrencyDisplayHelper.FormatConfiguredAmount(0m), Pct = 0 } };
            TopProductsList.Items.Clear();
            foreach (var p in products) TopProductsList.Items.Add(p);
        });
    }

    private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = VisualTreeHelper.GetChild(obj, i);
            if (child is T t) return t;
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null) return childOfChild;
        }
        return null;
    }

    private async Task LoadDiscountsCardAsync(DashboardMetrics currentMetrics, DashboardMetrics comparisonMetrics, DashboardReportingPeriod period)
    {
        await EnqueueOnUI(() =>
        {
            var currentDiscountAmount = currentMetrics.DiscountAmount;
            var comparisonDiscountAmount = comparisonMetrics.DiscountAmount;

            DiscountsTotalText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(currentDiscountAmount);

            if (period.IsPaidRange)
            {
                ApplyPercentDelta(DiscountsDeltaText, currentDiscountAmount, comparisonDiscountAmount, GetDeltaPositiveKey(period), GetDeltaNegativeKey(period), value => DiscountsDeltaText.Text = value);
            }
            else
            {
                var discountsDeltaPct = comparisonDiscountAmount > 0
                    ? (double)((currentDiscountAmount - comparisonDiscountAmount) / comparisonDiscountAmount) * 100.0
                    : currentDiscountAmount > 0 ? 100.0 : 0.0;
                DiscountsDeltaText.Text = CurrencyDisplayHelper.FormatStringWithDigits(discountsDeltaPct == 0 ? "0%" : $"{discountsDeltaPct:+0.#;-0.#}%");
                DiscountsDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 95, 103, 117));
            }

            DiscountsCountText.Text = string.Format(LocalizationHelper.GetString("ReportsDashboard_Discounts_CountFormat"), CurrencyDisplayHelper.FormatInt(currentMetrics.DiscountCount));
        });
    }

    private static DashboardSparklineBucket GetSparklineBucket(DashboardReportingPeriod period)
    {
        if (period.DayCount <= 1)
        {
            return DashboardSparklineBucket.Hour;
        }

        if (period.DayCount <= 31)
        {
            return DashboardSparklineBucket.Day;
        }

        return period.DayCount > 730
            ? DashboardSparklineBucket.Year
            : DashboardSparklineBucket.Month;
    }

    private static List<string> BuildSparklineBucketKeys(DashboardReportingPeriod period, DashboardSparklineBucket bucket)
    {
        var keys = new List<string>();
        switch (bucket)
        {
            case DashboardSparklineBucket.Hour:
                for (var cursor = period.CurrentStartLocal; cursor < period.CurrentEndLocalExclusive; cursor = cursor.AddHours(1))
                {
                    keys.Add(cursor.ToString("yyyy-MM-dd HH:00"));
                }
                break;

            case DashboardSparklineBucket.Day:
                for (var cursor = period.CurrentStartLocal.Date; cursor < period.CurrentEndLocalExclusive.Date; cursor = cursor.AddDays(1))
                {
                    keys.Add(cursor.ToString("yyyy-MM-dd"));
                }
                break;

            case DashboardSparklineBucket.Month:
                for (var cursor = new DateTime(period.CurrentStartLocal.Year, period.CurrentStartLocal.Month, 1);
                     cursor < period.CurrentEndLocalExclusive;
                     cursor = cursor.AddMonths(1))
                {
                    keys.Add(cursor.ToString("yyyy-MM"));
                }
                break;

            default:
                for (var cursor = new DateTime(period.CurrentStartLocal.Year, 1, 1);
                     cursor < period.CurrentEndLocalExclusive;
                     cursor = cursor.AddYears(1))
                {
                    keys.Add(cursor.ToString("yyyy"));
                }
                break;
        }

        return keys;
    }

    private async Task LoadSparklinesAsync(DashboardReportingPeriod period)
    {
        var bucket = GetSparklineBucket(period);
        var sparklineRows = await Task.Run(() => ReportingSalesModule.GetDashboardSparklines(period.CurrentStartUtc, period.CurrentEndUtc, bucket));
        await EnqueueOnUI(() =>
        {
            var buckets = BuildSparklineBucketKeys(period, bucket);
            var salesSparkData = new double[buckets.Count + 1];
            var profitSparkData = new double[buckets.Count + 1];
            var invoiceSparkData = new double[buckets.Count + 1];
            var avgSparkData = new double[buckets.Count + 1];

            salesSparkData[0] = profitSparkData[0] = invoiceSparkData[0] = avgSparkData[0] = 0;

            var rowByBucket = sparklineRows.ToDictionary(row => row.LocalDay, StringComparer.Ordinal);
            for (var i = 0; i < buckets.Count; i++)
            {
                var idx = i + 1;
                if (rowByBucket.TryGetValue(buckets[i], out var row))
                {
                    salesSparkData[idx] = (double)row.Revenue;
                    profitSparkData[idx] = (double)row.Profit;
                    invoiceSparkData[idx] = row.InvoiceCount;
                }

                avgSparkData[idx] = invoiceSparkData[idx] > 0 ? salesSparkData[idx] / invoiceSparkData[idx] : 0;
            }

            if (SalesSparkline[0] is LineSeries<double> ls) ls.Values = salesSparkData;
            if (ProfitSparkline[0] is LineSeries<double> lp) lp.Values = profitSparkData;
            if (InvoiceSparkline[0] is LineSeries<double> li) li.Values = invoiceSparkData;
            if (AvgInvoiceSparkline[0] is LineSeries<double> la) la.Values = avgSparkData;
        });
    }

    private async Task LoadInventoryCardsAsync()
    {
        var products = await Task.Run(() => ReportingInventoryCatalog.GetAll().ToList());

        await EnqueueOnUI(() =>
        {
            var alertGreenForeground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            var alertRedBackground = new SolidColorBrush(Color.FromArgb(255, 252, 232, 230));
            var alertRedForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            var alertAmberBackground = new SolidColorBrush(Color.FromArgb(255, 255, 244, 229));
            var alertAmberForeground = new SolidColorBrush(Color.FromArgb(255, 239, 108, 0));

            var shelfLowCount = products.Count(p => p.HasShelfLowAlert);
            var warehouseLowCount = products.Count(p => p.HasWarehouseLowAlert);
            var shelfEmptyCount = products.Count(p => p.IsShelfOutOfStock);
            var warehouseEmptyCount = products.Count(p => p.IsWarehouseOutOfStock);

            ApplyInventorySummaryToUI(new InventorySummary {
                TotalProducts = products.Count,
                ShelfLow = shelfLowCount,
                WarehouseLow = warehouseLowCount,
                ShelfEmpty = shelfEmptyCount,
                WarehouseEmpty = warehouseEmptyCount
            });

            var stockAmber = new SolidColorBrush(Color.FromArgb(255, 255, 143, 0));
            var stockRed = new SolidColorBrush(Color.FromArgb(255, 229, 57, 53));

            var lowStockRows = products.Select(product =>
            {
                var displayName = string.IsNullOrWhiteSpace(product.Name) ? $"Product #{product.Id}" : product.Name.Trim();
                var shelfOut = product.IsShelfOutOfStock;
                var warehouseOut = product.IsWarehouseOutOfStock;
                var shelfLow = product.HasShelfLowAlert;
                var warehouseLow = product.HasWarehouseLowAlert;

                if (shelfOut && warehouseOut) return new { Item = new DashboardStockItem { Name = displayName, PrimaryLabelText = LocalizationHelper.GetString("ReportsDashboard_Stock_ShelfLabel"), PrimaryStatusText = LocalizationHelper.GetString("ReportsDashboard_Stock_Empty"), PrimaryStatusBrush = stockRed, SecondaryLabelText = LocalizationHelper.GetString("ReportsDashboard_Stock_WarehouseLabel"), SecondaryStatusText = LocalizationHelper.GetString("ReportsDashboard_Stock_Empty"), SecondaryStatusBrush = stockRed, SimpleStatusVisibility = Visibility.Collapsed, DetailedStatusVisibility = Visibility.Visible, SimpleBadgeTextVisibility = Visibility.Collapsed, BadgeVisible = Visibility.Visible }, SeverityRank = 0, RemainingQty = 0m, SortName = displayName };
                if (shelfOut || warehouseOut) return new { Item = new DashboardStockItem { Name = displayName, StockStatus = LocalizationHelper.GetString("ReportsDashboard_Stock_OutOffStock"), StatusBrush = stockRed, BadgeText = shelfOut ? LocalizationHelper.GetString("ReportsDashboard_Stock_Shelf") : LocalizationHelper.GetString("ReportsDashboard_Stock_Warehouse"), BadgeVisible = Visibility.Visible }, SeverityRank = 1, RemainingQty = 0m, SortName = displayName };
                if (shelfLow && warehouseLow) return new { Item = new DashboardStockItem { Name = displayName, PrimaryLabelText = LocalizationHelper.GetString("ReportsDashboard_Stock_ShelfLabel"), PrimaryStatusText = string.Format(LocalizationHelper.GetString("ReportsDashboard_Stock_QtyLeftFormat"), product.QuantityStore.ToString("N0")), PrimaryStatusBrush = stockAmber, SecondaryLabelText = LocalizationHelper.GetString("ReportsDashboard_Stock_WarehouseLabel"), SecondaryStatusText = string.Format(LocalizationHelper.GetString("ReportsDashboard_Stock_QtyLeftFormat"), product.QuantityWarehouse.ToString("N0")), SecondaryStatusBrush = stockAmber, SimpleStatusVisibility = Visibility.Collapsed, DetailedStatusVisibility = Visibility.Visible, SimpleBadgeTextVisibility = Visibility.Collapsed, BadgeVisible = Visibility.Visible }, SeverityRank = 2, RemainingQty = Math.Max(0m, (decimal)(product.QuantityStore + product.QuantityWarehouse)), SortName = displayName };
                if (shelfLow || warehouseLow) return new { Item = new DashboardStockItem { Name = displayName, StockStatus = LocalizationHelper.GetString("ReportsDashboard_Stock_LowStock"), StatusBrush = stockAmber, BadgeText = shelfLow ? LocalizationHelper.GetString("ReportsDashboard_Stock_Shelf") : LocalizationHelper.GetString("ReportsDashboard_Stock_Warehouse"), BadgeVisible = Visibility.Visible }, SeverityRank = 3, RemainingQty = Math.Max(0m, (decimal)(shelfLow ? product.QuantityStore : product.QuantityWarehouse)), SortName = displayName };
                return null;
            })
                .Where(r => r is not null)
                .Select(r => r!)
                .OrderBy(r => r.SeverityRank)
                .ThenBy(r => r.RemainingQty)
                .ThenBy(r => r.SortName)
                .Take(8)
                .Select(r => r.Item)
                .ToList();

            var finalRows = lowStockRows.Count > 0 ? (IEnumerable<DashboardStockItem>)lowStockRows : new[] { new DashboardStockItem { Name = LocalizationHelper.GetString("ReportsDashboard_Stock_AllStocked"), StockStatus = LocalizationHelper.GetString("ReportsDashboard_Stock_Healthy"), StatusBrush = alertGreenForeground, BadgeVisible = Visibility.Collapsed } };
            LowStockList.Items.Clear();
            foreach (var r in finalRows) LowStockList.Items.Add(r);
        });
    }

    private static void DrawDonutChart(
        Canvas canvas,
        TextBlock centerLabel,
        (double Pct, Color Color)[] segments,
        double canvasSize,
        double radius,
        double thickness)
    {
        canvas.Children.Clear();
        double cx = canvasSize / 2;
        double cy = canvasSize / 2;

        // Draw each arc segment
        double startAngle = -90.0; // start at top
        foreach (var (pct, color) in segments)
        {
            if (pct <= 0) continue;

            double sweepAngle = pct * 360.0;

            // Clamp slightly to avoid path collapsing at exactly 360
            if (sweepAngle >= 360) sweepAngle = 359.99;

            var path = CreateArcPath(cx, cy, radius, startAngle, sweepAngle, color, thickness);
            canvas.Children.Add(path);

            startAngle += sweepAngle;
        }
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreateArcPath(
        double cx, double cy, double radius,
        double startDeg, double sweepDeg,
        Color color, double thickness)
    {
        double startRad = startDeg * Math.PI / 180.0;
        double endRad = (startDeg + sweepDeg) * Math.PI / 180.0;

        var startPt = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
        var endPt = new Point(cx + radius * Math.Cos(endRad), cy + radius * Math.Sin(endRad));

        var figure = new PathFigure
        {
            StartPoint = startPt,
            IsClosed = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = endPt,
            Size = new Size(radius, radius),
            IsLargeArc = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle = 0
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap = PenLineCap.Flat,
            Fill = null
        };
    }

    private static bool IsDiscountSaleItem(SaleItem item)
    {
        return item.LineTotal < 0
            && item.ProductId == 0
            && item.Name.StartsWith("Discount", StringComparison.OrdinalIgnoreCase);
    }

    private FrameworkElement? _lastInfoTarget;

    private async void InfoIcon_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not Button btn) return;

            if (CardInfoTip.IsOpen && _lastInfoTarget == btn)
            {
                CardInfoTip.IsOpen = false;
                _lastInfoTarget = null;
                return;
            }

            if (CardInfoTip.IsOpen)
            {
                CardInfoTip.IsOpen = false;
                await Task.Delay(200);
            }

            CardInfoTip.Target = btn;
            var tagKey = btn.Tag?.ToString() ?? string.Empty;
            CardInfoTip.Subtitle = LocalizationHelper.GetString(tagKey);
            CardInfoTip.IsOpen = true;
            _lastInfoTarget = btn;
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"InfoIcon_Click failed: {ex}");
        }
    }

    private void SkeletonTimer_Tick(object? sender, object e)
    {
        _skeletonPhase += 0.05;
        if (_skeletonPhase > 1.35) _skeletonPhase = -0.35;

        if (_skeletonShimmerBrush != null)
        {
            _skeletonShimmerBrush.GradientStops[1].Offset = _skeletonPhase;
            _skeletonShimmerBrush.GradientStops[2].Offset = _skeletonPhase + 0.05;
            _skeletonShimmerBrush.GradientStops[3].Offset = _skeletonPhase + 0.15;
        }
    }

    private void SetDashboardLoadingState(bool isLoading)
    {
        if (isLoading)
        {
            _skeletonTimer.Start();
        }
    }

    private void StopSkeletonShimmer()
    {
        _skeletonTimer.Stop();
    }
}
