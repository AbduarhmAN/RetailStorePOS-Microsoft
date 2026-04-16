using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;
using Windows.Foundation;
using Windows.UI;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Defaults;
using SkiaSharp;

namespace RetailStorePOS.WinUiLogin.Views;

// ─── Data models ──────────────────────────────────────────────────────────────

public sealed class DashboardProductItem
{
    public string Name    { get; set; } = string.Empty;
    public string Revenue { get; set; } = string.Empty;
    public double Pct     { get; set; }
}

public sealed class DashboardStockItem
{
    public string Name        { get; set; } = string.Empty;
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
    public string BadgeText   { get; set; } = string.Empty;
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

// ─── Page ─────────────────────────────────────────────────────────────────────

public sealed partial class ReportsDashboardPage : Page
{
    private readonly DispatcherTimer _skeletonTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private LinearGradientBrush? _skeletonShimmerBrush;
    private bool _isDashboardLoading;
    private bool _hasLoadedDashboard;
    private double _skeletonPhase = -0.35;
    private double _todayBarPct;
    private double _yesterdayBarPct;
    private (double Pct, Color Color)[]? _donutSegments;
    private DispatcherTimer? _donutAnimTimer;
    private DateTime _donutAnimStart;

    public ReportsDashboardPage()
    {
        this.InitializeComponent();
        _skeletonShimmerBrush = Resources["SkeletonShimmerBrush"] as LinearGradientBrush;
        _skeletonTimer.Tick += SkeletonTimer_Tick;
        Loaded += ReportsDashboardPage_Loaded;
        Unloaded += ReportsDashboardPage_Unloaded;
    }

    // ── LiveCharts Sparkline Properties ──────────────────────────────────────
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

    private async void ReportsDashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hasLoadedDashboard || _isDashboardLoading)
        {
            return;
        }

        _isDashboardLoading = true;
        SetDashboardLoadingState(true);

        try
        {
            await PopulateDashboardAsync();
            _hasLoadedDashboard = true;
        }
        catch
        {
        }
        finally
        {
            _isDashboardLoading = false;
            SetDashboardLoadingState(false);
        }
    }

    private void ReportsDashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        StopSkeletonShimmer();
    }

    
    private async Task PopulateDashboardAsync()
    {
        // ── HEADER ─────────────────────────────────────────
        var hour = DateTime.Now.Hour;
        GreetingText.Text = hour < 12 ? "Good morning, Store Manager"
            : hour < 17 ? "Good afternoon, Store Manager"
            : "Good evening, Store Manager";
        TodayDateText.Text = DateTime.Now.ToString("MMMM dd, yyyy");

        var todayStartUtc = DateTime.Today.ToUniversalTime();
        var todayEndUtc = DateTime.Today.AddDays(1).ToUniversalTime();
        var yesterdayStartUtc = DateTime.Today.AddDays(-1).ToUniversalTime();
        var yesterdayEndUtc = DateTime.Today.ToUniversalTime();
        var currentLocalTimeOffset = DateTime.Now.TimeOfDay;
        var yesterdayPacingEndUtc = yesterdayStartUtc.Add(currentLocalTimeOffset);
        var pastWeekStartUtc = DateTime.Today.AddDays(-6).ToUniversalTime();

        // Reveal the main Grid instantly so skeletons appear
        DashboardScrollViewer.Visibility = Visibility.Visible;

        // Fire all data fetches in parallel, wait for ALL to complete
        await Task.WhenAll(
            LoadSalesCardAsync(todayStartUtc, todayEndUtc, yesterdayStartUtc, yesterdayPacingEndUtc),
            LoadProfitCardAsync(todayStartUtc, todayEndUtc),
            LoadInvoiceCountCardAsync(todayStartUtc, todayEndUtc, yesterdayStartUtc, yesterdayPacingEndUtc),
            LoadAvgInvoiceCardAsync(todayStartUtc, todayEndUtc),
            LoadSalesTargetCardAsync(todayStartUtc, todayEndUtc, yesterdayStartUtc, yesterdayEndUtc),
            LoadDiscountsCardAsync(todayStartUtc, todayEndUtc, yesterdayStartUtc, yesterdayEndUtc),
            LoadTopProductsCardAsync(todayStartUtc, todayEndUtc),
            LoadInventoryCardsAsync(),
            LoadSparklinesAsync(pastWeekStartUtc, todayEndUtc)
        );

        // All data is loaded — now reveal everything and animate
        DispatcherQueue.TryEnqueue(RevealAndAnimateDashboard);
    }

    private async void RevealAndAnimateDashboard()
    {
        // ── Step 1: Reveal all cards at once (hide skeletons, show content) ──
        SalesTodaySkeleton.Visibility = Visibility.Collapsed;
        SalesTodayContent.Visibility = Visibility.Visible;

        NetProfitSkeleton.Visibility = Visibility.Collapsed;
        NetProfitContent.Visibility = Visibility.Visible;

        InvoiceCountSkeleton.Visibility = Visibility.Collapsed;
        InvoiceCountContent.Visibility = Visibility.Visible;

        AvgInvoiceSkeleton.Visibility = Visibility.Collapsed;
        AvgInvoiceContent.Visibility = Visibility.Visible;

        SalesTargetSkeleton.Visibility = Visibility.Collapsed;
        SalesTargetContent.Visibility = Visibility.Visible;

        TopProdSkeleton.Visibility = Visibility.Collapsed;
        TopProdContent.Visibility = Visibility.Visible;

        DiscountsSkeleton.Visibility = Visibility.Collapsed;
        DiscountsContent.Visibility = Visibility.Visible;

        CrAlertsSkeleton.Visibility = Visibility.Collapsed;
        CrAlertsContent.Visibility = Visibility.Visible;

        LowStockSkeleton.Visibility = Visibility.Collapsed;
        LowStockContent.Visibility = Visibility.Visible;

        StockHealthSkeleton.Visibility = Visibility.Collapsed;
        StockHealthContent.Visibility = Visibility.Visible;

        StopSkeletonShimmer();

        // ── Step 2: Wait one frame for WinUI to measure & layout ──
        await Task.Delay(50);

        // ── Step 3: Animate Sales Target bars ──
        SalesBarContainer.SizeChanged += SalesBarContainer_SizeChanged;
        UpdateSalesBar(_todayBarPct, _yesterdayBarPct);

        // ── Step 4: Animate Top Products progress bars ──
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
                    var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
                    {
                        From = 0,
                        To = targetValue,
                        Duration = new Duration(TimeSpan.FromSeconds(3)),
                        EnableDependentAnimation = true
                    };
                    var sb = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
                    sb.Children.Add(anim);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(anim, progressBar);
                    Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(anim, "Value");
                    sb.Begin();
                }
            }
        }

        // ── Step 5: Animate Stock Health donut ──
        if (_donutSegments != null)
        {
            _donutAnimStart = DateTime.UtcNow;
            _donutAnimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _donutAnimTimer.Tick += DonutAnimTimer_Tick;
            _donutAnimTimer.Start();
        }
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

    private async Task LoadSalesCardAsync(DateTime todayStartUtc, DateTime todayEndUtc, DateTime yesterdayStartUtc, DateTime yesterdayPacingEndUtc)
    {
        var todayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(todayStartUtc, todayEndUtc));
        var yesterdayPacingMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(yesterdayStartUtc, yesterdayPacingEndUtc));

        DispatcherQueue.TryEnqueue(() =>
        {
            var todayTotal = todayMetrics.Revenue;
            var yesterdayPacingTotal = yesterdayPacingMetrics.Revenue;
            SalesTodayText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayTotal);

            double salesDelta = yesterdayPacingTotal > 0 
                ? (double)((todayTotal - yesterdayPacingTotal) / yesterdayPacingTotal) * 100.0 
                : (todayTotal > 0 ? 100.0 : 0);

            if (salesDelta >= 0)
            {
                SalesDeltaText.Text = $"+ {salesDelta:0.#}%  vs  yesterday";
                SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                SalesDeltaText.Text = $"- {Math.Abs(salesDelta):0.#}%  vs  yesterday";
                SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            }
            // Skeleton swap deferred to RevealAndAnimateDashboard
        });
    }

    private async Task LoadProfitCardAsync(DateTime todayStartUtc, DateTime todayEndUtc)
    {
        var todayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(todayStartUtc, todayEndUtc));
        DispatcherQueue.TryEnqueue(() =>
        {
            var todayTotal = todayMetrics.Revenue;
            var todayProfit = todayMetrics.Profit;
            ProfitTodayText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayProfit);

            if (todayTotal > 0)
            {
                var marginPct = (double)(todayProfit / todayTotal) * 100.0;
                ProfitDeltaText.Text = $"{marginPct:0.#}% margin";
                ProfitDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                ProfitDeltaText.Text = "No sales yet";
                ProfitDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163));
            }
            // Skeleton swap deferred to RevealAndAnimateDashboard
        });
    }

    private async Task LoadInvoiceCountCardAsync(DateTime todayStartUtc, DateTime todayEndUtc, DateTime yesterdayStartUtc, DateTime yesterdayPacingEndUtc)
    {
        var todayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(todayStartUtc, todayEndUtc));
        var yesterdayPacingMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(yesterdayStartUtc, yesterdayPacingEndUtc));
        DispatcherQueue.TryEnqueue(() =>
        {
            var todayInvoiceCount = todayMetrics.InvoiceCount;
            var yesterdayPacingInvoiceCount = yesterdayPacingMetrics.InvoiceCount;
            InvoiceCountText.Text = todayInvoiceCount.ToString("N0");

            double invoiceDelta = yesterdayPacingInvoiceCount > 0 
                ? ((double)(todayInvoiceCount - yesterdayPacingInvoiceCount) / yesterdayPacingInvoiceCount) * 100.0 
                : (todayInvoiceCount > 0 ? 100.0 : 0);

            if (invoiceDelta >= 0)
            {
                InvoiceDeltaText.Text = $"+ {invoiceDelta:0.#}%  vs  yesterday";
                InvoiceDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            }
            else
            {
                InvoiceDeltaText.Text = $"- {Math.Abs(invoiceDelta):0.#}%  vs  yesterday";
                InvoiceDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            }
            // Skeleton swap deferred to RevealAndAnimateDashboard
        });
    }

    private async Task LoadAvgInvoiceCardAsync(DateTime todayStartUtc, DateTime todayEndUtc)
    {
        var todayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(todayStartUtc, todayEndUtc));
        DispatcherQueue.TryEnqueue(() =>
        {
            var avgInvoice = todayMetrics.InvoiceCount > 0 ? todayMetrics.Revenue / todayMetrics.InvoiceCount : 0m;
            AvgInvoiceText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(avgInvoice);
            MedianText.Text = string.Empty;

            // Skeleton swap deferred to RevealAndAnimateDashboard
        });
    }

    private async Task LoadSalesTargetCardAsync(DateTime todayStartUtc, DateTime todayEndUtc, DateTime yesterdayStartUtc, DateTime yesterdayEndUtc)
    {
        var todayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(todayStartUtc, todayEndUtc));
        var yesterdayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(yesterdayStartUtc, yesterdayEndUtc));
        DispatcherQueue.TryEnqueue(() =>
        {
            var todayTotal = todayMetrics.Revenue;
            var yesterdayFullTotal = yesterdayMetrics.Revenue;
            const decimal dailyTargetAmount = 200000m;
            
            var gapPct = dailyTargetAmount > 0 ? (double)((todayTotal - dailyTargetAmount) / dailyTargetAmount) * 100.0 : 0;

            CurrentSalesText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayTotal);
            SalesGapText.Text = $"{gapPct:+0.#;-0.#}% vs target";
            SalesGapText.Foreground = gapPct >= 0
                ? new SolidColorBrush(Color.FromArgb(255, 46, 125, 50))
                : new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            TargetText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(dailyTargetAmount);
            TargetSubText.Text = $"YESTERDAY: {CurrencyDisplayHelper.FormatConfiguredAmount(yesterdayFullTotal)}  ·  TARGET: {CurrencyDisplayHelper.FormatConfiguredAmount(dailyTargetAmount)}";

            _todayBarPct = dailyTargetAmount > 0 ? Math.Min(1.0, Math.Max(0, (double)(todayTotal / dailyTargetAmount))) : 0;
            _yesterdayBarPct = dailyTargetAmount > 0 ? Math.Min(1.0, Math.Max(0, (double)(yesterdayFullTotal / dailyTargetAmount))) : 0;

            // Skeleton swap + animation deferred to RevealAndAnimateDashboard
        });
    }

    private void SalesBarContainer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSalesBar(_todayBarPct, _yesterdayBarPct);
    }

    private async Task LoadTopProductsCardAsync(DateTime todayStartUtc, DateTime todayEndUtc)
    {
        var topProducts = await Task.Run(() => LoginRuntime.Sales.GetTopProducts(todayStartUtc, todayEndUtc, 5));
        DispatcherQueue.TryEnqueue(() =>
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
                    : new[] { new DashboardProductItem { Name = "No products sold today", Revenue = CurrencyDisplayHelper.FormatConfiguredAmount(0m), Pct = 0 } }
            );

            // Skeleton swap + animation deferred to RevealAndAnimateDashboard
        });
    }

    private static T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
    {
        for (int i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(obj, i);
            if (child is T t) return t;
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null) return childOfChild;
        }
        return null;
    }

    private async Task LoadDiscountsCardAsync(DateTime todayStartUtc, DateTime todayEndUtc, DateTime yesterdayStartUtc, DateTime yesterdayEndUtc)
    {
        var todayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(todayStartUtc, todayEndUtc));
        var yesterdayMetrics = await Task.Run(() => LoginRuntime.Sales.GetDashboardMetrics(yesterdayStartUtc, yesterdayEndUtc));
        DispatcherQueue.TryEnqueue(() =>
        {
            var todayDiscountAmount = todayMetrics.DiscountAmount;
            var yesterdayDiscountAmount = yesterdayMetrics.DiscountAmount;
            var discountsDeltaPct = yesterdayDiscountAmount > 0
                ? (double)((todayDiscountAmount - yesterdayDiscountAmount) / yesterdayDiscountAmount) * 100.0
                : todayDiscountAmount > 0 ? 100.0 : 0.0;

            DiscountsTotalText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayDiscountAmount);
            DiscountsDeltaText.Text = discountsDeltaPct == 0 ? "0%" : $"{discountsDeltaPct:+0.#;-0.#}%";
            DiscountsCountText.Text = $"{todayMetrics.DiscountCount} Discounts Applied";

            // Skeleton swap deferred to RevealAndAnimateDashboard
        });
    }

    private async Task LoadSparklinesAsync(DateTime pastWeekStartUtc, DateTime todayEndUtc)
    {
        var pastWeekSparklines = await Task.Run(() => LoginRuntime.Sales.GetDailySparklines(pastWeekStartUtc, todayEndUtc));
        DispatcherQueue.TryEnqueue(() =>
        {
            var numDays = 7;
            var salesSparkData   = new double[numDays + 1];
            var profitSparkData  = new double[numDays + 1];
            var invoiceSparkData = new double[numDays + 1];
            var avgSparkData     = new double[numDays + 1];

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
                        salesSparkData[idx]   = (double)day.Revenue;
                        profitSparkData[idx]  = (double)day.Profit;
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
        var products = await Task.Run(() => LoginRuntime.Products.GetAll().ToList());
        
        DispatcherQueue.TryEnqueue(() =>
        {
            var alertGreenBackground = new SolidColorBrush(Color.FromArgb(255, 232, 245, 233));
            var alertGreenForeground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
            var alertRedBackground = new SolidColorBrush(Color.FromArgb(255, 252, 232, 230));
            var alertRedForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
            var alertAmberBackground = new SolidColorBrush(Color.FromArgb(255, 255, 244, 229));
            var alertAmberForeground = new SolidColorBrush(Color.FromArgb(255, 239, 108, 0));

            DashboardAlertItem CreateAlertItem(string n, int c, SolidColorBrush aB, SolidColorBrush aF)
            {
                var isHealthy = c == 0;
                return new DashboardAlertItem { Name = n, CountText = c.ToString("N0"), BadgeBackground = isHealthy ? alertGreenBackground : aB, BadgeForeground = isHealthy ? alertGreenForeground : aF };
            }

            var shelfLowCount = products.Count(p => p.QuantityStore > 0 && p.QuantityStore <= p.MinThresholdStore);
            var warehouseLowCount = products.Count(p => p.QuantityWarehouse > 0 && p.QuantityWarehouse <= p.MinThresholdWarehouse);
            var shelfEmptyCount = products.Count(p => p.QuantityStore <= 0);
            var warehouseEmptyCount = products.Count(p => p.QuantityWarehouse <= 0);

            CriticalAlertsList.ItemsSource = new ObservableCollection<DashboardAlertItem>
            {
                CreateAlertItem("Shelf Low Stock", shelfLowCount, alertAmberBackground, alertAmberForeground),
                CreateAlertItem("Warehouse Low Stock", warehouseLowCount, alertAmberBackground, alertAmberForeground),
                CreateAlertItem("Shelf Empty", shelfEmptyCount, alertRedBackground, alertRedForeground),
                CreateAlertItem("Warehouse Empty", warehouseEmptyCount, alertRedBackground, alertRedForeground)
            };
            
            // Skeleton swap deferred to RevealAndAnimateDashboard

            var stockAmber = new SolidColorBrush(Color.FromArgb(255, 255, 143, 0));
            var stockRed = new SolidColorBrush(Color.FromArgb(255, 229, 57, 53));
            var stockNeutral = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163));

            var lowStockRows = products.Select(product =>
            {
                var displayName = string.IsNullOrWhiteSpace(product.Name) ? $"Product #{product.Id}" : product.Name.Trim();
                var shelfOut = product.QuantityStore <= 0;
                var warehouseOut = product.QuantityWarehouse <= 0;
                var shelfLow = product.QuantityStore > 0 && product.QuantityStore <= product.MinThresholdStore;
                var warehouseLow = product.QuantityWarehouse > 0 && product.QuantityWarehouse <= product.MinThresholdWarehouse;

                if (shelfOut && warehouseOut) return new { Item = new DashboardStockItem { Name = displayName, PrimaryLabelText = "Shelf ", PrimaryStatusText = "Empty", PrimaryStatusBrush = stockRed, SecondaryLabelText = "Warehouse ", SecondaryStatusText = "Empty", SecondaryStatusBrush = stockRed, SimpleStatusVisibility = Visibility.Collapsed, DetailedStatusVisibility = Visibility.Visible, SimpleBadgeTextVisibility = Visibility.Collapsed, BadgeVisible = Visibility.Visible }, SeverityRank = 0, RemainingQty = 0m, SortName = displayName };
                if (shelfOut || warehouseOut) return new { Item = new DashboardStockItem { Name = displayName, StockStatus = "Out of Stock", StatusBrush = stockRed, BadgeText = shelfOut ? "Shelf" : "Warehouse", BadgeVisible = Visibility.Visible }, SeverityRank = 1, RemainingQty = 0m, SortName = displayName };
                if (shelfLow && warehouseLow) return new { Item = new DashboardStockItem { Name = displayName, PrimaryLabelText = "Shelf ", PrimaryStatusText = $"{product.QuantityStore:N0} left", PrimaryStatusBrush = stockAmber, SecondaryLabelText = "Warehouse ", SecondaryStatusText = $"{product.QuantityWarehouse:N0} left", SecondaryStatusBrush = stockAmber, SimpleStatusVisibility = Visibility.Collapsed, DetailedStatusVisibility = Visibility.Visible, SimpleBadgeTextVisibility = Visibility.Collapsed, BadgeVisible = Visibility.Visible }, SeverityRank = 2, RemainingQty = Math.Max(0m, (decimal)(product.QuantityStore + product.QuantityWarehouse)), SortName = displayName };
                if (shelfLow || warehouseLow) return new { Item = new DashboardStockItem { Name = displayName, StockStatus = "Low Stock", StatusBrush = stockAmber, BadgeText = shelfLow ? "Shelf" : "Warehouse", BadgeVisible = Visibility.Visible }, SeverityRank = 3, RemainingQty = Math.Max(0m, (decimal)(shelfLow ? product.QuantityStore : product.QuantityWarehouse)), SortName = displayName };
                return null;
            }).Where(r => r != null).OrderBy(r => r.SeverityRank).ThenBy(r => r.RemainingQty).ThenBy(r => r.SortName).Take(8).Select(r => r.Item).ToList();

            LowStockList.ItemsSource = new ObservableCollection<DashboardStockItem>(lowStockRows.Count > 0 ? lowStockRows : new[] { new DashboardStockItem { Name = "All products are stocked", StockStatus = "Healthy", StatusBrush = alertGreenForeground, BadgeVisible = Visibility.Collapsed } });
            
            // Skeleton swap deferred to RevealAndAnimateDashboard

            int totalRows = products.Count;
            if (totalRows > 0)
            {
                int healthyCnt = totalRows - (shelfLowCount + warehouseLowCount + shelfEmptyCount + warehouseEmptyCount);
                if (healthyCnt < 0) healthyCnt = 0;
                int sumLow = shelfLowCount + warehouseLowCount;
                int sumOut = shelfEmptyCount + warehouseEmptyCount;

                double pctHealthy = (double)healthyCnt / totalRows;
                double pctLow = (double)sumLow / totalRows;
                double pctOut = (double)sumOut / totalRows;

                HealthyLegendText.Text = $"{(pctHealthy * 100):0}%  Healthy";
                LowStockLegendText.Text = $"{(pctLow * 100):0}%  Low Stock";
                OutOfStockLegendText.Text = $"{(pctOut * 100):0}%  Out of Stock";

                var colorHealthy = Color.FromArgb(255, 25, 118, 210);
                var colorLow = Color.FromArgb(255, 249, 168, 37);
                var colorOut = Color.FromArgb(255, 229, 57, 53);

                _donutSegments = new[] { (pctHealthy, colorHealthy), (pctLow, colorLow), (pctOut, colorOut) };
                DonutCenterText.Text = $"{(pctHealthy * 100):0}%";
            }
            // Skeleton swap deferred to RevealAndAnimateDashboard
        });
    }

    // ── Donut drawing engine ──────────────────────────────────────────────────
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

        if (string.IsNullOrWhiteSpace(centerLabel.Text))
        {
            centerLabel.Text = "0%";
        }
    }

    private static Microsoft.UI.Xaml.Shapes.Path CreateArcPath(
        double cx, double cy, double radius,
        double startDeg, double sweepDeg,
        Color color, double thickness)
    {
        double startRad = startDeg * Math.PI / 180.0;
        double endRad   = (startDeg + sweepDeg) * Math.PI / 180.0;

        var startPt = new Point(cx + radius * Math.Cos(startRad), cy + radius * Math.Sin(startRad));
        var endPt   = new Point(cx + radius * Math.Cos(endRad),   cy + radius * Math.Sin(endRad));

        var figure = new PathFigure
        {
            StartPoint = startPt,
            IsClosed   = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point          = endPt,
            Size           = new Size(radius, radius),
            IsLargeArc     = sweepDeg > 180,
            SweepDirection = SweepDirection.Clockwise,
            RotationAngle  = 0
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);

        return new Microsoft.UI.Xaml.Shapes.Path
        {
            Data            = geometry,
            Stroke          = new SolidColorBrush(color),
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Flat,
            StrokeEndLineCap   = PenLineCap.Flat,
            Fill            = null
        };
    }

    private void UpdateSalesBar(double todayPct, double yesterdayPct)
    {
        double containerWidth = SalesBarContainer.ActualWidth;
        if (containerWidth <= 0) return;

        // Animation for yesterday: 2 seconds
        var yesterdayAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = containerWidth * yesterdayPct,
            Duration = new Duration(TimeSpan.FromSeconds(2)),
            EnableDependentAnimation = true
        };

        // Animation for today: 3 seconds
        var todayAnim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
        {
            From = 0,
            To = containerWidth * todayPct,
            Duration = new Duration(TimeSpan.FromSeconds(3)),
            EnableDependentAnimation = true
        };

        var yesterdayStoryboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        yesterdayStoryboard.Children.Add(yesterdayAnim);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(yesterdayAnim, YesterdayBar);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(yesterdayAnim, "Width");
        
        var todayStoryboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
        todayStoryboard.Children.Add(todayAnim);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(todayAnim, TodayBar);
        Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(todayAnim, "Width");

        yesterdayStoryboard.Begin();
        todayStoryboard.Begin();
    }

    private static bool IsDiscountSaleItem(SaleItem item)
    {
        return item.LineTotal < 0
            && item.ProductId == 0
            && item.Name.StartsWith("Discount", StringComparison.OrdinalIgnoreCase);
    }

    // ── Shared TeachingTip for info icons ─────────────────────────────────────
    private FrameworkElement? _lastInfoTarget;

    private async void InfoIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;

        // If the same button is tapped again, just toggle it off
        if (CardInfoTip.IsOpen && _lastInfoTarget == btn)
        {
            CardInfoTip.IsOpen = false;
            _lastInfoTarget = null;
            return;
        }

        // Close the current tip first so the user sees a clean transition
        if (CardInfoTip.IsOpen)
        {
            CardInfoTip.IsOpen = false;
            await Task.Delay(200);
        }

        CardInfoTip.Target = btn;
        CardInfoTip.Subtitle = btn.Tag?.ToString() ?? string.Empty;
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
