
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiveChartsCore;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.Data.Modules.Reporting;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Views.Components;
using SkiaSharp;

namespace RetailStorePOS.UI.Reporting.Views.AdvancedReports;

/// <summary>
/// Period-over-period revenue intelligence. Page-as-ViewModel pattern matches
/// the rest of the codebase (see <see cref="ReportsDashboardPage"/>).
/// </summary>
public sealed partial class RevenueDashboardPage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly SolidColorBrush DeltaUpBrush = new(ColorHelper.FromArgb(0xFF, 0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush DeltaDownBrush = new(ColorHelper.FromArgb(0xFF, 0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush DeltaFlatBrush = new(ColorHelper.FromArgb(0xFF, 0x8F, 0x96, 0xA3));

    // Profit-line brushes for the Top Products card. Reuses the same green/red
    // tone as the KPI deltas so the dashboard reads consistently.
    private static readonly SolidColorBrush ProfitPositiveBrush = DeltaUpBrush;
    private static readonly SolidColorBrush ProfitNegativeBrush = DeltaDownBrush;

    private static readonly SKColor RevenueColor = new(0x6D, 0x28, 0xD9);
    private static readonly SKColor ProfitColor = new(0x22, 0xC5, 0x5E);

    private CancellationTokenSource? _refreshCts;

    // ---- Skeleton shimmer animation ----------------------------------------
    private readonly DispatcherTimer _shimmerTimer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private LinearGradientBrush? _shimmerBrush;
    private double _shimmerPhase = -0.35;

    public RevenueDashboardPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        _shimmerBrush = Resources["RD_ShimmerBrush"] as LinearGradientBrush;
        _shimmerTimer.Tick += ShimmerTimer_Tick;
        Loaded += RevenueDashboardPage_Loaded;
        Unloaded += RevenueDashboardPage_Unloaded;
    }

    private void ShimmerTimer_Tick(object? sender, object e)
    {
        if (_shimmerBrush is null || _shimmerBrush.GradientStops.Count < 5)
        {
            return;
        }

        _shimmerPhase += 0.02;
        if (_shimmerPhase > 1.35) _shimmerPhase = -0.35;

        // 5-stop gradient sweeping a brighter band across a darker base.
        _shimmerBrush.GradientStops[0].Offset = Math.Clamp(_shimmerPhase - 0.30, 0, 1);
        _shimmerBrush.GradientStops[1].Offset = Math.Clamp(_shimmerPhase - 0.10, 0, 1);
        _shimmerBrush.GradientStops[2].Offset = Math.Clamp(_shimmerPhase,        0, 1);
        _shimmerBrush.GradientStops[3].Offset = Math.Clamp(_shimmerPhase + 0.10, 0, 1);
        _shimmerBrush.GradientStops[4].Offset = Math.Clamp(_shimmerPhase + 0.30, 0, 1);
    }

    private void StartShimmer()
    {
        if (!_shimmerTimer.IsEnabled) _shimmerTimer.Start();
    }

    private void StopShimmer()
    {
        if (_shimmerTimer.IsEnabled) _shimmerTimer.Stop();
    }

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnChanged(nameof(IsLoading));
            OnChanged(nameof(SkeletonVisibility));
            OnChanged(nameof(ContentVisibility));
        }
    }

    public Visibility SkeletonVisibility => _isLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ContentVisibility => _isLoading ? Visibility.Collapsed : Visibility.Visible;

    private void HideSkeletons()
    {
        if (!_isLoading) return;
        IsLoading = false;
        StopShimmer();
    }

    private void RevenueDashboardPage_Loaded(object sender, RoutedEventArgs e)
    {
        PeriodFilter.PeriodChanged += PeriodFilter_PeriodChanged;
        // AOT: mirror TopProducts into the ListView from code-behind (x:Bind ItemsSource crashes under Native AOT).
        TopProducts.CollectionChanged -= TopProducts_CollectionChanged;
        TopProducts.CollectionChanged += TopProducts_CollectionChanged;
        ItemsControlSync.Reset(TopProducts, TopProductsListView.Items);
        StartShimmer();
        // Defer the initial load to the next dispatcher cycle so the page
        // paints its first frame BEFORE any work begins. Without this, even
        // the tiny snapshot file read on the UI thread can stall the
        // navigation animation visibly.
        DispatcherQueue.TryEnqueue(QueueReload);
    }

    private void RevenueDashboardPage_Unloaded(object sender, RoutedEventArgs e)
    {
        PeriodFilter.PeriodChanged -= PeriodFilter_PeriodChanged;
        TopProducts.CollectionChanged -= TopProducts_CollectionChanged;
        StopShimmer();
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private void TopProducts_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => DispatcherQueue.TryEnqueue(() => ItemsControlSync.Apply(TopProducts, TopProductsListView.Items, e));

    private void PeriodFilter_PeriodChanged(object? sender, PeriodChangedEventArgs e)
    {
        // Same deferral on user-initiated period changes - period buttons
        // stay instantly responsive while data refreshes in the background.
        DispatcherQueue.TryEnqueue(QueueReload);
    }

    private void QueueReload()
        => _ = ReloadSafelyAsync();

    private async Task ReloadSafelyAsync()
    {
        try
        {
            await ReloadAsync();
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer refresh before ReloadAsync reached its guarded section.
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"Revenue dashboard reload failed before guarded refresh: {ex}");
            SetStatusBanner(string.Format(
                CultureInfo.CurrentCulture,
                LocalizationHelper.GetString(
                    "RevenueDashboard_Status_RefreshFailed",
                    fallback: "Could not refresh analytics: {0}"),
                ex.Message));
            HideSkeletons();
        }
    }

    private async Task ReloadAsync()
    {
        // Cancel any in-flight refresh; only the latest period selection matters.
        _refreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var token = cts.Token;

        var startUtc = PeriodFilter.StartUtc;
        var endUtc = PeriodFilter.EndUtc;
        var (prevStart, prevEnd) = PeriodFilter.GetPreviousPeriod();
        var periodKey = BuildPeriodKey(PeriodFilter.ActivePreset, startUtc, endUtc);

        SetStatusBanner(null);

        // -----------------------------------------------------------------
        // 1. Fast path: try a signed local snapshot for this period.
        //    Run on a background thread so file I/O + JSON deserialize +
        //    HMAC verify never block the UI thread. Apply on UI thread when
        //    it lands.
        // -----------------------------------------------------------------
        var snapshotStore = LoginRuntime.AdvancedReportsSnapshots;
        var maxAge = TimeSpan.FromMinutes(5);
        var cached = await Task.Run(
            () => TryLoadSnapshotSafe(snapshotStore, periodKey, maxAge),
            token).ConfigureAwait(true);

        if (cached is not null && !token.IsCancellationRequested)
        {
            ApplySnapshot(cached, startUtc, endUtc);
            HideSkeletons();
        }

        // -----------------------------------------------------------------
        // 2. Always run a live query in the background to refresh + persist
        //    a new snapshot. If the cache hit was very recent and the period
        //    has not advanced, the live result will still be applied to keep
        //    the UI authoritative.
        // -----------------------------------------------------------------
        try
        {
            var query = LoginRuntime.AdvancedReports;

            var result = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var current = query.GetKpiSnapshot(startUtc, endUtc);
                token.ThrowIfCancellationRequested();
                var previous = query.GetKpiSnapshot(prevStart, prevEnd);
                token.ThrowIfCancellationRequested();
                var byDay = query.GetRevenueByDay(startUtc, endUtc);
                token.ThrowIfCancellationRequested();
                var top = query.GetTopProducts(startUtc, endUtc, 5);
                token.ThrowIfCancellationRequested();
                var busiest = query.GetBusiestHourAndDay(startUtc, endUtc);
                return (current, previous, byDay, top, busiest);
            }, token).ConfigureAwait(true);

            if (token.IsCancellationRequested)
            {
                return;
            }

            ApplyKpis(result.current, result.previous);
            ApplyTrend(result.byDay, startUtc, endUtc);
            ApplyTopProducts(result.top);
            ApplyBusiest(result.busiest);
            HideSkeletons();

            // Persist a fresh snapshot in the background. The UI is already
            // populated from the live query; log failures for AOT/runtime
            // diagnostics without blocking the page.
            _ = Task.Run(() =>
            {
                try
                {
                    var snapshot = BuildSnapshot(
                        periodKey, startUtc, endUtc,
                        result.current, result.previous,
                        result.byDay, result.top, result.busiest);
                    snapshotStore.SaveSnapshot(snapshot);
                }
                catch (Exception ex)
                {
                    StartupTrace.Write($"Revenue dashboard snapshot save failed: {ex}");
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer refresh; ignore.
        }
        catch (Exception ex)
        {
            // Live query failed. If we already painted from cache earlier,
            // keep showing it; only blank the UI when no cache was applied.
            SetStatusBanner(string.Format(
                CultureInfo.CurrentCulture,
                LocalizationHelper.GetString(
                    "RevenueDashboard_Status_RefreshFailed",
                    fallback: "Could not refresh analytics: {0}"),
                ex.Message));
            if (cached is null)
            {
                ApplyKpis(default, default);
                ApplyTrend(new List<DailyRevenuePoint>(), startUtc, endUtc);
                ApplyTopProducts(new List<TopProductRow>());
                ApplyBusiest(new BusiestPeriodSnapshot(null, 0, null, 0));
            }
            HideSkeletons();
        }
    }

    private static string BuildPeriodKey(PeriodFilterControl.PeriodPreset preset, DateTime startUtc, DateTime endUtc)
    {
        if (preset == PeriodFilterControl.PeriodPreset.Custom)
        {
            return $"custom-{startUtc:yyyyMMdd}-{endUtc:yyyyMMdd}";
        }
        return preset.ToString();
    }

    private static AdvancedReportsSnapshot? TryLoadSnapshotSafe(
        AdvancedReportsSnapshotStore store,
        string periodKey,
        TimeSpan maxAge)
    {
        try
        {
            return store.LoadSnapshotIfFresh(periodKey, maxAge);
        }
        catch
        {
            return null;
        }
    }

    private void ApplySnapshot(AdvancedReportsSnapshot snapshot, DateTime startUtc, DateTime endUtc)
    {
        var p = snapshot.Payload;
        var current = new KpiSnapshot(
            p.TotalRevenue, p.GrossProfit, p.TransactionCount,
            p.AverageTransactionValue, p.ProfitMarginPercent);
        var previous = new KpiSnapshot(
            p.PreviousRevenue, p.PreviousProfit, p.PreviousTransactionCount,
            p.PreviousAverageTransactionValue, 0m);

        ApplyKpis(current, previous);

        var byDay = new List<DailyRevenuePoint>(p.DailyTrend.Count);
        foreach (var d in p.DailyTrend)
        {
            byDay.Add(new DailyRevenuePoint(d.Day, d.Revenue, d.Profit, d.InvoiceCount));
        }
        ApplyTrend(byDay, startUtc, endUtc);

        var top = new List<TopProductRow>(p.TopProducts.Count);
        foreach (var t in p.TopProducts)
        {
            top.Add(new TopProductRow(t.ProductId, t.Name, t.Revenue, t.Units, t.Profit));
        }
        ApplyTopProducts(top);

        ApplyBusiest(new BusiestPeriodSnapshot(
            p.BusiestHour, p.BusiestHourTransactionCount,
            p.BusiestDayOfWeek, p.BusiestDayOfWeekTransactionCount));
    }

    private static AdvancedReportsSnapshot BuildSnapshot(
        string periodKey,
        DateTime startUtc,
        DateTime endUtc,
        KpiSnapshot current,
        KpiSnapshot previous,
        List<DailyRevenuePoint> byDay,
        List<TopProductRow> top,
        BusiestPeriodSnapshot busiest)
    {
        var payload = new AdvancedReportsSnapshotPayload
        {
            TotalRevenue = current.TotalRevenue,
            GrossProfit = current.GrossProfit,
            TransactionCount = current.TransactionCount,
            AverageTransactionValue = current.AverageTransactionValue,
            ProfitMarginPercent = current.ProfitMarginPercent,
            PreviousRevenue = previous.TotalRevenue,
            PreviousProfit = previous.GrossProfit,
            PreviousTransactionCount = previous.TransactionCount,
            PreviousAverageTransactionValue = previous.AverageTransactionValue,
            BusiestHour = busiest.PeakHour,
            BusiestHourTransactionCount = busiest.PeakHourTransactionCount,
            BusiestDayOfWeek = busiest.PeakDayOfWeek,
            BusiestDayOfWeekTransactionCount = busiest.PeakDayOfWeekTransactionCount
        };

        foreach (var d in byDay)
        {
            payload.DailyTrend.Add(new DailyRevenueEntry
            {
                Day = d.Day, Revenue = d.Revenue, Profit = d.Profit, InvoiceCount = d.InvoiceCount
            });
        }

        foreach (var t in top)
        {
            payload.TopProducts.Add(new TopProductSnapshotEntry
            {
                ProductId = t.ProductId, Name = t.Name,
                Revenue = t.Revenue, Units = t.Units, Profit = t.Profit
            });
        }

        return new AdvancedReportsSnapshot
        {
            SchemaVersion = AdvancedReportsSnapshot.CurrentSchemaVersion,
            PeriodKey = periodKey,
            CapturedAtUtc = DateTime.UtcNow,
            PeriodStartUtc = startUtc,
            PeriodEndUtc = endUtc,
            Payload = payload
        };
    }

    // -----------------------------------------------------------------
    // KPI tiles + deltas
    // -----------------------------------------------------------------

    private void ApplyKpis(KpiSnapshot? currentNullable, KpiSnapshot? previousNullable)
    {
        var current = currentNullable ?? new KpiSnapshot(0m, 0m, 0, 0m, 0m);
        var previous = previousNullable ?? new KpiSnapshot(0m, 0m, 0, 0m, 0m);

        TotalRevenueText = CurrencyDisplayHelper.FormatConfiguredAmount(current.TotalRevenue);
        GrossProfitText = CurrencyDisplayHelper.FormatConfiguredAmount(current.GrossProfit);
        TransactionCountText = current.TransactionCount.ToString("N0", CultureInfo.CurrentCulture);
        AverageTransactionText = CurrencyDisplayHelper.FormatConfiguredAmount(current.AverageTransactionValue);

        var revDelta = ComputePercentDelta(previous.TotalRevenue, current.TotalRevenue);
        RevenueDeltaText = FormatDelta(revDelta);
        RevenueDeltaBrush = BrushForDelta(revDelta);

        var profitDelta = ComputePercentDelta(previous.GrossProfit, current.GrossProfit);
        ProfitDeltaText = FormatDelta(profitDelta);
        ProfitDeltaBrush = BrushForDelta(profitDelta);

        var txDelta = ComputeAbsoluteDelta(previous.TransactionCount, current.TransactionCount);
        TransactionsDeltaText = FormatTransactionDelta(txDelta);
        TransactionsDeltaBrush = BrushForDelta(txDelta);

        var avgDelta = ComputePercentDelta(previous.AverageTransactionValue, current.AverageTransactionValue);
        AverageDeltaText = FormatDelta(avgDelta);
        AverageDeltaBrush = BrushForDelta(avgDelta);
    }

    private static decimal? ComputePercentDelta(decimal previous, decimal current)
    {
        if (previous == 0m && current == 0m)
        {
            return 0m;
        }

        if (previous == 0m)
        {
            return null;
        }

        return Math.Round((current - previous) / previous * 100m, 1);
    }

    private static int ComputeAbsoluteDelta(int previous, int current) => current - previous;

    private static string FormatDelta(decimal? delta)
    {
        if (delta is null)
        {
            return "—";
        }

        var arrow = delta > 0 ? "▲" : delta < 0 ? "▼" : "•";
        var template = LocalizationHelper.GetString(
            "RevenueDashboard_Delta_Format",
            fallback: "{0} {1:0.0}% vs previous");
        return string.Format(CultureInfo.CurrentCulture, template, arrow, Math.Abs(delta.Value));
    }

    private static string FormatTransactionDelta(int delta)
    {
        var arrow = delta > 0 ? "▲" : delta < 0 ? "▼" : "•";
        var template = LocalizationHelper.GetString(
            "RevenueDashboard_DeltaCount_Format",
            fallback: "{0} {1:N0} vs previous");
        return string.Format(CultureInfo.CurrentCulture, template, arrow, Math.Abs(delta));
    }

    private static SolidColorBrush BrushForDelta(decimal? delta)
    {
        if (delta is null) return DeltaFlatBrush;
        if (delta.Value > 0) return DeltaUpBrush;
        if (delta.Value < 0) return DeltaDownBrush;
        return DeltaFlatBrush;
    }

    private static SolidColorBrush BrushForDelta(int delta)
    {
        if (delta > 0) return DeltaUpBrush;
        if (delta < 0) return DeltaDownBrush;
        return DeltaFlatBrush;
    }

    // -----------------------------------------------------------------
    // Trend chart
    // -----------------------------------------------------------------

    private void ApplyTrend(List<DailyRevenuePoint> points, DateTime startUtc, DateTime endUtc)
    {
        var dense = DensifyByDay(points, startUtc, endUtc);
        var revenueValues = dense.Select(p => (double)p.Revenue).ToArray();
        var profitValues = dense.Select(p => (double)p.Profit).ToArray();

        // X-axis labels: convert ISO yyyy-MM-dd strings into short localized
        // dates (e.g. "May 12" in en-US). Falls back to the
        // raw ISO string when parsing fails so a malformed row still renders.
        var shortDateFormat = LocalizationHelper.GetString(
            "RevenueDashboard_Chart_AxisDateFormat",
            fallback: "MMM d");
        var labels = dense.Select(p => FormatDayLabel(p.Day, shortDateFormat)).ToArray();

        TrendSeries = new ISeries[]
        {
            new LineSeries<double>
            {
                Name = LocalizationHelper.GetString("RevenueDashboard_Chart_Revenue", fallback: "Revenue"),
                Values = revenueValues,
                Stroke = new SolidColorPaint(RevenueColor) { StrokeThickness = 2.5f },
                GeometryStroke = new SolidColorPaint(RevenueColor) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 6,
                Fill = new SolidColorPaint(RevenueColor.WithAlpha(40))
            },
            new LineSeries<double>
            {
                Name = LocalizationHelper.GetString("RevenueDashboard_Chart_Profit", fallback: "Profit"),
                Values = profitValues,
                Stroke = new SolidColorPaint(ProfitColor) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(ProfitColor) { StrokeThickness = 2 },
                GeometryFill = new SolidColorPaint(SKColors.White),
                GeometrySize = 5,
                Fill = null
            }
        };

        TrendXAxes = new ICartesianAxis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsRotation = labels.Length > 14 ? 35 : 0,
                TextSize = 11,
                LabelsPaint = new SolidColorPaint(new SKColor(0x8F, 0x96, 0xA3)),
                SeparatorsPaint = null
            }
        };

        TrendYAxes = new ICartesianAxis[]
        {
            new Axis
            {
                MinLimit = 0,
                TextSize = 11,
                // Format Y-axis tick labels as the configured currency. The
                // Labeler runs on every redraw so it picks up locale changes
                // automatically. Compact-form (1.5K / 1.2M) keeps the axis
                // readable when amounts grow large.
                Labeler = FormatCurrencyAxisLabel,
                LabelsPaint = new SolidColorPaint(new SKColor(0x8F, 0x96, 0xA3)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(0xEE, 0xF2, 0xF6)) { StrokeThickness = 1 }
            }
        };
    }

    private static string FormatDayLabel(string isoDay, string shortDateFormat)
    {
        if (DateTime.TryParseExact(
                isoDay,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed.ToString(shortDateFormat, CultureInfo.CurrentCulture);
        }

        return isoDay;
    }

    private static string FormatCurrencyAxisLabel(double value)
    {
        // For very large values, fall back to compact "1.2K / 3.4M" style so
        // the axis does not stack 7-character labels on top of each other.
        var absolute = Math.Abs(value);
        if (absolute >= 1_000_000)
        {
            return CurrencyDisplayHelper.FormatConfiguredAmount((decimal)(value / 1_000_000)) + "M";
        }
        if (absolute >= 1_000)
        {
            return CurrencyDisplayHelper.FormatConfiguredAmount((decimal)(value / 1_000)) + "K";
        }

        return CurrencyDisplayHelper.FormatConfiguredAmount((decimal)value);
    }

    private static List<DailyRevenuePoint> DensifyByDay(
        IReadOnlyList<DailyRevenuePoint> sparse,
        DateTime startUtc,
        DateTime endUtc)
    {
        var byDay = sparse.ToDictionary(p => p.Day, p => p, StringComparer.Ordinal);
        var dense = new List<DailyRevenuePoint>();
        for (var d = startUtc.Date; d < endUtc.Date; d = d.AddDays(1))
        {
            var key = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (byDay.TryGetValue(key, out var point))
            {
                dense.Add(point);
            }
            else
            {
                dense.Add(new DailyRevenuePoint(key, 0m, 0m, 0));
            }
        }

        return dense;
    }

    // -----------------------------------------------------------------
    // Top products
    // -----------------------------------------------------------------

    private void ApplyTopProducts(List<TopProductRow> rows)
    {
        TopProducts.Clear();
        var rank = 1;
        foreach (var row in rows)
        {
            var profitFormatted = CurrencyDisplayHelper.FormatConfiguredAmount(Math.Abs(row.Profit));
            var isLoss = row.Profit < 0m;
            // Localized "+ X profit" or "- X loss" — keys fall back to English
            // when a translation is missing.
            var profitTemplateKey = isLoss
                ? "RevenueDashboard_TopProducts_LossFormat"
                : "RevenueDashboard_TopProducts_ProfitFormat";

            var profitText = string.Format(
                CultureInfo.CurrentCulture,
                LocalizationHelper.GetString(profitTemplateKey, fallback: isLoss ? "− {0} loss" : "+ {0} profit"),
                profitFormatted);

            var unitsTemplate = LocalizationHelper.GetString(
                "RevenueDashboard_TopProducts_UnitsFormat",
                fallback: "{0:0.##} units");

            TopProducts.Add(new TopProductDisplayItem
            {
                RankText = "#" + rank.ToString(CultureInfo.InvariantCulture),
                Name = row.Name,
                UnitsLabel = string.Format(CultureInfo.CurrentCulture, unitsTemplate, row.Units),
                RevenueText = CurrencyDisplayHelper.FormatConfiguredAmount(row.Revenue),
                ProfitText = profitText,
                ProfitBrush = isLoss ? ProfitNegativeBrush : ProfitPositiveBrush,
            });
            rank++;
        }

        EmptyTopProductsVisibility = TopProducts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    // -----------------------------------------------------------------
    // Busiest hour / day
    // -----------------------------------------------------------------

    private void ApplyBusiest(BusiestPeriodSnapshot busiest)
    {
        var notEnoughData = LocalizationHelper.GetString(
            "RevenueDashboard_NotEnoughData",
            fallback: "Not enough data yet");

        if (busiest.PeakHour is int hour)
        {
            var nextHour = (hour + 1) % 24;
            BusiestHourText = $"{hour:00}:00 – {nextHour:00}:00";
            var hourTemplate = LocalizationHelper.GetString(
                "RevenueDashboard_BusiestHour_SubFormat",
                fallback: "{0:N0} transactions in this hour");
            BusiestHourSubtext = string.Format(
                CultureInfo.CurrentCulture,
                hourTemplate,
                busiest.PeakHourTransactionCount);
        }
        else
        {
            BusiestHourText = "—";
            BusiestHourSubtext = notEnoughData;
        }

        if (busiest.PeakDayOfWeek is int dow && dow >= 0 && dow <= 6)
        {
            var dayName = CultureInfo.CurrentCulture.DateTimeFormat.GetDayName((DayOfWeek)dow);
            BusiestDayText = dayName;
            var dayTemplate = LocalizationHelper.GetString(
                "RevenueDashboard_BusiestDay_SubFormat",
                fallback: "{0:N0} transactions on this weekday");
            BusiestDaySubtext = string.Format(
                CultureInfo.CurrentCulture,
                dayTemplate,
                busiest.PeakDayOfWeekTransactionCount);
        }
        else
        {
            BusiestDayText = "—";
            BusiestDaySubtext = notEnoughData;
        }
    }

    // -----------------------------------------------------------------
    // Status banner
    // -----------------------------------------------------------------

    private void SetStatusBanner(string? message)
    {
        StatusBannerText = message ?? string.Empty;
        StatusBannerVisibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
    }

    // -----------------------------------------------------------------
    // Bound properties
    // -----------------------------------------------------------------

    private string _totalRevenueText = "—";
    public string TotalRevenueText { get => _totalRevenueText; set { _totalRevenueText = value; OnChanged(nameof(TotalRevenueText)); } }

    private string _grossProfitText = "—";
    public string GrossProfitText { get => _grossProfitText; set { _grossProfitText = value; OnChanged(nameof(GrossProfitText)); } }

    private string _transactionCountText = "—";
    public string TransactionCountText { get => _transactionCountText; set { _transactionCountText = value; OnChanged(nameof(TransactionCountText)); } }

    private string _averageTransactionText = "—";
    public string AverageTransactionText { get => _averageTransactionText; set { _averageTransactionText = value; OnChanged(nameof(AverageTransactionText)); } }

    private string _revenueDeltaText = "—";
    public string RevenueDeltaText { get => _revenueDeltaText; set { _revenueDeltaText = value; OnChanged(nameof(RevenueDeltaText)); } }

    private string _profitDeltaText = "—";
    public string ProfitDeltaText { get => _profitDeltaText; set { _profitDeltaText = value; OnChanged(nameof(ProfitDeltaText)); } }

    private string _transactionsDeltaText = "—";
    public string TransactionsDeltaText { get => _transactionsDeltaText; set { _transactionsDeltaText = value; OnChanged(nameof(TransactionsDeltaText)); } }

    private string _averageDeltaText = "—";
    public string AverageDeltaText { get => _averageDeltaText; set { _averageDeltaText = value; OnChanged(nameof(AverageDeltaText)); } }

    private SolidColorBrush _revenueDeltaBrush = DeltaFlatBrush;
    public SolidColorBrush RevenueDeltaBrush { get => _revenueDeltaBrush; set { _revenueDeltaBrush = value; OnChanged(nameof(RevenueDeltaBrush)); } }

    private SolidColorBrush _profitDeltaBrush = DeltaFlatBrush;
    public SolidColorBrush ProfitDeltaBrush { get => _profitDeltaBrush; set { _profitDeltaBrush = value; OnChanged(nameof(ProfitDeltaBrush)); } }

    private SolidColorBrush _transactionsDeltaBrush = DeltaFlatBrush;
    public SolidColorBrush TransactionsDeltaBrush { get => _transactionsDeltaBrush; set { _transactionsDeltaBrush = value; OnChanged(nameof(TransactionsDeltaBrush)); } }

    private SolidColorBrush _averageDeltaBrush = DeltaFlatBrush;
    public SolidColorBrush AverageDeltaBrush { get => _averageDeltaBrush; set { _averageDeltaBrush = value; OnChanged(nameof(AverageDeltaBrush)); } }

    private ISeries[] _trendSeries = Array.Empty<ISeries>();
    public ISeries[] TrendSeries { get => _trendSeries; set { _trendSeries = value; OnChanged(nameof(TrendSeries)); } }

    private ICartesianAxis[] _trendXAxes = Array.Empty<ICartesianAxis>();
    public ICartesianAxis[] TrendXAxes { get => _trendXAxes; set { _trendXAxes = value; OnChanged(nameof(TrendXAxes)); } }

    private ICartesianAxis[] _trendYAxes = Array.Empty<ICartesianAxis>();
    public ICartesianAxis[] TrendYAxes { get => _trendYAxes; set { _trendYAxes = value; OnChanged(nameof(TrendYAxes)); } }

    public ObservableCollection<TopProductDisplayItem> TopProducts { get; } = new();

    private Visibility _emptyTopProductsVisibility = Visibility.Visible;
    public Visibility EmptyTopProductsVisibility { get => _emptyTopProductsVisibility; set { _emptyTopProductsVisibility = value; OnChanged(nameof(EmptyTopProductsVisibility)); } }

    private string _busiestHourText = "—";
    public string BusiestHourText { get => _busiestHourText; set { _busiestHourText = value; OnChanged(nameof(BusiestHourText)); } }

    private string _busiestHourSubtext = string.Empty;
    public string BusiestHourSubtext { get => _busiestHourSubtext; set { _busiestHourSubtext = value; OnChanged(nameof(BusiestHourSubtext)); } }

    private string _busiestDayText = "—";
    public string BusiestDayText { get => _busiestDayText; set { _busiestDayText = value; OnChanged(nameof(BusiestDayText)); } }

    private string _busiestDaySubtext = string.Empty;
    public string BusiestDaySubtext { get => _busiestDaySubtext; set { _busiestDaySubtext = value; OnChanged(nameof(BusiestDaySubtext)); } }

    private string _statusBannerText = string.Empty;
    public string StatusBannerText { get => _statusBannerText; set { _statusBannerText = value; OnChanged(nameof(StatusBannerText)); } }

    private Visibility _statusBannerVisibility = Visibility.Collapsed;
    public Visibility StatusBannerVisibility { get => _statusBannerVisibility; set { _statusBannerVisibility = value; OnChanged(nameof(StatusBannerVisibility)); } }

    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class TopProductDisplayItem
{
    public string RankText { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UnitsLabel { get; set; } = string.Empty;
    public string RevenueText { get; set; } = string.Empty;
    public string ProfitText { get; set; } = string.Empty;
    /// <summary>
    /// Foreground brush for the profit/loss line. Green for non-negative,
    /// red for negative — set by <see cref="RevenueDashboardPage.ApplyTopProducts"/>.
    /// XAML binds the inner TextBlock's Foreground to this so a product losing
    /// money cannot accidentally render green.
    /// </summary>
    public SolidColorBrush ProfitBrush { get; set; } =
        new(ColorHelper.FromArgb(0xFF, 0x2E, 0x7D, 0x32));
}

internal static class SkColorExtensions
{
    public static SKColor WithAlpha(this SKColor color, byte alpha) => new(color.Red, color.Green, color.Blue, alpha);
}
