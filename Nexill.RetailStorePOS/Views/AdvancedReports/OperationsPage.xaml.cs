using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.Data.Modules.Reporting;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Views.Components;

namespace RetailStorePOS.WinUiLogin.Views.AdvancedReports;

/// <summary>
/// Page 3 of Advanced Analytics: operations.
///   - Hour x Day-of-week heatmap: spots staffing patterns and peak hours
///   - Cashier performance table: sales count, revenue, avg ticket, items
/// Same period filter and snapshot-friendly architecture as the other Pro pages.
/// </summary>
public sealed partial class OperationsPage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly string[] DayLabels = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    // Five-step purple ramp matching the legend in XAML.
    private static readonly Windows.UI.Color[] HeatmapRamp =
    {
        ColorHelper.FromArgb(0xFF, 0xF5, 0xF3, 0xFF), // empty / very low
        ColorHelper.FromArgb(0xFF, 0xDD, 0xD6, 0xFE),
        ColorHelper.FromArgb(0xFF, 0xA7, 0x8B, 0xFA),
        ColorHelper.FromArgb(0xFF, 0x7C, 0x3A, 0xED),
        ColorHelper.FromArgb(0xFF, 0x5B, 0x21, 0xB6)  // peak
    };

    private CancellationTokenSource? _refreshCts;
    private bool _isUpdatingOperatorOptions;
    private OperatorFilterOption? _selectedOperator;

    public OperationsPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        OperatorOptions.Add(CreateAllOperatorsOption());
        _selectedOperator = OperatorOptions[0];
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public ObservableCollection<CashierDisplayItem> CashierRows { get; } = new();
    public ObservableCollection<OperatorFilterOption> OperatorOptions { get; } = new();

    public OperatorFilterOption? SelectedOperator
    {
        get => _selectedOperator;
        set
        {
            if (ReferenceEquals(_selectedOperator, value)) return;
            _selectedOperator = value;
            OnChanged(nameof(SelectedOperator));

            if (!_isUpdatingOperatorOptions && IsLoaded)
            {
                DispatcherQueue.TryEnqueue(() => { _ = ReloadAsync(); });
            }
        }
    }

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText == value) return; _statusText = value; OnChanged(nameof(StatusText)); }
    }

    private Visibility _emptyCashierVisibility = Visibility.Collapsed;
    public Visibility EmptyCashierVisibility
    {
        get => _emptyCashierVisibility;
        private set { if (_emptyCashierVisibility == value) return; _emptyCashierVisibility = value; OnChanged(nameof(EmptyCashierVisibility)); }
    }

    private void OnChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PeriodFilter.PeriodChanged += OnPeriodChanged;
        RenderEmptyHeatmap();
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
        var selectedCashier = SelectedOperator?.CashierName;
        var periodKey = BuildPeriodKey(PeriodFilter.ActivePreset, startUtc, endUtc, selectedCashier);

        StatusText = LocalizationHelper.GetString("Operations_Status_Loading");

        // 1. Fast path: load signed snapshot for this period off the UI thread.
        var snapshotStore = LoginRuntime.OperationsSnapshots;
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

        // 2. Live query in the background; replace painted UI with fresh data.
        try
        {
            var (operatorRows, cells, cashiers) = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var operators = LoginRuntime.AdvancedReports.GetCashierPerformance(startUtc, endUtc);
                token.ThrowIfCancellationRequested();
                var c = LoginRuntime.AdvancedReports.GetHourDayHeatmap(startUtc, endUtc, selectedCashier);
                token.ThrowIfCancellationRequested();
                var k = string.IsNullOrWhiteSpace(selectedCashier)
                    ? operators
                    : LoginRuntime.AdvancedReports.GetCashierPerformance(startUtc, endUtc, selectedCashier);
                return (operators, c, k);
            }, token).ConfigureAwait(true);

            if (token.IsCancellationRequested) return;

            ApplyOperatorOptions(operatorRows, selectedCashier);
            RenderHeatmap(cells);
            ApplyCashierRows(cashiers);

            StatusText = LocalizationHelper.Format(
                "Operations_Status_LoadedFormat",
                cashiers.Count,
                cells.Count,
                startUtc,
                endUtc);

            // 3. Persist a fresh snapshot in the background; failure is silent.
            _ = Task.Run(() =>
            {
                try
                {
                    snapshotStore.SaveSnapshot(BuildSnapshot(periodKey, startUtc, endUtc, cells, cashiers));
                }
                catch { /* best-effort */ }
            });
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex)
        {
            // Live query failed — keep cached UI if we already painted it; otherwise blank.
            if (!paintedFromCache)
            {
                CashierRows.Clear();
                UpdateEmptyCashier();
                RenderEmptyHeatmap();
            }
            StatusText = paintedFromCache
                ? LocalizationHelper.GetString("Operations_Status_Stale")
                : LocalizationHelper.Format("Operations_Status_FailedFormat", ex.Message);
        }
    }

    private static string BuildPeriodKey(
        PeriodFilterControl.PeriodPreset preset,
        DateTime startUtc,
        DateTime endUtc,
        string? cashierName)
    {
        string baseKey;
        if (preset == PeriodFilterControl.PeriodPreset.Custom)
        {
            baseKey = $"custom-{startUtc:yyyyMMdd}-{endUtc:yyyyMMdd}";
        }
        else
        {
            baseKey = preset.ToString();
        }

        return string.IsNullOrWhiteSpace(cashierName)
            ? $"{baseKey}-all"
            : $"{baseKey}-operator-{cashierName.Trim()}";
    }

    private void ApplySnapshot(OperationsSnapshot snapshot)
    {
        var cells = new List<HourDayCell>(snapshot.Payload.Heatmap.Count);
        foreach (var c in snapshot.Payload.Heatmap)
        {
            cells.Add(new HourDayCell(c.DayOfWeek, c.Hour, c.TransactionCount, c.Revenue));
        }

        var cashiers = new List<CashierPerformanceRow>(snapshot.Payload.Cashiers.Count);
        foreach (var c in snapshot.Payload.Cashiers)
        {
            cashiers.Add(new CashierPerformanceRow(
                c.CashierName, c.SalesCount, c.Revenue,
                c.AverageTicket, c.UnitsSold, c.ItemsPerTicket));
        }

        RenderHeatmap(cells);
        ApplyCashierRows(cashiers);

        StatusText = LocalizationHelper.Format(
            "Operations_Status_LoadedFormat",
            cashiers.Count,
            cells.Count,
            snapshot.PeriodStartUtc,
            snapshot.PeriodEndUtc);
    }

    private static OperationsSnapshot BuildSnapshot(
        string periodKey, DateTime startUtc, DateTime endUtc,
        List<HourDayCell> cells, List<CashierPerformanceRow> cashiers)
    {
        var payload = new OperationsSnapshotPayload();
        foreach (var c in cells)
        {
            payload.Heatmap.Add(new HeatmapCellEntry
            {
                DayOfWeek = c.DayOfWeek,
                Hour = c.Hour,
                TransactionCount = c.TransactionCount,
                Revenue = c.Revenue
            });
        }
        foreach (var c in cashiers)
        {
            payload.Cashiers.Add(new CashierEntry
            {
                CashierName = c.CashierName,
                SalesCount = c.SalesCount,
                Revenue = c.Revenue,
                AverageTicket = c.AverageTicket,
                UnitsSold = c.UnitsSold,
                ItemsPerTicket = c.ItemsPerTicket
            });
        }

        return new OperationsSnapshot
        {
            PeriodKey = periodKey,
            CapturedAtUtc = DateTime.UtcNow,
            PeriodStartUtc = startUtc,
            PeriodEndUtc = endUtc,
            Payload = payload
        };
    }

    // -----------------------------------------------------------------
    // Heatmap render
    // -----------------------------------------------------------------
    private void RenderEmptyHeatmap()
    {
        BuildHeatmapGrid(new int[7, 24], peak: 0);
    }

    private void RenderHeatmap(List<HourDayCell> cells)
    {
        var matrix = new int[7, 24];
        var peak = 0;
        foreach (var cell in cells)
        {
            if (cell.DayOfWeek < 0 || cell.DayOfWeek > 6) continue;
            if (cell.Hour < 0 || cell.Hour > 23) continue;
            matrix[cell.DayOfWeek, cell.Hour] = cell.TransactionCount;
            if (cell.TransactionCount > peak) peak = cell.TransactionCount;
        }
        BuildHeatmapGrid(matrix, peak);
    }

    private void BuildHeatmapGrid(int[,] matrix, int peak)
    {
        HeatmapHost.Children.Clear();
        HeatmapHost.RowDefinitions.Clear();
        HeatmapHost.ColumnDefinitions.Clear();
        HeatmapHost.RowSpacing = 2;
        HeatmapHost.ColumnSpacing = 2;

        // Column 0: day label. Columns 1..24: hour cells. (25 columns total.)
        HeatmapHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        for (var h = 0; h < 24; h++)
        {
            HeatmapHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Row 0: hour labels. Rows 1..7: day rows. (8 rows total.)
        HeatmapHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });
        for (var d = 0; d < 7; d++)
        {
            HeatmapHost.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
        }

        // Hour column headers (every 3 hours to keep it readable).
        for (var h = 0; h < 24; h++)
        {
            if (h % 3 != 0) continue;
            var label = new TextBlock
            {
                Text = h.ToString("00"),
                FontSize = 10,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x8F, 0x96, 0xA3)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, h + 1);
            HeatmapHost.Children.Add(label);
        }

        // Day rows.
        for (var d = 0; d < 7; d++)
        {
            var dayLabel = new TextBlock
            {
                Text = DayLabels[d],
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x45, 0x44, 0x43)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(dayLabel, d + 1);
            Grid.SetColumn(dayLabel, 0);
            HeatmapHost.Children.Add(dayLabel);

            for (var h = 0; h < 24; h++)
            {
                var value = matrix[d, h];
                var color = ResolveHeatmapColor(value, peak);
                var cell = new Border
                {
                    Background = new SolidColorBrush(color),
                    CornerRadius = new CornerRadius(3),
                    MinHeight = 24,
                    Margin = new Thickness(0)
                };
                if (value > 0)
                {
                    ToolTipService.SetToolTip(cell, $"{DayLabels[d]} {h:00}:00 — {value:N0} sales");
                }
                Grid.SetRow(cell, d + 1);
                Grid.SetColumn(cell, h + 1);
                HeatmapHost.Children.Add(cell);
            }
        }
    }

    private static Windows.UI.Color ResolveHeatmapColor(int value, int peak)
    {
        if (value <= 0 || peak <= 0) return HeatmapRamp[0];
        // Square root mapping spreads small values across the ramp better than linear.
        var ratio = Math.Sqrt((double)value / peak);
        var idx = (int)Math.Round(ratio * (HeatmapRamp.Length - 1));
        idx = Math.Clamp(idx, 1, HeatmapRamp.Length - 1);
        return HeatmapRamp[idx];
    }

    // -----------------------------------------------------------------
    // Cashier table
    // -----------------------------------------------------------------
    private void ApplyCashierRows(List<CashierPerformanceRow> rows)
    {
        CashierRows.Clear();
        foreach (var row in rows)
        {
            CashierRows.Add(new CashierDisplayItem
            {
                CashierName = row.CashierName,
                SalesText = row.SalesCount.ToString("N0", CultureInfo.CurrentCulture),
                RevenueText = CurrencyDisplayHelper.FormatConfiguredAmount(row.Revenue),
                AverageTicketText = CurrencyDisplayHelper.FormatConfiguredAmount(row.AverageTicket),
                UnitsText = row.UnitsSold.ToString("0.##", CultureInfo.CurrentCulture),
                ItemsPerTicketText = row.ItemsPerTicket.ToString("0.0", CultureInfo.CurrentCulture)
            });
        }
        UpdateEmptyCashier();
    }

    private void UpdateEmptyCashier()
    {
        EmptyCashierVisibility = CashierRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyOperatorOptions(List<CashierPerformanceRow> rows, string? selectedCashierName)
    {
        var selectedName = string.IsNullOrWhiteSpace(selectedCashierName) ? null : selectedCashierName.Trim();
        _isUpdatingOperatorOptions = true;
        try
        {
            OperatorOptions.Clear();
            var allOption = CreateAllOperatorsOption();
            OperatorOptions.Add(allOption);

            OperatorFilterOption? selectedOption = selectedName is null ? allOption : null;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.CashierName) || !seen.Add(row.CashierName))
                {
                    continue;
                }

                var option = new OperatorFilterOption(row.CashierName, row.CashierName);
                OperatorOptions.Add(option);
                if (selectedName is not null && string.Equals(row.CashierName, selectedName, StringComparison.OrdinalIgnoreCase))
                {
                    selectedOption = option;
                }
            }

            if (selectedOption is null && selectedName is not null)
            {
                selectedOption = new OperatorFilterOption(selectedName, selectedName);
                OperatorOptions.Add(selectedOption);
            }

            SelectedOperator = selectedOption ?? allOption;
        }
        finally
        {
            _isUpdatingOperatorOptions = false;
        }
    }

    private static OperatorFilterOption CreateAllOperatorsOption()
    {
        return new OperatorFilterOption(LocalizationHelper.GetString("Operations_Operator_All"), cashierName: null);
    }
}

public sealed class CashierDisplayItem
{
    public string CashierName { get; set; } = string.Empty;
    public string SalesText { get; set; } = string.Empty;
    public string RevenueText { get; set; } = string.Empty;
    public string AverageTicketText { get; set; } = string.Empty;
    public string UnitsText { get; set; } = string.Empty;
    public string ItemsPerTicketText { get; set; } = string.Empty;
}

public sealed class OperatorFilterOption
{
    public OperatorFilterOption(string displayName, string? cashierName)
    {
        DisplayName = displayName;
        CashierName = cashierName;
    }

    public string DisplayName { get; }
    public string? CashierName { get; }
}
