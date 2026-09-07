
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
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Views.Components;

namespace RetailStorePOS.UI.Reporting.Views.AdvancedReports;

/// <summary>
/// Basket Affinity (Pro): frequently-bought-together pairs with support,
/// confidence and lift over the selected period.
/// </summary>
public sealed partial class BasketAffinityPage : Page, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private static readonly SolidColorBrush LiftStrongBrush = new(ColorHelper.FromArgb(0xFF, 0x2E, 0x7D, 0x32));
    private static readonly SolidColorBrush LiftNeutralBrush = new(ColorHelper.FromArgb(0xFF, 0x1A, 0x1D, 0x23));
    private static readonly SolidColorBrush LiftWeakBrush = new(ColorHelper.FromArgb(0xFF, 0xC6, 0x28, 0x28));

    private CancellationTokenSource? _refreshCts;

    public ObservableCollection<BasketAffinityDisplayItem> Rows { get; } = new();

    private string _statusText = string.Empty;
    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText == value) return; _statusText = value; OnChanged(nameof(StatusText)); }
    }

    private Visibility _emptyVisibility = Visibility.Collapsed;
    public Visibility EmptyVisibility
    {
        get => _emptyVisibility;
        private set { if (_emptyVisibility == value) return; _emptyVisibility = value; OnChanged(nameof(EmptyVisibility)); }
    }

    public BasketAffinityPage()
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
        // AOT: mirror Rows into the ListView from code-behind (x:Bind ItemsSource crashes under Native AOT).
        Rows.CollectionChanged -= Rows_CollectionChanged;
        Rows.CollectionChanged += Rows_CollectionChanged;
        ItemsControlSync.Reset(Rows, RowsListView.Items);
        DispatcherQueue.TryEnqueue(QueueReload);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        PeriodFilter.PeriodChanged -= OnPeriodChanged;
        Rows.CollectionChanged -= Rows_CollectionChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }

    private void Rows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        => DispatcherQueue.TryEnqueue(() => ItemsControlSync.Apply(Rows, RowsListView.Items, e));

    private void OnPeriodChanged(object? sender, PeriodChangedEventArgs e)
    {
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
            StartupTrace.Write($"Basket affinity reload failed before guarded refresh: {ex}");
            StatusText = LocalizationHelper.Format("BasketAffinity_Status_FailedFormat", ex.Message);
        }
    }

    private async Task ReloadAsync()
    {
        _refreshCts?.Cancel();
        var cts = new CancellationTokenSource();
        _refreshCts = cts;
        var token = cts.Token;

        var startUtc = PeriodFilter.StartUtc;
        var endUtc = PeriodFilter.EndUtc;

        StatusText = LocalizationHelper.GetString("BasketAffinity_Status_Loading");

        try
        {
            var pairs = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                return LoginRuntime.AdvancedReports.GetBasketAffinity(startUtc, endUtc, minPairCount: 2, topN: 200);
            }, token).ConfigureAwait(true);

            if (token.IsCancellationRequested) return;

            var totalBaskets = await Task.Run(() =>
            {
                // Cheap approximation: sum the top pair count to give a sense
                // of base volume. The query layer doesn't expose total baskets
                // directly to avoid a second round-trip; this is just for the
                // "X significant pairs in N multi-line baskets" status.
                long max = 0L;
                foreach (var r in pairs)
                {
                    if (r.PairCount > max) max = r.PairCount;
                }
                return max;
            }, token).ConfigureAwait(true);

            ApplyRows(pairs);

            StatusText = LocalizationHelper.Format(
                "BasketAffinity_Status_LoadedFormat",
                pairs.Count,
                totalBaskets);
        }
        catch (OperationCanceledException) { /* superseded */ }
        catch (Exception ex)
        {
            Rows.Clear();
            EmptyVisibility = Visibility.Visible;
            StatusText = LocalizationHelper.Format("BasketAffinity_Status_FailedFormat", ex.Message);
        }
    }

    private void ApplyRows(List<BasketAffinityRow> rows)
    {
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(BuildDisplayItem(row));
        }
        EmptyVisibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static BasketAffinityDisplayItem BuildDisplayItem(BasketAffinityRow row)
    {
        var liftBrush = row.Lift >= 1.5m
            ? LiftStrongBrush
            : row.Lift >= 1.0m
                ? LiftNeutralBrush
                : LiftWeakBrush;

        return new BasketAffinityDisplayItem
        {
            ProductIdA = row.ProductIdA,
            ProductIdB = row.ProductIdB,
            ProductNameA = row.ProductNameA,
            ProductNameB = row.ProductNameB,
            PairCountText = row.PairCount.ToString("N0", CultureInfo.CurrentCulture),
            SupportAText = $"{row.SupportAPercent:0.0}%",
            SupportBText = $"{row.SupportBPercent:0.0}%",
            ConfidenceText = $"{row.ConfidenceAtoBPercent:0.0}%",
            LiftText = row.Lift.ToString("0.00", CultureInfo.CurrentCulture),
            LiftBrush = liftBrush
        };
    }
}

/// <summary>Display-only POCO bound to basket-affinity list rows.</summary>
public sealed class BasketAffinityDisplayItem
{
    public long ProductIdA { get; set; }
    public long ProductIdB { get; set; }
    public string ProductNameA { get; set; } = string.Empty;
    public string ProductNameB { get; set; } = string.Empty;
    public string PairCountText { get; set; } = string.Empty;
    public string SupportAText { get; set; } = string.Empty;
    public string SupportBText { get; set; } = string.Empty;
    public string ConfidenceText { get; set; } = string.Empty;
    public string LiftText { get; set; } = string.Empty;
    public Brush LiftBrush { get; set; } = new SolidColorBrush(ColorHelper.FromArgb(0xFF, 0x1A, 0x1D, 0x23));
}
