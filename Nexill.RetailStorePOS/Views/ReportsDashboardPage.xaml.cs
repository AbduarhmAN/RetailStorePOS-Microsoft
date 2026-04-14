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
        new LineSeries<double> { Values = [0, 18, 8, 28, 18, 42, 35, 50], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(25, 118, 210)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(25, 118, 210, 40)), GeometrySize = 0 }
    ];

    public ISeries[] ProfitSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 14, 5, 22, 14, 38, 32, 45], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(67, 160, 71)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(67, 160, 71, 40)), GeometrySize = 0 }
    ];

    public ISeries[] InvoiceSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 24, 12, 32, 22, 44, 38, 50], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(25, 118, 210)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(25, 118, 210, 40)), GeometrySize = 0 }
    ];

    public ISeries[] AvgInvoiceSparkline { get; set; } = [
        new LineSeries<double> { Values = [0, 20, 10, 26, 16, 34, 28, 40], LineSmoothness = 0.1, Stroke = new SolidColorPaint(new SKColor(249, 168, 37)) { StrokeThickness = 2 }, Fill = new SolidColorPaint(new SKColor(249, 168, 37, 40)), GeometrySize = 0 }
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
        GreetingText.Text  = hour < 12 ? "Good morning, Store Manager"
                           : hour < 17 ? "Good afternoon, Store Manager"
                                       : "Good evening, Store Manager";
        TodayDateText.Text = DateTime.Now.ToString("MMMM dd, yyyy");
        
        // ── KPI CARDS (BACKGROUND SQLITE FETCH) ────────────
        var (todaySales, yesterdaySales) = await Task.Run(() => 
        {
            var today = LoginRuntime.Sales.GetSalesByDate(DateTime.Today).ToList();
            var yesterday = LoginRuntime.Sales.GetSalesByDate(DateTime.Today.AddDays(-1)).ToList();
            return (today, yesterday);
        });
        var products = await Task.Run(() => LoginRuntime.Products.GetAll().ToList());

        // Pacing Logic calculation
        var todayTotal = todaySales.Sum(s => s.Total);
        var currentTime = DateTime.Now.TimeOfDay;
        var pacingYesterdaySales = yesterdaySales.Where(s => s.CreatedAt.TimeOfDay <= currentTime).ToList();
        var yesterdayPacingTotal = pacingYesterdaySales.Sum(s => s.Total);

        SalesTodayText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayTotal);

        double salesDelta = 0;
        if (yesterdayPacingTotal > 0)
        {
            salesDelta = (double)((todayTotal - yesterdayPacingTotal) / yesterdayPacingTotal) * 100.0;
        }
        else if (todayTotal > 0)
        {
            salesDelta = 100.0; // Pacing way ahead (zero yesterday)
        }

        if (salesDelta >= 0)
        {
            SalesDeltaText.Text = $"+ {salesDelta:0.#}%  vs  yesterday";
            SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50)); // Green
        }
        else
        {
            SalesDeltaText.Text = $"- {Math.Abs(salesDelta):0.#}%  vs  yesterday";
            SalesDeltaText.Foreground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40)); // Red
        }

        // Live Sparkline Background Spark Generation for all 4 KPI cards
        var pastWeekSales = await Task.Run(() => LoginRuntime.Sales.GetSalesByDateRange(DateTime.Today.AddDays(-6), DateTime.Today).ToList());
        var numDays = 7;
        var salesSparkData   = new double[numDays + 1];
        var profitSparkData  = new double[numDays + 1];
        var invoiceSparkData = new double[numDays + 1];
        var avgSparkData     = new double[numDays + 1];

        // Anchor at zero
        salesSparkData[0] = profitSparkData[0] = invoiceSparkData[0] = avgSparkData[0] = 0;

        var startDate = DateTime.Today.AddDays(-6);
        foreach (var sale in pastWeekSales)
        {
            int dayOffset = (int)(sale.CreatedAt.Date - startDate).TotalDays;
            if (dayOffset >= 0 && dayOffset < numDays)
            {
                var idx = dayOffset + 1;
                salesSparkData[idx]   += (double)sale.Total;
                profitSparkData[idx]  += (double)(sale.Subtotal - sale.Items.Sum(i => i.ItemCost * i.Quantity));
                invoiceSparkData[idx] += 1;
            }
        }

        // Calculate avg invoice per day (avoid div by zero)
        for (int i = 1; i <= numDays; i++)
        {
            avgSparkData[i] = invoiceSparkData[i] > 0 ? salesSparkData[i] / invoiceSparkData[i] : 0;
        }

        if (SalesSparkline[0] is LineSeries<double> ls)
            ls.Values = new ObservableCollection<double>(salesSparkData);
        if (ProfitSparkline[0] is LineSeries<double> lp)
            lp.Values = new ObservableCollection<double>(profitSparkData);
        if (InvoiceSparkline[0] is LineSeries<double> li)
            li.Values = new ObservableCollection<double>(invoiceSparkData);
        if (AvgInvoiceSparkline[0] is LineSeries<double> la)
            la.Values = new ObservableCollection<double>(avgSparkData);

        // ── NET PROFIT (Subtotal - COGS, tax excluded) ─────
        var todayProfit = todaySales.Sum(s => s.Subtotal - s.Items.Sum(i => i.ItemCost * i.Quantity));

        ProfitTodayText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayProfit);

        // Show profit margin % instead of redundant "vs yesterday" delta
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

        // ── INVOICE COUNT ────────────────────────────────────
        var todayInvoiceCount = todaySales.Count;
        var yesterdayPacingInvoiceCount = pacingYesterdaySales.Count;

        InvoiceCountText.Text = todayInvoiceCount.ToString("N0");

        double invoiceDelta = 0;
        if (yesterdayPacingInvoiceCount > 0)
        {
            invoiceDelta = ((double)(todayInvoiceCount - yesterdayPacingInvoiceCount) / yesterdayPacingInvoiceCount) * 100.0;
        }
        else if (todayInvoiceCount > 0)
        {
            invoiceDelta = 100.0;
        }

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

        // ── AVG INVOICE VALUE + MEDIAN ───────────────────────
        var avgInvoice = todayInvoiceCount > 0 ? todayTotal / todayInvoiceCount : 0m;
        AvgInvoiceText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(avgInvoice);

        if (todayInvoiceCount > 0)
        {
            var sortedTotals = todaySales.Select(s => s.Total).OrderBy(t => t).ToList();
            var mid = sortedTotals.Count / 2;
            var median = sortedTotals.Count % 2 == 0
                ? (sortedTotals[mid - 1] + sortedTotals[mid]) / 2
                : sortedTotals[mid];
            MedianText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(median);
        }
        else
        {
            MedianText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(0m);
        }

        // ── SALES VS TARGET ────────────────────────────────
        const decimal dailyTargetAmount = 10000m;
        var yesterdayFullTotal = yesterdaySales.Sum(s => s.Total);
        var gapPct = dailyTargetAmount > 0
            ? (double)((todayTotal - dailyTargetAmount) / dailyTargetAmount) * 100.0
            : 0;

        CurrentSalesText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayTotal);
        SalesGapText.Text = $"{gapPct:+0.#;-0.#}% vs target";
        SalesGapText.Foreground = gapPct >= 0
            ? new SolidColorBrush(Color.FromArgb(255, 46, 125, 50))
            : new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
        TargetText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(dailyTargetAmount);
        TargetSubText.Text = $"YESTERDAY: {CurrencyDisplayHelper.FormatConfiguredAmount(yesterdayFullTotal)}  ·  TARGET: {CurrencyDisplayHelper.FormatConfiguredAmount(dailyTargetAmount)}";

        var todayBarPct = dailyTargetAmount > 0
            ? Math.Min(1.0, Math.Max(0, (double)(todayTotal / dailyTargetAmount)))
            : 0;
        var yesterdayBarPct = dailyTargetAmount > 0
            ? Math.Min(1.0, Math.Max(0, (double)(yesterdayFullTotal / dailyTargetAmount)))
            : 0;

        // Wire bar widths when the container is measured
        SalesBarContainer.SizeChanged += (_, _) => UpdateSalesBar(todayPct: todayBarPct, yesterdayPct: yesterdayBarPct);
        UpdateSalesBar(todayPct: todayBarPct, yesterdayPct: yesterdayBarPct);

        // ── DISCOUNTS ──────────────────────────────────────
        var todayDiscountItems = todaySales
            .SelectMany(sale => sale.Items)
            .Where(IsDiscountSaleItem)
            .ToList();
        var yesterdayDiscountItems = yesterdaySales
            .SelectMany(sale => sale.Items)
            .Where(IsDiscountSaleItem)
            .ToList();

        var todayDiscountAmount = todayDiscountItems.Sum(item => Math.Abs(item.LineTotal));
        var yesterdayDiscountAmount = yesterdayDiscountItems.Sum(item => Math.Abs(item.LineTotal));
        var discountsDeltaPct = yesterdayDiscountAmount > 0
            ? (double)((todayDiscountAmount - yesterdayDiscountAmount) / yesterdayDiscountAmount) * 100.0
            : todayDiscountAmount > 0
                ? 100.0
                : 0.0;

        DiscountsTotalText.Text = CurrencyDisplayHelper.FormatConfiguredAmount(todayDiscountAmount);
        DiscountsDeltaText.Text = discountsDeltaPct == 0
            ? "0%"
            : $"{discountsDeltaPct:+0.#;-0.#}%";
        DiscountsCountText.Text = $"{todayDiscountItems.Count} Discounts Applied";

        // ── CRITICAL ALERTS ───────────────────────────────
        var alertGreenBackground = new SolidColorBrush(Color.FromArgb(255, 232, 245, 233));
        var alertGreenForeground = new SolidColorBrush(Color.FromArgb(255, 46, 125, 50));
        var alertRedBackground = new SolidColorBrush(Color.FromArgb(255, 252, 232, 230));
        var alertRedForeground = new SolidColorBrush(Color.FromArgb(255, 198, 40, 40));
        var alertAmberBackground = new SolidColorBrush(Color.FromArgb(255, 255, 244, 229));
        var alertAmberForeground = new SolidColorBrush(Color.FromArgb(255, 239, 108, 0));

        DashboardAlertItem CreateAlertItem(
            string name,
            int count,
            SolidColorBrush activeBackground,
            SolidColorBrush activeForeground)
        {
            var isHealthy = count == 0;

            return new DashboardAlertItem
            {
                Name = name,
                CountText = count.ToString("N0"),
                BadgeBackground = isHealthy ? alertGreenBackground : activeBackground,
                BadgeForeground = isHealthy ? alertGreenForeground : activeForeground
            };
        }

        var shelfLowCount = products.Count(product => product.QuantityStore > 0 && product.QuantityStore <= product.MinThresholdStore);
        var warehouseLowCount = products.Count(product => product.QuantityWarehouse > 0 && product.QuantityWarehouse <= product.MinThresholdWarehouse);
        var shelfEmptyCount = products.Count(product => product.QuantityStore <= 0);
        var warehouseEmptyCount = products.Count(product => product.QuantityWarehouse <= 0);

        CriticalAlertsList.ItemsSource = new ObservableCollection<DashboardAlertItem>
        {
            CreateAlertItem("Shelf Low Stock", shelfLowCount, alertAmberBackground, alertAmberForeground),
            CreateAlertItem("Warehouse Low Stock", warehouseLowCount, alertAmberBackground, alertAmberForeground),
            CreateAlertItem("Shelf Empty", shelfEmptyCount, alertRedBackground, alertRedForeground),
            CreateAlertItem("Warehouse Empty", warehouseEmptyCount, alertRedBackground, alertRedForeground)
        };

        // ── TOP PRODUCTS ───────────────────────────────────
        var topProducts = todaySales
            .SelectMany(sale => sale.Items)
            .Where(item => item.Quantity > 0 && item.LineTotal > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(
                item => new
                {
                    item.ProductId,
                    Name = item.Name.Trim()
                })
            .Select(group => new
            {
                group.Key.Name,
                Revenue = group.Sum(item => item.LineTotal),
                Quantity = group.Sum(item => item.Quantity)
            })
            .OrderByDescending(item => item.Revenue)
            .ThenByDescending(item => item.Quantity)
            .ThenBy(item => item.Name)
            .Take(5)
            .ToList();

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
                : new[]
                {
                    new DashboardProductItem
                    {
                        Name = "No products sold today",
                        Revenue = CurrencyDisplayHelper.FormatConfiguredAmount(0m),
                        Pct = 0
                    }
                });

        // ── LOW STOCK LIST ──────────────────────────────────
        var stockAmber = new SolidColorBrush(Color.FromArgb(255, 255, 143, 0));
        var stockRed = new SolidColorBrush(Color.FromArgb(255, 229, 57, 53));
        var stockNeutral = new SolidColorBrush(Color.FromArgb(255, 143, 150, 163));

        var lowStockRows = products
            .Select(product =>
            {
                var displayName = string.IsNullOrWhiteSpace(product.Name)
                    ? $"Product #{product.Id}"
                    : product.Name.Trim();

                var shelfOut = product.QuantityStore <= 0;
                var warehouseOut = product.QuantityWarehouse <= 0;
                var shelfLow = product.QuantityStore > 0 && product.QuantityStore <= product.MinThresholdStore;
                var warehouseLow = product.QuantityWarehouse > 0 && product.QuantityWarehouse <= product.MinThresholdWarehouse;

                if (shelfOut && warehouseOut)
                {
                    return new
                    {
                        Item = new DashboardStockItem
                        {
                            Name = displayName,
                            PrimaryLabelText = "Shelf ",
                            PrimaryStatusText = "Empty",
                            PrimaryStatusBrush = stockRed,
                            SecondaryLabelText = "Warehouse ",
                            SecondaryStatusText = "Empty",
                            SecondaryStatusBrush = stockRed,
                            SimpleStatusVisibility = Visibility.Collapsed,
                            DetailedStatusVisibility = Visibility.Visible,
                            SimpleBadgeTextVisibility = Visibility.Collapsed,
                            BadgeVisible = Visibility.Visible
                        },
                        SeverityRank = 0,
                        RemainingQty = 0m,
                        SortName = displayName
                    };
                }

                if (shelfOut || warehouseOut)
                {
                    return new
                    {
                        Item = new DashboardStockItem
                        {
                            Name = displayName,
                            StockStatus = "Out of Stock",
                            StatusBrush = stockRed,
                            BadgeText = shelfOut ? "Shelf" : "Warehouse",
                            BadgeVisible = Visibility.Visible
                        },
                        SeverityRank = 1,
                        RemainingQty = 0m,
                        SortName = displayName
                    };
                }

                if (shelfLow && warehouseLow)
                {
                    return new
                    {
                        Item = new DashboardStockItem
                        {
                            Name = displayName,
                            PrimaryLabelText = "Shelf ",
                            PrimaryStatusText = $"{product.QuantityStore:N0} left",
                            PrimaryStatusBrush = stockAmber,
                            SecondaryLabelText = "Warehouse ",
                            SecondaryStatusText = $"{product.QuantityWarehouse:N0} left",
                            SecondaryStatusBrush = stockAmber,
                            SimpleStatusVisibility = Visibility.Collapsed,
                            DetailedStatusVisibility = Visibility.Visible,
                            SimpleBadgeTextVisibility = Visibility.Collapsed,
                            BadgeVisible = Visibility.Visible
                        },
                        SeverityRank = 2,
                        RemainingQty = product.QuantityStore + product.QuantityWarehouse,
                        SortName = displayName
                    };
                }

                if (shelfLow || warehouseLow)
                {
                    var isShelfIssue = shelfLow;
                    var remainingQty = isShelfIssue ? product.QuantityStore : product.QuantityWarehouse;

                    return new
                    {
                        Item = new DashboardStockItem
                        {
                            Name = displayName,
                            StockStatus = $"{remainingQty:N0} left",
                            StatusBrush = stockAmber,
                            BadgeText = isShelfIssue ? "Shelf" : "Warehouse",
                            BadgeVisible = Visibility.Visible
                        },
                        SeverityRank = 3,
                        RemainingQty = remainingQty,
                        SortName = displayName
                    };
                }

                return null;
            })
            .Where(row => row is not null)
            .OrderBy(row => row!.SeverityRank)
            .ThenBy(row => row!.RemainingQty)
            .ThenBy(row => row!.SortName, StringComparer.CurrentCultureIgnoreCase)
            .Take(5)
            .Select(row => row!.Item)
            .ToList();

        LowStockList.ItemsSource = new ObservableCollection<DashboardStockItem>(
            lowStockRows.Count > 0
                ? lowStockRows
                : new[]
                {
                    new DashboardStockItem
                    {
                        Name = "Inventory is healthy",
                        StockStatus = "No low or out-of-stock items",
                        StatusBrush = stockNeutral,
                        BadgeVisible = Visibility.Collapsed
                    }
                });

        // ── DONUT CHART ────────────────────────────────────
        var totalProducts = products.Count;
        var outOfStockCount = products.Count(product => product.QuantityStore <= 0 && product.QuantityWarehouse <= 0);
        var lowStockCount = products.Count(product =>
            !(product.QuantityStore <= 0 && product.QuantityWarehouse <= 0) &&
            (product.QuantityStore <= product.MinThresholdStore || product.QuantityWarehouse <= product.MinThresholdWarehouse));
        var healthyCount = Math.Max(0, totalProducts - lowStockCount - outOfStockCount);

        var healthyPct = totalProducts > 0 ? (double)healthyCount / totalProducts : 0.0;
        var lowPct = totalProducts > 0 ? (double)lowStockCount / totalProducts : 0.0;
        var outPct = totalProducts > 0 ? (double)outOfStockCount / totalProducts : 0.0;
        var healthyColor = Color.FromArgb(255, 25, 118, 210);
        var lowColor = Color.FromArgb(255, 249, 168, 37);
        var outColor = Color.FromArgb(255, 229, 57, 53);

        var dominantSegment = new[]
        {
            new { Label = "Healthy", Pct = healthyPct, Color = healthyColor, SeverityRank = 0 },
            new { Label = "Low Stock", Pct = lowPct, Color = lowColor, SeverityRank = 1 },
            new { Label = "Out of Stock", Pct = outPct, Color = outColor, SeverityRank = 2 }
        }
        .OrderByDescending(segment => segment.Pct)
        .ThenByDescending(segment => segment.SeverityRank)
        .First();

        DonutCenterText.Text = $"{dominantSegment.Pct * 100:0}%";
        DonutCenterText.Foreground = new SolidColorBrush(dominantSegment.Color);
        HealthyLegendText.Text = $"{healthyPct * 100:0}%  Healthy";
        LowStockLegendText.Text = $"{lowPct * 100:0}%  Low Stock";
        OutOfStockLegendText.Text = $"{outPct * 100:0}%  Out of Stock";

        DrawDonutChart(
            canvas: StockHealthCanvas,
            centerLabel: DonutCenterText,
            segments: new[]
            {
                (healthyPct, healthyColor),
                (lowPct, lowColor),
                (outPct, outColor),
            },
            canvasSize: 140,
            radius: 50,
            thickness: 24
        );
    }

    private void SetDashboardLoadingState(bool isLoading)
    {
        DashboardLoadingRoot.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        DashboardContentRoot.Visibility = isLoading ? Visibility.Collapsed : Visibility.Visible;

        if (isLoading)
        {
            StartSkeletonShimmer();
        }
        else
        {
            StopSkeletonShimmer();
        }
    }

    private void StartSkeletonShimmer()
    {
        if (_skeletonShimmerBrush is null)
        {
            return;
        }

        _skeletonPhase = -0.35;
        AdvanceSkeletonShimmer();

        if (!_skeletonTimer.IsEnabled)
        {
            _skeletonTimer.Start();
        }
    }

    private void StopSkeletonShimmer()
    {
        if (_skeletonTimer.IsEnabled)
        {
            _skeletonTimer.Stop();
        }
    }

    private void SkeletonTimer_Tick(object? sender, object e)
    {
        AdvanceSkeletonShimmer();
    }

    private void AdvanceSkeletonShimmer()
    {
        if (_skeletonShimmerBrush is null || _skeletonShimmerBrush.GradientStops.Count < 5)
        {
            return;
        }

        _skeletonPhase += 0.04;
        if (_skeletonPhase > 1.0)
        {
            _skeletonPhase = -0.35;
        }

        var lead = Math.Clamp(_skeletonPhase, 0.0, 1.0);
        var highlight = Math.Clamp(_skeletonPhase + 0.14, 0.0, 1.0);
        var tail = Math.Clamp(_skeletonPhase + 0.28, 0.0, 1.0);

        _skeletonShimmerBrush.GradientStops[0].Offset = 0;
        _skeletonShimmerBrush.GradientStops[1].Offset = lead;
        _skeletonShimmerBrush.GradientStops[2].Offset = highlight;
        _skeletonShimmerBrush.GradientStops[3].Offset = tail;
        _skeletonShimmerBrush.GradientStops[4].Offset = 1;
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

        YesterdayBar.Width = containerWidth * yesterdayPct;
        TodayBar.Width = containerWidth * todayPct;
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
}
