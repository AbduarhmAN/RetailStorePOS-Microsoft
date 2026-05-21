using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.Data.Modules.Reporting;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Views.Components;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RetailStorePOS.WinUiLogin.Views.AdvancedReports;

/// <summary>
/// Page 2 of Advanced Analytics: per-product performance over a period.
/// Tabs:
///   - Top sellers : products with sales, sorted by revenue
///   - Slow movers : products with sales but fewer than the slow threshold
///   - Dead stock  : products with no sales in the period
/// CSV export emits the currently visible tab.
/// </summary>
public sealed partial class ProductPerformancePage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private const int SlowMoverUnitThreshold = 5;
    private const int VisibleRowLimit = 200;

    private static readonly SolidColorBrush ProfitPositiveBrush = new(ColorHelper.FromArgb(0xFF, 0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush ProfitNegativeBrush = new(ColorHelper.FromArgb(0xFF, 0xC6, 0x28, 0x28));
    private static readonly SolidColorBrush ProfitNeutralBrush  = new(ColorHelper.FromArgb(0xFF, 0x8F, 0x96, 0xA3));

    private CancellationTokenSource? _refreshCts;
    private List<ProductPerformanceRow> _allRows = new();

    public ProductPerformancePage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // -----------------------------------------------------------------
    // Bindings
    // -----------------------------------------------------------------
    public ObservableCollection<ProductPerformanceDisplayItem> VisibleRows { get; } = new();

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText == value) return; _statusText = value; OnChanged(nameof(StatusText)); }
    }

    private string _rowCountText = string.Empty;
    public string RowCountText
    {
        get => _rowCountText;
        private set { if (_rowCountText == value) return; _rowCountText = value; OnChanged(nameof(RowCountText)); }
    }

    private string _emptyStateText = LocalizationHelper.GetString("ProductPerformance_Empty_Default");
    public string EmptyStateText
    {
        get => _emptyStateText;
        private set { if (_emptyStateText == value) return; _emptyStateText = value; OnChanged(nameof(EmptyStateText)); }
    }

    private Visibility _emptyStateVisibility = Visibility.Collapsed;
    public Visibility EmptyStateVisibility
    {
        get => _emptyStateVisibility;
        private set { if (_emptyStateVisibility == value) return; _emptyStateVisibility = value; OnChanged(nameof(EmptyStateVisibility)); }
    }

    private void OnChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    // -----------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------
    private enum Tab { TopSellers, TopMovers, SlowMovers, DeadStock }
    private Tab _activeTab = Tab.TopSellers;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PeriodFilter.PeriodChanged += OnPeriodChanged;
        DispatcherQueue.TryEnqueue(() => { _ = ReloadAsync(); });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PeriodFilter.PeriodChanged -= OnPeriodChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private void OnPeriodChanged(object? sender, PeriodChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => { _ = ReloadAsync(); });
    }

    private void TabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton btn || btn.Tag is not string tagStr) return;
        if (!Enum.TryParse<Tab>(tagStr, out var tab)) return;

        // Enforce single-selection across the toggle group.
        TopSellersButton.IsChecked = tab == Tab.TopSellers;
        TopMoversButton.IsChecked  = tab == Tab.TopMovers;
        SlowMoversButton.IsChecked = tab == Tab.SlowMovers;
        DeadStockButton.IsChecked  = tab == Tab.DeadStock;

        _activeTab = tab;
        RefreshVisibleRows();
    }

    // -----------------------------------------------------------------
    // Data load — snapshot-first (signed local cache) + background refresh.
    // -----------------------------------------------------------------
    private async Task ReloadAsync()
    {
        _refreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var token = cts.Token;

        var startUtc = PeriodFilter.StartUtc;
        var endUtc = PeriodFilter.EndUtc;
        var periodKey = BuildPeriodKey(PeriodFilter.ActivePreset, startUtc, endUtc);

        StatusText = LocalizationHelper.GetString("ProductPerformance_Status_Loading");

        // 1. Fast path: load signed snapshot for this period off the UI thread.
        var snapshotStore = LoginRuntime.ProductPerformanceSnapshots;
        var maxAge = TimeSpan.FromMinutes(5);
        var cached = await Task.Run(() =>
        {
            try { return snapshotStore.LoadSnapshotIfFresh(periodKey, maxAge); }
            catch { return null; }
        }, token).ConfigureAwait(true);

        var paintedFromCache = false;
        if (cached is not null && !token.IsCancellationRequested)
        {
            ApplySnapshot(cached);
            paintedFromCache = true;
        }

        // 2. Live query in the background; replace cached UI with fresh data.
        try
        {
            var rows = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return LoginRuntime.AdvancedReports.GetProductPerformance(startUtc, endUtc);
            }, token).ConfigureAwait(true);

            if (token.IsCancellationRequested) return;

            _allRows = rows;
            RefreshVisibleRows();

            StatusText = LocalizationHelper.Format(
                "ProductPerformance_Status_LoadedFormat",
                _allRows.Count,
                startUtc,
                endUtc);

            // 3. Persist a fresh snapshot in the background; failure is silent.
            _ = Task.Run(() =>
            {
                try
                {
                    snapshotStore.SaveSnapshot(BuildSnapshot(periodKey, startUtc, endUtc, rows));
                }
                catch { /* best-effort */ }
            });
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex)
        {
            if (!paintedFromCache)
            {
                _allRows = new List<ProductPerformanceRow>();
                VisibleRows.Clear();
                UpdateEmptyState();
            }
            StatusText = paintedFromCache
                ? LocalizationHelper.GetString("ProductPerformance_Status_Stale")
                : LocalizationHelper.Format("ProductPerformance_Status_FailedFormat", ex.Message);
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

    private void ApplySnapshot(ProductPerformanceSnapshot snapshot)
    {
        var rows = new List<ProductPerformanceRow>(snapshot.Payload.Rows.Count);
        foreach (var r in snapshot.Payload.Rows)
        {
            rows.Add(new ProductPerformanceRow(
                r.ProductId,
                r.Name,
                r.Barcode,
                r.UnitsSold,
                r.Revenue,
                r.Profit,
                r.MarginPercent,
                r.LastSaleAt,
                r.CurrentStock));
        }
        _allRows = rows;
        RefreshVisibleRows();

        StatusText = LocalizationHelper.Format(
            "ProductPerformance_Status_LoadedFormat",
            _allRows.Count,
            snapshot.PeriodStartUtc,
            snapshot.PeriodEndUtc);
    }

    private static ProductPerformanceSnapshot BuildSnapshot(
        string periodKey, DateTime startUtc, DateTime endUtc,
        List<ProductPerformanceRow> rows)
    {
        var payload = new ProductPerformanceSnapshotPayload();
        foreach (var r in rows)
        {
            payload.Rows.Add(new ProductPerformanceEntry
            {
                ProductId = r.ProductId,
                Name = r.Name,
                Barcode = r.Barcode,
                UnitsSold = r.UnitsSold,
                Revenue = r.Revenue,
                Profit = r.Profit,
                MarginPercent = r.MarginPercent,
                LastSaleAt = r.LastSaleAt,
                CurrentStock = r.CurrentStock
            });
        }

        return new ProductPerformanceSnapshot
        {
            PeriodKey = periodKey,
            CapturedAtUtc = DateTime.UtcNow,
            PeriodStartUtc = startUtc,
            PeriodEndUtc = endUtc,
            Payload = payload
        };
    }

    // -----------------------------------------------------------------
    // Filtering + sorting per tab
    // -----------------------------------------------------------------
    private void RefreshVisibleRows()
    {
        VisibleRows.Clear();

        IEnumerable<ProductPerformanceRow> filtered = _activeTab switch
        {
            Tab.TopSellers => _allRows
                .Where(r => r.UnitsSold > 0)
                .OrderByDescending(r => r.Revenue)
                .ThenByDescending(r => r.UnitsSold),
            Tab.TopMovers => _allRows
                .Where(r => r.UnitsSold > 0)
                .OrderByDescending(r => r.UnitsSold)
                .ThenByDescending(r => r.Revenue),
            Tab.SlowMovers => _allRows
                .Where(r => r.UnitsSold > 0 && r.UnitsSold < SlowMoverUnitThreshold)
                .OrderBy(r => r.UnitsSold)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            Tab.DeadStock => _allRows
                .Where(r => r.UnitsSold == 0)
                .OrderBy(r => r.LastSaleAt ?? DateTime.MinValue)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => _allRows
        };

        var rank = 1;
        foreach (var row in filtered.Take(VisibleRowLimit))
        {
            VisibleRows.Add(BuildDisplayItem(row, rank));
            rank++;
        }

        EmptyStateText = _activeTab switch
        {
            Tab.TopSellers => LocalizationHelper.GetString("ProductPerformance_Empty_NoSales"),
            Tab.TopMovers  => LocalizationHelper.GetString("ProductPerformance_Empty_NoSales"),
            Tab.SlowMovers => LocalizationHelper.Format("ProductPerformance_Empty_SlowFormat", SlowMoverUnitThreshold),
            Tab.DeadStock  => LocalizationHelper.GetString("ProductPerformance_Empty_DeadStock"),
            _ => LocalizationHelper.GetString("ProductPerformance_Empty_Default")
        };

        UpdateEmptyState();

        var totalForActive = _activeTab switch
        {
            Tab.TopSellers => _allRows.Count(r => r.UnitsSold > 0),
            Tab.TopMovers  => _allRows.Count(r => r.UnitsSold > 0),
            Tab.SlowMovers => _allRows.Count(r => r.UnitsSold > 0 && r.UnitsSold < SlowMoverUnitThreshold),
            Tab.DeadStock  => _allRows.Count(r => r.UnitsSold == 0),
            _ => _allRows.Count
        };
        var shown = VisibleRows.Count;
        RowCountText = totalForActive > shown
            ? LocalizationHelper.Format("ProductPerformance_RowCount_ShowingFormat", shown, totalForActive)
            : LocalizationHelper.Format("ProductPerformance_RowCount_AllFormat", shown);
    }

    private void UpdateEmptyState()
    {
        EmptyStateVisibility = VisibleRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static ProductPerformanceDisplayItem BuildDisplayItem(ProductPerformanceRow row, int rank)
    {
        SolidColorBrush brush = row.Profit > 0m ? ProfitPositiveBrush
                              : row.Profit < 0m ? ProfitNegativeBrush
                              : ProfitNeutralBrush;

        return new ProductPerformanceDisplayItem
        {
            ProductId = row.ProductId,
            Name = row.Name,
            BarcodeText = string.IsNullOrWhiteSpace(row.Barcode) ? string.Empty : "Barcode " + row.Barcode,
            RankText = "#" + rank.ToString(CultureInfo.InvariantCulture),
            UnitsText = row.UnitsSold.ToString("0.##", CultureInfo.CurrentCulture),
            RevenueText = CurrencyDisplayHelper.FormatConfiguredAmount(row.Revenue),
            ProfitText = CurrencyDisplayHelper.FormatConfiguredAmount(row.Profit),
            MarginText = row.Revenue > 0m
                ? row.MarginPercent.ToString("0.0", CultureInfo.CurrentCulture) + "%"
                : "—",
            LastSoldText = row.LastSaleAt.HasValue
                ? row.LastSaleAt.Value.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)
                : "Never",
            StockText = row.CurrentStock.ToString("0.##", CultureInfo.CurrentCulture),
            ProfitBrush = brush
        };
    }

    // -----------------------------------------------------------------
    // CSV export
    // -----------------------------------------------------------------
    private async void ExportCsvButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker
            {
                SuggestedFileName = $"product-performance-{_activeTab}-{DateTime.Now:yyyyMMdd-HHmm}",
                DefaultFileExtension = ".csv"
            };
            picker.FileTypeChoices.Add("CSV", new List<string> { ".csv" });

            var hwnd = WindowNative.GetWindowHandle(MainWindow.Current);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            // Reuse the visible rows so users export exactly what they see.
            var snapshot = VisibleRows.ToArray();

            await Task.Run(() =>
            {
                using var writer = new StreamWriter(file.Path);
                using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
                csv.WriteField("Rank");
                csv.WriteField("Name");
                csv.WriteField("Barcode");
                csv.WriteField("Units");
                csv.WriteField("Revenue");
                csv.WriteField("Profit");
                csv.WriteField("Margin %");
                csv.WriteField("Last sold");
                csv.WriteField("Stock");
                csv.NextRecord();

                foreach (var item in snapshot)
                {
                    csv.WriteField(item.RankText);
                    csv.WriteField(item.Name);
                    csv.WriteField(item.BarcodeText);
                    csv.WriteField(item.UnitsText);
                    csv.WriteField(item.RevenueText);
                    csv.WriteField(item.ProfitText);
                    csv.WriteField(item.MarginText);
                    csv.WriteField(item.LastSoldText);
                    csv.WriteField(item.StockText);
                    csv.NextRecord();
                }
            }).ConfigureAwait(true);

            StatusText = LocalizationHelper.Format("ProductPerformance_Status_ExportedFormat", snapshot.Length, file.Name);
        }
        catch (Exception ex)
        {
            StatusText = LocalizationHelper.Format("ProductPerformance_Status_ExportFailedFormat", ex.Message);
        }
    }
}

/// <summary>
/// Display-only POCO bound to the table rows.
/// </summary>
public sealed class ProductPerformanceDisplayItem
{
    public long ProductId { get; set; }
    public string RankText { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string BarcodeText { get; set; } = string.Empty;
    public string UnitsText { get; set; } = string.Empty;
    public string RevenueText { get; set; } = string.Empty;
    public string ProfitText { get; set; } = string.Empty;
    public string MarginText { get; set; } = string.Empty;
    public string LastSoldText { get; set; } = string.Empty;
    public string StockText { get; set; } = string.Empty;
    public SolidColorBrush ProfitBrush { get; set; } = new(ColorHelper.FromArgb(0xFF, 0x8F, 0x96, 0xA3));
}
