using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.Data.Modules.Sales;
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
        RefreshCommand = new AsyncRelayCommand(LoadReport);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        SetTodayRangeCommand = new RelayCommand(() => SetTodayRange());
        SetLast7DaysRangeCommand = new RelayCommand(SetLast7DaysRange);
        SetLast30DaysRangeCommand = new RelayCommand(SetLast30DaysRange);
        OpenReceiptFolderCommand = new RelayCommand(OpenReceiptFolder);
        ReprintReceiptCommand = new AsyncRelayCommand<Sale>(ReprintReceipt);
        ToggleTransactionsViewCommand = new RelayCommand(ToggleTransactionsView);
        GenerateXReportCommand = new AsyncRelayCommand(GenerateXReport);

        SetTodayRange(loadReport: false);
        StatusText = LocalizationHelper.GetString("ReportsReceipts_Status_Loading");
    }

    public LocalizationService Loc => LocalizationService.Instance;

    public string ReportsReceipts_Filters_Title => Loc["ReportsReceipts_Filters_Title.Text"];
    public string ReportsReceipts_Filters_Subtitle => Loc["ReportsReceipts_Filters_Subtitle.Text"];
    public string ReportsReceipts_ShownLabel => Loc["ReportsReceipts_ShownLabel.Text"];
    public string ReportsReceipts_QuickRange_Label => Loc["ReportsReceipts_QuickRange_Label.Text"];
    public string ReportsReceipts_QuickRange_Today => Loc["ReportsReceipts_QuickRange_Today.Content"];
    public string ReportsReceipts_QuickRange_7Days => Loc["ReportsReceipts_QuickRange_7Days.Content"];
    public string ReportsReceipts_QuickRange_30Days => Loc["ReportsReceipts_QuickRange_30Days.Content"];
    public string ReportsReceipts_XReport_Button => Loc["ReportsReceipts_XReport_Button.Text"];
    public string ReportsReceipts_Date_From => Loc["ReportsReceipts_Date_From.Text"];
    public string ReportsReceipts_Date_To => Loc["ReportsReceipts_Date_To.Text"];
    public string ReportsReceipts_Search_Label => Loc["ReportsReceipts_Search_Label.Text"];
    public string ReportsReceipts_Search_Box => Loc["ReportsReceipts_Search_Box.PlaceholderText"];
    public string ReportsReceipts_Search_ClearTooltip => Loc["ReportsReceipts_Search_ClearTooltip.ToolTipService.ToolTip"];
    public string ReportsReceipts_List_Header_Receipt => Loc["ReportsReceipts_List_Header_Receipt.Text"];
    public string ReportsReceipts_List_Header_Date => Loc["ReportsReceipts_List_Header_Date.Text"];
    public string ReportsReceipts_List_Header_Time => Loc["ReportsReceipts_List_Header_Time.Text"];
    public string ReportsReceipts_List_Header_Type => Loc["ReportsReceipts_List_Header_Type.Text"];
    public string ReportsReceipts_List_Header_Subtotal => Loc["ReportsReceipts_List_Header_Subtotal.Text"];
    public string ReportsReceipts_List_Header_Tax => Loc["ReportsReceipts_List_Header_Tax.Text"];
    public string ReportsReceipts_List_Header_Total => Loc["ReportsReceipts_List_Header_Total.Text"];
    public string ReportsReceipts_List_Header_Action => Loc["ReportsReceipts_List_Header_Action.Text"];
    public string ReportsReceipts_List_ReprintButton => Loc["ReportsReceipts_List_ReprintButton.Text"];
    public string ReportsReceipts_XReport_Dialog_Title => Loc["ReportsReceipts_XReport_Dialog.Title"];
    public string ReportsReceipts_XReport_Dialog_Primary => Loc["ReportsReceipts_XReport_Dialog.PrimaryButtonText"];
    public string ReportsReceipts_XReport_Dialog_Secondary => Loc["ReportsReceipts_XReport_Dialog.SecondaryButtonText"];
    public string ReportsReceipts_XReport_Dialog_Close => Loc["ReportsReceipts_XReport_Dialog.CloseButtonText"];
    public string ReportsReceipts_XReport_Dialog_Success => Loc["ReportsReceipts_XReport_Dialog_Success.Text"];
    public string ReportsReceipts_XReport_Dialog_SavedTo => Loc["ReportsReceipts_XReport_Dialog_SavedTo.Text"];
    public string ReportsReceipts_XReport_Dialog_Hint => Loc["ReportsReceipts_XReport_Dialog_Hint.Text"];

    public ObservableCollection<Sale> Sales { get; private set; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public RelayCommand SetTodayRangeCommand { get; }
    public RelayCommand SetLast7DaysRangeCommand { get; }
    public RelayCommand SetLast30DaysRangeCommand { get; }
    public RelayCommand OpenReceiptFolderCommand { get; }
    public AsyncRelayCommand<Sale> ReprintReceiptCommand { get; }
    public RelayCommand ToggleTransactionsViewCommand { get; }
    public AsyncRelayCommand GenerateXReportCommand { get; }

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
                _ = LoadReport();
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
                _ = LoadReport();
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
                _ = ApplySearchFilterAsync();
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

    public string TotalSalesText => CurrencyDisplayHelper.FormatConfiguredAmount(_totalSales);
    public string NetSalesText => CurrencyDisplayHelper.FormatConfiguredAmount(_netSales);
    public string TaxCollectedText => CurrencyDisplayHelper.FormatConfiguredAmount(_taxCollected);
    public string GrossProfitText => CurrencyDisplayHelper.FormatConfiguredAmount(_grossProfit);
    public string ProfitMarginText => _profitMargin.ToString("P1", CultureInfo.CurrentCulture);
    public string CashTotalText => CurrencyDisplayHelper.FormatConfiguredAmount(_cashTotal);
    public string CardTotalText => CurrencyDisplayHelper.FormatConfiguredAmount(_cardTotal);

    public string AverageSaleText => CurrencyDisplayHelper.FormatConfiguredAmount(_averageSale);

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
        ? LocalizationHelper.GetString("ReportsReceipts_Tooltip_Collapse")
        : LocalizationHelper.GetString("ReportsReceipts_Tooltip_Expand");

    private void ClearSearch()
    {
        SearchText = string.Empty;
    }

    private void SetTodayRange(bool loadReport = true)
    {
        SetPresets(today: true, last7: false, last30: false);
        SetRange(DateTime.Today, DateTime.Today, LocalizationHelper.GetString("ReportsReceipts_Range_Today"), loadReport);
    }

    private void SetLast7DaysRange()
    {
        SetPresets(today: false, last7: true, last30: false);
        SetRange(DateTime.Today.AddDays(-6), DateTime.Today, LocalizationHelper.GetString("ReportsReceipts_Range_Last7Days"));
    }

    private void SetLast30DaysRange()
    {
        SetPresets(today: false, last7: false, last30: true);
        SetRange(DateTime.Today.AddDays(-29), DateTime.Today, LocalizationHelper.GetString("ReportsReceipts_Range_Last30Days"));
    }

    private void OpenReceiptFolder()
    {
        ReceiptHelper.OpenReceiptsFolder();
    }

    private void ToggleTransactionsView()
    {
        IsTransactionsExpanded = !IsTransactionsExpanded;
    }

    private async Task ReprintReceipt(Sale? sale)
    {
        if (sale is null) return;
        try
        {
            var receipt = BuildReceiptSummary(sale);
            var storeName = LoginRuntime.Settings.GetStoreName() ?? LocalizationHelper.GetString("ReportsReceipts_StoreFallback");
            var storeAddress = LoginRuntime.Settings.GetStoreAddress();
            var currencyCode = LoginRuntime.Settings.GetCurrencyCode() ?? "USD";
            var preferredPrinterName = LoginRuntime.Settings.GetPreferredPrinterName();

            using var printHelper = new ReceiptPrintHelper();
            await printHelper.PrintReceiptAsync(
                IntPtr.Zero,
                null!,
                receipt,
                storeName,
                storeAddress,
                currencyCode,
                preferredPrinterName);

            StatusText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Status_ReprintSuccess"), sale.ReceiptNumber);
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Status_ReprintError"), sale.ReceiptNumber, ex.Message);
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
            _ = LoadReport();
        }
    }

    private void UpdateCustomRangeLabel()
    {
        if (IsTodayPreset || IsLast7DaysPreset || IsLast30DaysPreset) return;

        var from = FromDate?.Date ?? DateTime.Today;
        var to = ToDate?.Date ?? DateTime.Today;

        RangeLabel = from == to
            ? from.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
            : string.Format(LocalizationHelper.GetString("ReportsReceipts_Date_RangeFormat"), from.ToString("MMM d"), to.ToString("MMM d, yyyy"));
    }

    private async Task LoadReport()
    {
        try
        {
            var from = FromDate?.Date ?? DateTime.Today;
            var to = ToDate?.Date ?? DateTime.Today;

            if (to < from)
            {
                (from, to) = (to, from);
            }

            // Immediately decouple the heavy Entity Framework tracking from the Graphics Thread.
            var repoSales = await Task.Run(() => LoginRuntime.Sales.GetSalesByDateRange(from, to).ToList());

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
            _ = ApplySearchFilterAsync();
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Status_Error"), ex.Message);
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
            TopMovers.Add(new ProductVelocityItem { ProductName = kvp.Key, VelocityText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Velocity_SoldFormat"), kvp.Value) });
        }

        var slow = sortedItems.OrderBy(kvp => kvp.Value).Take(10);
        foreach (var kvp in slow)
        {
            SlowMovers.Add(new ProductVelocityItem { ProductName = kvp.Key, VelocityText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Velocity_SoldFormat"), kvp.Value) });
        }
    }

    private async Task ApplySearchFilterAsync()
    {
        var query = SearchText?.Trim() ?? string.Empty;
        var from = FromDate?.Date ?? DateTime.Today;
        var to = ToDate?.Date ?? DateTime.Today;
        var rangeText = from == to
            ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Format(LocalizationHelper.GetString("ReportsReceipts_Date_RangeFormat"), from.ToString("yyyy-MM-dd"), to.ToString("yyyy-MM-dd"));

        if (string.IsNullOrWhiteSpace(query))
        {
            // Mode 1: Safe Date Bounded Calendar Scanning (RAM)
            var scopedList = _rangeSales.ToList();
            Sales = new ObservableCollection<Sale>(scopedList);
            VisibleTransactionsCount = scopedList.Count;
            StatusText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Status_RangeInfo"), rangeText, VisibleTransactionsCount, TransactionsCount);
        }
        else
        {
            // Mode 2: Global Database Search Protocol (SQLite)
            StatusText = LocalizationHelper.GetString("ReportsReceipts_Status_Searching");

            var globalResults = await Task.Run(() => LoginRuntime.Sales.FindGlobalSales(query, 200).ToList());

            Sales = new ObservableCollection<Sale>(globalResults);
            VisibleTransactionsCount = globalResults.Count;
            StatusText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Status_SearchActive"), VisibleTransactionsCount);
        }

        OnPropertyChanged(nameof(Sales));
        HasNoVisibleTransactions = VisibleTransactionsCount == 0;
        OnPropertyChanged(nameof(EmptyStateVisibility));
        OnPropertyChanged(nameof(ReportChromeVisibility));
        OnPropertyChanged(nameof(PageRowSpacing));
    }

    public event EventHandler<string>? XReportGenerated;

    private async Task GenerateXReport()
    {
        try
        {
            var sales = await Task.Run(() => LoginRuntime.Sales.GetSalesByDate(DateTime.Today));
            var storeName = LoginRuntime.Settings.GetStoreName() ?? LocalizationHelper.GetString("ReportsReceipts_StoreFallback");
            var pdfPath = XReportHelper.GenerateXReport(sales, DateTime.Today, storeName);
            XReportGenerated?.Invoke(this, pdfPath);
        }
        catch (Exception ex)
        {
            StatusText = string.Format(LocalizationHelper.GetString("ReportsReceipts_Status_XReportError"), ex.Message);
            LoginRuntime.ReportException(ex, "ReportsViewModel.GenerateXReport");
        }
    }
}
