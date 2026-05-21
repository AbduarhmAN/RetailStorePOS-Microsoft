using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
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
/// ABC × XYZ inventory prioritization page (Pro). ABC by revenue share,
/// XYZ by demand stability (coefficient of variation across weekly units).
/// </summary>
public sealed partial class AbcXyzPage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private CancellationTokenSource? _refreshCts;
    private List<AbcXyzRow> _allRows = new();

    public ObservableCollection<AbcXyzDisplayItem> Rows { get; } = new();

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText == value) return; _statusText = value; OnChanged(nameof(StatusText)); }
    }

    public AbcXyzPage()
    {
        NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnChanged(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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

    private async Task ReloadAsync()
    {
        _refreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var token = cts.Token;

        var startUtc = PeriodFilter.StartUtc;
        var endUtc = PeriodFilter.EndUtc;

        StatusText = LocalizationHelper.GetString("AbcXyz_Status_Loading");

        try
        {
            var rows = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return LoginRuntime.AdvancedReports.GetAbcXyzClassification(startUtc, endUtc);
            }, token).ConfigureAwait(true);

            if (token.IsCancellationRequested) return;

            _allRows = rows;
            ApplyMatrix(rows);
            ApplyRows(rows);

            if (rows.All(r => r.UnitsSold == 0))
            {
                StatusText = LocalizationHelper.GetString("AbcXyz_Status_NotEnoughData");
            }
            else
            {
                StatusText = LocalizationHelper.Format(
                    "AbcXyz_Status_LoadedFormat",
                    rows.Count,
                    startUtc,
                    endUtc);
            }
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex)
        {
            _allRows = new List<AbcXyzRow>();
            Rows.Clear();
            ResetMatrix();
            StatusText = LocalizationHelper.Format("AbcXyz_Status_FailedFormat", ex.Message);
        }
    }

    private void ApplyMatrix(List<AbcXyzRow> rows)
    {
        // Group counts + revenue share by (ABC, XYZ) bucket.
        var totalRevenue = rows.Sum(r => r.Revenue);
        var grouped = rows
            .GroupBy(r => (r.AbcClass, r.XyzClass))
            .ToDictionary(g => g.Key, g => (Count: g.Count(), Revenue: g.Sum(r => r.Revenue)));

        SetCell(CellAX, grouped, "A", "X", totalRevenue);
        SetCell(CellAY, grouped, "A", "Y", totalRevenue);
        SetCell(CellAZ, grouped, "A", "Z", totalRevenue);
        SetCell(CellBX, grouped, "B", "X", totalRevenue);
        SetCell(CellBY, grouped, "B", "Y", totalRevenue);
        SetCell(CellBZ, grouped, "B", "Z", totalRevenue);
        SetCell(CellCX, grouped, "C", "X", totalRevenue);
        SetCell(CellCY, grouped, "C", "Y", totalRevenue);
        SetCell(CellCZ, grouped, "C", "Z", totalRevenue);
    }

    private static void SetCell(
        TextBlock cell,
        Dictionary<(string, string), (int Count, decimal Revenue)> grouped,
        string abc,
        string xyz,
        decimal totalRevenue)
    {
        if (!grouped.TryGetValue((abc, xyz), out var stats) || stats.Count == 0)
        {
            cell.Text = "—";
            return;
        }
        var sharePct = totalRevenue > 0m
            ? Math.Round((stats.Revenue / totalRevenue) * 100m, 1)
            : 0m;
        cell.Text = $"{stats.Count:N0}  •  {sharePct:0.#}%";
    }

    private void ResetMatrix()
    {
        CellAX.Text = CellAY.Text = CellAZ.Text = "—";
        CellBX.Text = CellBY.Text = CellBZ.Text = "—";
        CellCX.Text = CellCY.Text = CellCZ.Text = "—";
    }

    private void ApplyRows(List<AbcXyzRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows.OrderBy(r => r.AbcClass).ThenBy(r => r.XyzClass).ThenByDescending(r => r.Revenue))
        {
            Rows.Add(BuildDisplayItem(row));
        }
    }

    private static AbcXyzDisplayItem BuildDisplayItem(AbcXyzRow row)
    {
        var classText = row.AbcClass + row.XyzClass;
        var (bg, fg) = classText.StartsWith('A')
            ? (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xEE, 0xE9, 0xFE)), new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6D, 0x28, 0xD9)))
            : classText.StartsWith('B')
                ? (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xE0, 0xF2, 0xFE)), new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x07, 0x69, 0xA8)))
                : (new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xFE, 0xF3, 0xC7)), new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x92, 0x40, 0x0E)));

        return new AbcXyzDisplayItem
        {
            ProductId = row.ProductId,
            ClassText = classText,
            ClassBackground = bg,
            ClassForeground = fg,
            Name = row.Name,
            BarcodeText = string.IsNullOrEmpty(row.Barcode) ? string.Empty : row.Barcode,
            UnitsText = row.UnitsSold.ToString("0.##", CultureInfo.CurrentCulture),
            RevenueText = CurrencyDisplayHelper.FormatConfiguredAmount(row.Revenue),
            RevenueShareText = $"{row.RevenueSharePercent:0.0}%",
            DemandCvText = row.DemandCv > 0m
                ? row.DemandCv.ToString("0.00", CultureInfo.CurrentCulture)
                : "—"
        };
    }
}

/// <summary>Display-only POCO bound to ABC-XYZ list rows.</summary>
public sealed class AbcXyzDisplayItem
{
    public long ProductId { get; set; }
    public string ClassText { get; set; } = string.Empty;
    public Brush ClassBackground { get; set; } = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0xEE, 0xE9, 0xFE));
    public Brush ClassForeground { get; set; } = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x6D, 0x28, 0xD9));
    public string Name { get; set; } = string.Empty;
    public string BarcodeText { get; set; } = string.Empty;
    public string UnitsText { get; set; } = string.Empty;
    public string RevenueText { get; set; } = string.Empty;
    public string RevenueShareText { get; set; } = string.Empty;
    public string DemandCvText { get; set; } = string.Empty;
}
