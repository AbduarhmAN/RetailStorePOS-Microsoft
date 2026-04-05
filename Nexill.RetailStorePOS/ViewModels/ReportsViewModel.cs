using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Models;
using RetailStorePOS.WinUiLogin.Views;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class ReportsViewModel : ObservableObject
{
    private readonly List<Sale> _rangeSales = new();

    private DateTimeOffset? _fromDate = DateTimeOffset.Now;
    private DateTimeOffset? _toDate = DateTimeOffset.Now;
    private string _searchText = string.Empty;
    private string _rangeLabel = "Today";
    private decimal _totalSales;
    private decimal _netSales;
    private decimal _taxCollected;
    private decimal _grossProfit;
    private decimal _profitMargin;
    private decimal _cashTotal;
    private decimal _cardTotal;
    private int _transactionsCount;
    private decimal _averageSale;
    private int _visibleTransactionsCount;
    private bool _hasNoVisibleTransactions;
    private string _statusText = "Loading...";
    private bool _isTodayPreset;
    private bool _isLast7DaysPreset;
    private bool _isLast30DaysPreset;
    private bool _isTransactionsExpanded;

    public ReportsViewModel()
    {
        RefreshCommand = new RelayCommand(LoadReport);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        SetTodayRangeCommand = new RelayCommand(() => SetTodayRange());
        SetLast7DaysRangeCommand = new RelayCommand(SetLast7DaysRange);
        SetLast30DaysRangeCommand = new RelayCommand(SetLast30DaysRange);
        OpenReceiptFolderCommand = new RelayCommand(OpenReceiptFolder);
        ReprintReceiptCommand = new RelayCommand<Sale>(ReprintReceipt);
        ToggleTransactionsViewCommand = new RelayCommand(ToggleTransactionsView);

        SetTodayRange(loadReport: false);
        StatusText = "Loading...";
    }

    public ObservableCollection<Sale> Sales { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand SetTodayRangeCommand { get; }
    public RelayCommand SetLast7DaysRangeCommand { get; }
    public RelayCommand SetLast30DaysRangeCommand { get; }
    public RelayCommand OpenReceiptFolderCommand { get; }
    public RelayCommand<Sale> ReprintReceiptCommand { get; }
    public RelayCommand ToggleTransactionsViewCommand { get; }

    public DateTimeOffset? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                if (value.HasValue && ToDate.HasValue && ToDate.Value < value.Value)
                {
                    ToDate = value.Value;
                }
                ClearPresets();
                LoadReport();
            }
        }
    }

    public DateTimeOffset? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                if (value.HasValue && FromDate.HasValue && FromDate.Value > value.Value)
                {
                    FromDate = value.Value;
                }
                ClearPresets();
                LoadReport();
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplySearchFilter();
            }
        }
    }

    public ObservableCollection<ProductVelocityItem> TopMovers { get; } = new();
    public ObservableCollection<ProductVelocityItem> SlowMovers { get; } = new();

    public string RangeLabel
    {
        get => _rangeLabel;
        private set => SetProperty(ref _rangeLabel, value);
    }

    public string TotalSalesText => _totalSales.ToString("C2", CultureInfo.CurrentCulture);
    public string NetSalesText => _netSales.ToString("C2", CultureInfo.CurrentCulture);
    public string TaxCollectedText => _taxCollected.ToString("C2", CultureInfo.CurrentCulture);
    public string GrossProfitText => _grossProfit.ToString("C2", CultureInfo.CurrentCulture);
    public string ProfitMarginText => _profitMargin.ToString("P1", CultureInfo.CurrentCulture);
    public string CashTotalText => _cashTotal.ToString("C2", CultureInfo.CurrentCulture);
    public string CardTotalText => _cardTotal.ToString("C2", CultureInfo.CurrentCulture);

    public string AverageSaleText => _averageSale.ToString("C2", CultureInfo.CurrentCulture);

    public SolidColorBrush TodayPresetBackground => IsTodayPreset ? new SolidColorBrush(Microsoft.UI.Colors.LightGray) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    public SolidColorBrush Last7PresetBackground => IsLast7DaysPreset ? new SolidColorBrush(Microsoft.UI.Colors.LightGray) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    public SolidColorBrush Last30PresetBackground => IsLast30DaysPreset ? new SolidColorBrush(Microsoft.UI.Colors.LightGray) : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    public int TransactionsCount
    {
        get => _transactionsCount;
        private set => SetProperty(ref _transactionsCount, value);
    }

    public int VisibleTransactionsCount
    {
        get => _visibleTransactionsCount;
        private set => SetProperty(ref _visibleTransactionsCount, value);
    }

    public bool HasNoVisibleTransactions
    {
        get => _hasNoVisibleTransactions;
        private set => SetProperty(ref _hasNoVisibleTransactions, value);
    }

    public Visibility EmptyStateVisibility => HasNoVisibleTransactions ? Visibility.Visible : Visibility.Collapsed;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsTodayPreset
    {
        get => _isTodayPreset;
        private set => SetProperty(ref _isTodayPreset, value);
    }

    public bool IsLast7DaysPreset
    {
        get => _isLast7DaysPreset;
        private set => SetProperty(ref _isLast7DaysPreset, value);
    }

    public bool IsLast30DaysPreset
    {
        get => _isLast30DaysPreset;
        private set => SetProperty(ref _isLast30DaysPreset, value);
    }

    public bool IsTransactionsExpanded
    {
        get => _isTransactionsExpanded;
        private set
        {
            if (SetProperty(ref _isTransactionsExpanded, value))
            {
                OnPropertyChanged(nameof(ReportChromeVisibility));
                OnPropertyChanged(nameof(PageRowSpacing));
                OnPropertyChanged(nameof(TransactionToggleSymbol));
                OnPropertyChanged(nameof(TransactionToggleTooltip));
            }
        }
    }

    public Visibility ReportChromeVisibility => IsTransactionsExpanded ? Visibility.Collapsed : Visibility.Visible;

    public double PageRowSpacing => IsTransactionsExpanded ? 0 : 16;

    public Symbol TransactionToggleSymbol => IsTransactionsExpanded ? Symbol.BackToWindow : Symbol.FullScreen;

    public string TransactionToggleTooltip => IsTransactionsExpanded
        ? "Collapse report view"
        : "Expand transaction list";

    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void SetTodayRange(bool loadReport = true)
    {
        SetPresets(today: true, last7: false, last30: false);
        SetRange(DateTime.Today, DateTime.Today, "Today", loadReport);
    }

    private void SetLast7DaysRange()
    {
        SetPresets(today: false, last7: true, last30: false);
        SetRange(DateTime.Today.AddDays(-6), DateTime.Today, "Last 7 days");
    }

    private void SetLast30DaysRange()
    {
        SetPresets(today: false, last7: false, last30: true);
        SetRange(DateTime.Today.AddDays(-29), DateTime.Today, "Last 30 days");
    }

    private void OpenReceiptFolder()
    {
        ReceiptHelper.OpenReceiptsFolder();
    }

    private void ToggleTransactionsView()
    {
        IsTransactionsExpanded = !IsTransactionsExpanded;
    }

    private void ReprintReceipt(Sale? sale)
    {
        if (sale is null) return;
        try
        {
            var pdfPath = ReceiptHelper.GetReceiptPdfPath(sale.ReceiptNumber);
            if (!File.Exists(pdfPath))
            {
                pdfPath = ReceiptHelper.ArchiveReceipt(BuildReceiptSummary(sale));
            }

            if (!ReceiptHelper.TryOpenReceiptPdf(pdfPath))
            {
                StatusText = $"Receipt PDF not found for #{sale.ReceiptNumber:D6}";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to reprint receipt #{sale.ReceiptNumber:D6}: {ex.Message}";
        }
    }

    private static ReceiptSummary BuildReceiptSummary(Sale sale)
    {
        var summary = new ReceiptSummary
        {
            SaleId = sale.Id,
            ReceiptNumber = sale.ReceiptNumber,
            CreatedAt = sale.CreatedAt,
            CashierName = sale.CashierName,
            Subtotal = sale.Subtotal,
            Tax = sale.Tax,
            Total = sale.Total,
            Tendered = sale.Tendered,
            Change = sale.Change
        };

        foreach (var item in sale.Items)
        {
            summary.Items.Add(new ReceiptLineItem
            {
                ProductId = item.ProductId,
                Name = item.Name,
                Barcode = item.Barcode,
                Price = item.Price,
                Quantity = item.Quantity
            });
        }

        return summary;
    }

    private void SetPresets(bool today, bool last7, bool last30)
    {
        IsTodayPreset = today;
        IsLast7DaysPreset = last7;
        IsLast30DaysPreset = last30;
    }

    private void ClearPresets()
    {
        SetPresets(false, false, false);
    }

    private void SetRange(DateTime from, DateTime to, string label, bool loadReport = true)
    {
        _fromDate = new DateTimeOffset(from);
        _toDate = new DateTimeOffset(to);
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));

        RangeLabel = label;
        if (loadReport)
        {
            LoadReport();
        }
    }

    private void UpdateCustomRangeLabel()
    {
        if (IsTodayPreset || IsLast7DaysPreset || IsLast30DaysPreset) return;

        var from = FromDate?.Date ?? DateTime.Today;
        var to = ToDate?.Date ?? DateTime.Today;

        RangeLabel = from == to
            ? from.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
            : $"{from:MMM d} - {to:MMM d, yyyy}";
    }

    private void LoadReport()
    {
        try
        {
            var from = FromDate?.Date ?? DateTime.Today;
            var to = ToDate?.Date ?? DateTime.Today;

            if (to < from)
            {
                (from, to) = (to, from);
            }

            var repoSales = LoginRuntime.Sales.GetSalesByDateRange(from, to);

            _rangeSales.Clear();
            _rangeSales.AddRange(repoSales.OrderByDescending(s => s.CreatedAt));

            _totalSales = _rangeSales.Sum(s => s.Total);
            _taxCollected = _rangeSales.Sum(s => s.Tax);
            _netSales = Math.Round(_totalSales - _taxCollected, 2, MidpointRounding.AwayFromZero);
            TransactionsCount = _rangeSales.Count;
            _averageSale = TransactionsCount == 0
                ? 0
                : Math.Round(_totalSales / TransactionsCount, 2, MidpointRounding.AwayFromZero);

            _cashTotal = _rangeSales.Where(s => s.PaymentType.Equals("Cash", StringComparison.OrdinalIgnoreCase)).Sum(s => s.Total);
            _cardTotal = _rangeSales.Where(s => s.PaymentType.Equals("Card", StringComparison.OrdinalIgnoreCase) || s.PaymentType.Equals("Credit", StringComparison.OrdinalIgnoreCase) || s.PaymentType.Equals("Debit", StringComparison.OrdinalIgnoreCase)).Sum(s => s.Total);

            var totalCogs = _rangeSales.SelectMany(s => s.Items).Sum(i => i.Quantity * i.ItemCost);
            _grossProfit = _netSales - totalCogs;
            _profitMargin = _netSales > 0 ? _grossProfit / _netSales : 0;

            OnPropertyChanged(nameof(TotalSalesText));
            OnPropertyChanged(nameof(TaxCollectedText));
            OnPropertyChanged(nameof(NetSalesText));
            OnPropertyChanged(nameof(AverageSaleText));
            OnPropertyChanged(nameof(GrossProfitText));
            OnPropertyChanged(nameof(ProfitMarginText));
            OnPropertyChanged(nameof(CashTotalText));
            OnPropertyChanged(nameof(CardTotalText));
            OnPropertyChanged(nameof(TodayPresetBackground));
            OnPropertyChanged(nameof(Last7PresetBackground));
            OnPropertyChanged(nameof(Last30PresetBackground));

            UpdateProductVelocity();

            UpdateCustomRangeLabel();
            ApplySearchFilter();
        }
        catch (Exception ex)
        {
            StatusText = $"ERROR: {ex.Message}";
        }
    }

    private void UpdateProductVelocity()
    {
        TopMovers.Clear();
        SlowMovers.Clear();

        var itemQuantities = new Dictionary<string, decimal>();
        foreach (var sale in _rangeSales)
        {
            foreach (var item in sale.Items)
            {
                if (!itemQuantities.ContainsKey(item.Name))
                {
                    itemQuantities[item.Name] = 0;
                }
                itemQuantities[item.Name] += item.Quantity;
            }
        }

        var sortedItems = itemQuantities.OrderByDescending(kvp => kvp.Value).ToList();

        var top = sortedItems.Take(10);
        foreach (var kvp in top)
        {
            TopMovers.Add(new ProductVelocityItem { ProductName = kvp.Key, VelocityText = $"{kvp.Value} sold" });
        }

        var slow = sortedItems.OrderBy(kvp => kvp.Value).Take(10);
        foreach (var kvp in slow)
        {
            SlowMovers.Add(new ProductVelocityItem { ProductName = kvp.Key, VelocityText = $"{kvp.Value} sold" });
        }
    }

    private void ApplySearchFilter()
    {
        IEnumerable<Sale> scoped = _rangeSales;
        var query = SearchText?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            scoped = scoped.Where(s => MatchesSearch(s, query));
        }

        Sales.Clear();
        foreach (var sale in scoped)
        {
            Sales.Add(sale);
        }

        VisibleTransactionsCount = Sales.Count;
        HasNoVisibleTransactions = VisibleTransactionsCount == 0;
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ReportChromeVisibility));
        OnPropertyChanged(nameof(PageRowSpacing));

        var from = FromDate?.Date ?? DateTime.Today;
        var to = ToDate?.Date ?? DateTime.Today;
        var rangeText = from == to
            ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";

        StatusText = $"{rangeText} | {VisibleTransactionsCount} of {TransactionsCount} transactions shown.";
    }

    private static bool MatchesSearch(Sale sale, string query)
    {
        var normalized = query.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        var localTime = sale.CreatedAt.ToLocalTime();

        if (normalized.Contains(':'))
        {
            return localTime
                .ToString("HH:mm", CultureInfo.InvariantCulture)
                .Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }

        if (normalized.Contains('-') || normalized.Contains('/'))
        {
            return localTime
                .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                .Contains(normalized, StringComparison.OrdinalIgnoreCase)
                   || localTime
                       .ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                       .Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }

        if (normalized.Any(char.IsLetter))
        {
            var paymentMatch = sale.PaymentType?.Contains(normalized, StringComparison.OrdinalIgnoreCase) == true;
            if (paymentMatch) return true;

            return localTime
                .ToString("ddd", CultureInfo.InvariantCulture)
                .Contains(normalized, StringComparison.OrdinalIgnoreCase)
                   || localTime
                       .ToString("MMMM", CultureInfo.InvariantCulture)
                       .Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }

        if (long.TryParse(normalized, out var receiptNum))
        {
            return sale.ReceiptNumber == receiptNum || sale.ReceiptNumber.ToString().Contains(normalized);
        }

        return false;
    }
}


