using System.Collections.ObjectModel;
using System.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using global::RetailStorePOS.Data.Modules.Contracts;
using global::RetailStorePOS.Data.Modules.Products;
using global::RetailStorePOS.Data.Modules.Sales;
using RetailStorePOS.WinUiLogin.Common;
using SkiaSharp;
using global::RetailStorePOS.Data.Modules.Reporting;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Xaml.Media.Animation;
using RetailStorePOS.App.Services;
using RetailStorePOS.App.Services.Licensing;

namespace RetailStorePOS.WinUiLogin.Views;

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
    public string ReportsDashboard_KPI_SalesToday => Loc["ReportsDashboard_KPI_SalesToday.Text"];
    public string ReportsDashboard_KPI_NetProfit => Loc["ReportsDashboard_KPI_NetProfit.Text"];
    public string ReportsDashboard_KPI_InvoiceCount => Loc["ReportsDashboard_KPI_InvoiceCount.Text"];
    public string ReportsDashboard_KPI_AvgInvoiceValue => Loc["ReportsDashboard_KPI_AvgInvoiceValue.Text"];
    public string ReportsDashboard_MedianLabel => Loc["ReportsDashboard_MedianLabel.Text"];
    public string ReportsDashboard_Target_Title => Loc["ReportsDashboard_Target_Title.Text"];
    public string ReportsDashboard_Target_TodayLabel => Loc["ReportsDashboard_Target_TodayLabel.Text"];
    public string ReportsDashboard_Target_GoalLabel => Loc["ReportsDashboard_Target_GoalLabel.Text"];
    public string ReportsDashboard_Alerts_Title => Loc["ReportsDashboard_Alerts_Title.Text"];
    public string ReportsDashboard_TopProducts_Title => Loc["ReportsDashboard_TopProducts_Title.Text"];
    public string ReportsDashboard_StockStatus_Title => Loc["ReportsDashboard_StockStatus_Title.Text"];
    public string ReportsDashboard_Discounts_Title => Loc["ReportsDashboard_Discounts_Title.Text"];
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
        TodayDateValue = CurrencyDisplayHelper.FormatDate(DateTime.Now);
        
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
        // Date pill in the dashboard header is a paid-tier surface. When the
        // active license snapshot does not list DashboardDatePill (or a dev
        // override / force-free-mode denies it), hide the pill. The rest of
        // the dashboard header continues to render normally.
        var canShowDatePill = LoginRuntime.FeatureAccess?.CanUse(FeatureAccessService.Features.DashboardDatePill) ?? false;
        if (TodayDatePill is not null)
        {
            TodayDatePill.Visibility = canShowDatePill ? Visibility.Visible : Visibility.Collapsed;
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
        catch
        {
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
    /// silent because the live UI is already authoritative.
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
        catch
        {
            // Best-effort: a write failure does not affect the live UI.
        }
    }

    private static bool HasUsableDashboardSnapshot()
    {
        try
        {
            return _dashboardSnapshotStore.LoadSnapshot() is not null;
        }
        catch
        {
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

        CriticalAlertsList.ItemsSource = new ObservableCollection<DashboardAlertItem>
        {
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Shelf"), LocalizationHelper.GetString("ReportsDashboard_Stock_LowStock")), inv.ShelfLow, alertAmberBackground, alertAmberForeground),
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Warehouse"), LocalizationHelper.GetString("ReportsDashboard_Stock_LowStock")), inv.WarehouseLow, alertAmberBackground, alertAmberForeground),
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Shelf"), LocalizationHelper.GetString("ReportsDashboard_Stock_Empty")), inv.ShelfEmpty, alertRedBackground, alertRedForeground),
            CreateAlertItem(string.Format(LocalizationHelper.GetString("ReportsDashboard_Alert_Format"), LocalizationHelper.GetString("ReportsDashboard_Stock_Warehouse"), LocalizationHelper.GetString("ReportsDashboard_Stock_Empty")), inv.WarehouseEmpty, alertRedBackground, alertRedForeground)
        };

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


    private async Task PopulateDashboardAsync()
    {
        RefreshLocalizedProperties();

        var todayStartUtc = DateTime.Today.ToUniversalTime();
        var todayEndUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        var yesterdayStartUtc = DateTime.Today.AddDays(-1).ToUniversalTime();
        var yesterdayEndUtc = DateTime.Today.ToUniversalTime();
        var currentLocalTimeOffset = DateTime.Now.TimeOfDay;
        var yesterdayPacingEndUtc = yesterdayStartUtc.Add(currentLocalTimeOffset);
        var pastWeekStartUtc = DateTime.Today.AddDays(-6).ToUniversalTime();

        // Reveal the main Grid instantly so skeletons appear
        DashboardScrollViewer.Visibility = Visibility.Visible;

        // Perform the heavy DB queries completely off the main thread, exactly once.
        var metricsData = await Task.Run(() =>
        {
            return new
            {
                Today = ReportingSalesModule.GetDashboardMetrics(todayStartUtc, todayEndUtc),
                YestPacing = ReportingSalesModule.GetDashboardMetrics(yesterdayStartUtc, yesterdayPacingEndUtc),
                YestFull = ReportingSalesModule.GetDashboardMetrics(yesterdayStartUtc, yesterdayEndUtc)
            };
        });

        // Fire all remaining data fetches in parallel
        await Task.WhenAll(
            LoadSalesCardAsync(metricsData.Today, metricsData.YestPacing),
            LoadProfitCardAsync(metricsData.Today),
            LoadInvoiceCountCardAsync(metricsData.Today, metricsData.YestPacing),
            LoadAvgInvoiceCardAsync(metricsData.Today),
            LoadSalesTargetCardAsync(metricsData.Today, metricsData.YestFull),
            LoadDiscountsCardAsync(metricsData.Today, metricsData.YestFull),
            LoadTopProductsCardAsync(todayStartUtc, todayEndUtc),
            LoadInventoryCardsAsync(),
            LoadSparklinesAsync(pastWeekStartUtc, todayEndUtc)
        );

        // All data is loaded – now reveal everything and animate
        DispatcherQueue.TryEnqueue(RevealAndAnimateDashboard);
    }

    private async void RevealAndAnimateDashboard()
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
            _donutAnimStart = DateTime.UtcNow;
            _donutAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _donutAnimTimer.Tick += DonutAnimTimer_Tick;
            _donutAnimTimer.Start();
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

    private async Task LoadSalesCardAsync(DashboardMetrics todayMetrics, DashboardMetrics yesterdayMetrics)
    {
        await EnqueueOnUI(() =>
        {
            var todayTotal = todayMetrics.Revenue;
            var yesterdayPacingTotal = yesterdayMetrics.Revenue;
            SalesTodayValue = CurrencyDisplayHelper.FormatConfiguredAmount(todayTotal);

            double salesDelta = yesterdayPacingTotal > 0
                ? (double)((todayTotal - yesterdayPacingTotal) / yesterdayPacingTotal) * 100.0
                : (todayTotal > 0 ? 100.0 : 0);

            if (salesDelta >= 0)
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)salesDelta, "0.#");
                SalesDeltaValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_Sales_Delta_Positive"), valStr);
                SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)Math.Abs(salesDelta), "0.#");
                SalesDeltaValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_Sales_Delta_Negative"), valStr);
                SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            }
        });
    }

    private async Task LoadProfitCardAsync(DashboardMetrics todayMetrics)
    {
        await EnqueueOnUI(() =>
        {
            var todayTotal = todayMetrics.Revenue;
            var todayProfit = todayMetrics.Profit;
            NetProfitValue = CurrencyDisplayHelper.FormatConfiguredAmount(todayProfit);

            if (todayTotal > 0)
            {
                var marginPct = (double)(todayProfit / todayTotal) * 100.0;
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

    private async Task LoadInvoiceCountCardAsync(DashboardMetrics todayMetrics, DashboardMetrics yesterdayMetrics)
    {
        await EnqueueOnUI(() =>
        {
            var todayInvoiceCount = todayMetrics.InvoiceCount;
            var yesterdayPacingInvoiceCount = yesterdayMetrics.InvoiceCount;
            InvoiceCountValue = CurrencyDisplayHelper.FormatInt(todayInvoiceCount);

            double invoiceDelta = yesterdayPacingInvoiceCount > 0
                ? ((double)(todayInvoiceCount - yesterdayPacingInvoiceCount) / yesterdayPacingInvoiceCount) * 100.0
                : (todayInvoiceCount > 0 ? 100.0 : 0);

            if (invoiceDelta >= 0)
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)invoiceDelta, "0.#");
                InvoiceDeltaValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_Invoice_Delta_Positive"), valStr);
                InvoiceDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                var valStr = CurrencyDisplayHelper.FormatNumber((decimal)Math.Abs(invoiceDelta), "0.#");
                InvoiceDeltaValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_Invoice_Delta_Negative"), valStr);
                InvoiceDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            }
        });
    }

    private async Task LoadAvgInvoiceCardAsync(DashboardMetrics todayMetrics)
    {
        await EnqueueOnUI(() =>
        {
            var avgInvoice = todayMetrics.InvoiceCount > 0 ? todayMetrics.Revenue / todayMetrics.InvoiceCount : 0m;
            AvgInvoiceValue = CurrencyDisplayHelper.FormatConfiguredAmount(avgInvoice);

            if (todayMetrics.InvoiceCount > 0)
            {
                MedianValue = CurrencyDisplayHelper.FormatConfiguredAmount(todayMetrics.MedianInvoice);
            }
            else
            {
                MedianValue = string.Empty;
            }
        });
    }

    private async Task LoadSalesTargetCardAsync(DashboardMetrics todayMetrics, DashboardMetrics yesterdayMetrics)
    {
        var prefs = await LoginRuntime.LocalPreferences.LoadPreferencesAsync();
        var dailyTargetAmount = prefs.DailyTarget;

        await EnqueueOnUI(() =>
        {
            var todayTotal = todayMetrics.Revenue;
            var yesterdayFullTotal = yesterdayMetrics.Revenue;

            var achievedPct = dailyTargetAmount > 0 ? (double)(todayTotal / dailyTargetAmount) * 100.0 : 0;
            
            CurrentSalesValue = CurrencyDisplayHelper.FormatConfiguredAmount(todayTotal);
            TargetValueText = CurrencyDisplayHelper.FormatConfiguredAmount(dailyTargetAmount);
            SalesAchievedPctValue = CurrencyDisplayHelper.FormatPercent(achievedPct, "0");
            
            TargetSubValue = string.Format(LocalizationHelper.GetString("ReportsDashboard_Target_YesterdayPerformance"), CurrencyDisplayHelper.FormatConfiguredAmount(yesterdayFullTotal));

            _todayBarPct = dailyTargetAmount > 0 ? Math.Min(1.0, Math.Max(0, (double)(todayTotal / dailyTargetAmount))) : 0;
            _yesterdayBarPct = dailyTargetAmount > 0 ? Math.Min(1.0, Math.Max(0, (double)(yesterdayFullTotal / dailyTargetAmount))) : 0;
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

    private async Task LoadTopProductsCardAsync(DateTime todayStartUtc, DateTime todayEndUtc)
    {
        var topProducts = await Task.Run(() => ReportingSalesModule.GetTopProducts(todayStartUtc, todayEndUtc, 5));
        await EnqueueOnUI(() =>
        {
            var topProductsTotalRevenue = topProducts.Sum(item => item.Revenue);
            TopProductsList.ItemsSource = new ObservableCollection<DashboardProductItem>(
                topProducts.Count > 0
                    ? topProducts.Select(item => new DashboardProductItem
                    {
                        Name = item.Name,
                        Revenue = CurrencyDisplayHelper.FormatConfiguredAmount(item.Revenue),
                        Pct = topProductsTotalRevenue > 0
                            ? Math.Min(100, Math.Max(0, (double)(item.Revenue / topProductsTotalRevenue) * 100.0))
                            : 0
                    })
                    : new[] { new DashboardProductItem { Name = LocalizationHelper.GetString("ReportsDashboard_TopProducts_None"), Revenue = CurrencyDisplayHelper.FormatConfiguredAmount(0m), Pct = 0 } }
            );
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

    private async Task LoadDiscountsCardAsync(DashboardMetrics todayMetrics, DashboardMetrics yesterdayMetrics)
    {
        await EnqueueOnUI(() =>
        {
            var todayDiscountAmount = todayMetrics.DiscountAmount;
            var yesterdayDiscountAmount = yesterdayMetrics.DiscountAmount;
            var discountsDeltaPct = yesterdayDiscountAmount > 0
                ? (double)((todayDiscountAmount - yesterdayDiscountAmount) / yesterdayDiscountAmount) * 100.0
                : todayDiscountAmount > 0 ? 100.0 : 0.0;

            DiscountsTotalText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayDiscountAmount);
            DiscountsDeltaText.Text = CurrencyDisplayHelper.FormatStringWithDigits(discountsDeltaPct == 0 ? "0%" : $"{discountsDeltaPct:+0.#;-0.#}%");
            DiscountsCountText.Text = string.Format(LocalizationHelper.GetString("ReportsDashboard_Discounts_CountFormat"), CurrencyDisplayHelper.FormatInt(todayMetrics.DiscountCount));
        });
    }

    private async Task LoadSparklinesAsync(DateTime pastWeekStartUtc, DateTime todayEndUtc)
    {
        var pastWeekSparklines = await Task.Run(() => ReportingSalesModule.GetDailySparklines(pastWeekStartUtc, todayEndUtc));
        await EnqueueOnUI(() =>
        {
            var numDays = 7;
            var salesSparkData = new double[numDays + 1];
            var profitSparkData = new double[numDays + 1];
            var invoiceSparkData = new double[numDays + 1];
            var avgSparkData = new double[numDays + 1];

            salesSparkData[0] = profitSparkData[0] = invoiceSparkData[0] = avgSparkData[0] = 0;

            var startDate = DateTime.Today.AddDays(-6);
            foreach (var day in pastWeekSparklines)
            {
                if (DateTime.TryParse(day.LocalDay, out var parsedDate))
                {
                    int dayOffset = (int)(parsedDate.Date - startDate).TotalDays;
                    if (dayOffset >= 0 && dayOffset < numDays)
                    {
                        var idx = dayOffset + 1;
                        salesSparkData[idx] = (double)day.Revenue;
                        profitSparkData[idx] = (double)day.Profit;
                        invoiceSparkData[idx] = day.InvoiceCount;
                    }
                }
            }

            for (int i = 1; i <= numDays; i++)
            {
                avgSparkData[i] = invoiceSparkData[i] > 0 ? salesSparkData[i] / invoiceSparkData[i] : 0;
            }

            if (SalesSparkline[0] is LineSeries<double> ls) ls.Values = new ObservableCollection<double>(salesSparkData);
            if (ProfitSparkline[0] is LineSeries<double> lp) lp.Values = new ObservableCollection<double>(profitSparkData);
            if (InvoiceSparkline[0] is LineSeries<double> li) li.Values = new ObservableCollection<double>(invoiceSparkData);
            if (AvgInvoiceSparkline[0] is LineSeries<double> la) la.Values = new ObservableCollection<double>(avgSparkData);
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

            LowStockList.ItemsSource = new ObservableCollection<DashboardStockItem>(lowStockRows.Count > 0 ? lowStockRows : new[] { new DashboardStockItem { Name = LocalizationHelper.GetString("ReportsDashboard_Stock_AllStocked"), StockStatus = LocalizationHelper.GetString("ReportsDashboard_Stock_Healthy"), StatusBrush = alertGreenForeground, BadgeVisible = Visibility.Collapsed } });
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
