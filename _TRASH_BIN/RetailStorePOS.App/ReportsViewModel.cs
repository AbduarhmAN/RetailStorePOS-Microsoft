using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;


namespace RetailStorePOS.App;

public class ReportsViewModel : ViewModelBase
{
    private readonly SaleRepository _saleRepository;
    private readonly IReceiptService _receiptPdf;
    private readonly List<Sale> _rangeSales = new();
    private List<Sale> _metricScopeSales = new();
    private bool _suspendAutoLoad;

    private DateTime? _fromDate = DateTime.Today;
    private DateTime? _toDate = DateTime.Today;
    private decimal _dailySales;
    private decimal _netSales;
    private decimal _taxCollected;
    private decimal _averageTicket;
    private int _transactions;
    private decimal _cashSales;
    private decimal _cardSales;
    private decimal _otherSales;
    private double _cashSharePercent;
    private double _cardSharePercent;
    private double _otherSharePercent;
    private string _topPaymentMethod = "No sales";
    private decimal _topPaymentAmount;
    private string _searchText = string.Empty;
    private string _rangeLabel = "Today";
    private ObservableCollection<Sale> _sales;
    private string _statusText = "Loading...";
    private ReportRangePreset _activePreset = ReportRangePreset.Today;


    private enum ReportRangePreset
    {
        Custom,
        Today,
        Last7Days,
        Last30Days
    }

    public ReportsViewModel(SaleRepository saleRepository, IReceiptService receiptPdf)
    {
        _saleRepository = saleRepository;
        _receiptPdf = receiptPdf;
        _sales = new ObservableCollection<Sale>();

        LoadReportCommand = new RelayCommand(LoadReport);
        TodayRangeCommand = new RelayCommand(SetTodayRange);
        Last7DaysRangeCommand = new RelayCommand(() => SetRelativeRange(7, ReportRangePreset.Last7Days, "Last 7 days"));
        Last30DaysRangeCommand = new RelayCommand(() => SetRelativeRange(30, ReportRangePreset.Last30Days, "Last 30 days"));
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        OpenReceiptsFolderCommand = new RelayCommand(OpenReceiptsFolder);
        ReprintCommand = new RelayCommand<Sale>(ReprintReceipt);

        SetTodayRange();

        AppServices.Auth.LoginStateChanged += (_, _) =>
        {
            SetTodayRange();
        };
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (value == null || value == DateTime.MinValue) return;
            var normalized = value.Value.Date;

            if (SetProperty(ref _fromDate, normalized) && !_suspendAutoLoad)
            {
                if (_toDate == null || _toDate.Value.Date < normalized)
                {
                    _toDate = normalized;
                    OnPropertyChanged(nameof(ToDate));
                }

                SetPreset(ReportRangePreset.Custom);
                UpdateCustomRangeLabel();
                LoadReport();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (value == null || value == DateTime.MinValue) return;
            var normalized = value.Value.Date;

            if (SetProperty(ref _toDate, normalized) && !_suspendAutoLoad)
            {
                if (_fromDate == null || _fromDate.Value.Date > normalized)
                {
                    _fromDate = normalized;
                    OnPropertyChanged(nameof(FromDate));
                }

                SetPreset(ReportRangePreset.Custom);
                UpdateCustomRangeLabel();
                LoadReport();
            }
        }
    }

    public decimal DailySales
    {
        get => _dailySales;
        set => SetProperty(ref _dailySales, value);
    }

    public decimal NetSales
    {
        get => _netSales;
        set => SetProperty(ref _netSales, value);
    }

    public decimal TaxCollected
    {
        get => _taxCollected;
        set => SetProperty(ref _taxCollected, value);
    }

    public decimal AverageTicket
    {
        get => _averageTicket;
        set => SetProperty(ref _averageTicket, value);
    }

    public int Transactions
    {
        get => _transactions;
        set => SetProperty(ref _transactions, value);
    }

    public decimal CashSales
    {
        get => _cashSales;
        set => SetProperty(ref _cashSales, value);
    }

    public decimal CardSales
    {
        get => _cardSales;
        set => SetProperty(ref _cardSales, value);
    }

    public decimal OtherSales
    {
        get => _otherSales;
        set => SetProperty(ref _otherSales, value);
    }

    public double CashSharePercent
    {
        get => _cashSharePercent;
        set => SetProperty(ref _cashSharePercent, value);
    }

    public double CardSharePercent
    {
        get => _cardSharePercent;
        set => SetProperty(ref _cardSharePercent, value);
    }

    public double OtherSharePercent
    {
        get => _otherSharePercent;
        set => SetProperty(ref _otherSharePercent, value);
    }

    public string TopPaymentMethod
    {
        get => _topPaymentMethod;
        set => SetProperty(ref _topPaymentMethod, value);
    }

    public decimal TopPaymentAmount
    {
        get => _topPaymentAmount;
        set => SetProperty(ref _topPaymentAmount, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplySearchFilter();
                UpdateStatusText();
            }
        }
    }

    public string RangeLabel
    {
        get => _rangeLabel;
        set => SetProperty(ref _rangeLabel, value);
    }

    public bool IsTodayPreset => _activePreset == ReportRangePreset.Today;
    public bool IsLast7DaysPreset => _activePreset == ReportRangePreset.Last7Days;
    public bool IsLast30DaysPreset => _activePreset == ReportRangePreset.Last30Days;

    public int VisibleTransactions => Sales.Count;
    public bool HasVisibleTransactions => VisibleTransactions > 0;
    public bool HasNoVisibleTransactions => !HasVisibleTransactions;

    public ObservableCollection<Sale> Sales
    {
        get => _sales;
        set
        {
            if (SetProperty(ref _sales, value))
            {
                OnPropertyChanged(nameof(VisibleTransactions));
                OnPropertyChanged(nameof(HasVisibleTransactions));
                OnPropertyChanged(nameof(HasNoVisibleTransactions));
            }
        }
    }

    public ICommand LoadReportCommand { get; }
    public ICommand TodayRangeCommand { get; }
    public ICommand Last7DaysRangeCommand { get; }
    public ICommand Last30DaysRangeCommand { get; }
    public ICommand ClearSearchCommand { get; }
    public ICommand OpenReceiptsFolderCommand { get; }
    public RelayCommand<Sale> ReprintCommand { get; }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private void LoadReport()
    {
        try
        {
            var from = (_fromDate ?? DateTime.Today).Date;
            var to = (_toDate ?? from).Date;

            if (to < from)
            {
                (from, to) = (to, from);
            }

            _rangeSales.Clear();
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                _rangeSales.AddRange(_saleRepository.GetSalesByDate(day));
            }

            _rangeSales.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

            ApplyFiltersAndMetrics();

            if (_activePreset == ReportRangePreset.Custom)
            {
                UpdateCustomRangeLabel();
            }

            UpdateStatusText();
        }
        catch (Exception ex)
        {
            StatusText = $"ERROR: {ex.Message}";
            AppServices.ReportException(ex, "ReportsViewModel.LoadReport");
            var logPath = AppDataPaths.Combine("reports_error.log");
            File.AppendAllText(logPath,
                $"[{DateTime.Now}] {ex}\r\n");
        }
    }

    private void SetTodayRange()
    {
        SetRange(DateTime.Today, DateTime.Today, ReportRangePreset.Today, "Today");
    }

    private void SetRelativeRange(int days, ReportRangePreset preset, string label)
    {
        var end = DateTime.Today;
        var start = end.AddDays(-(days - 1));
        SetRange(start, end, preset, label);
    }

    private void SetRange(DateTime fromDate, DateTime toDate, ReportRangePreset preset, string label)
    {
        _suspendAutoLoad = true;
        FromDate = fromDate;
        ToDate = toDate;
        _suspendAutoLoad = false;

        SetPreset(preset);
        RangeLabel = label;
        LoadReport();
    }

    private void SetPreset(ReportRangePreset preset)
    {
        if (_activePreset == preset) return;

        _activePreset = preset;
        OnPropertyChanged(nameof(IsTodayPreset));
        OnPropertyChanged(nameof(IsLast7DaysPreset));
        OnPropertyChanged(nameof(IsLast30DaysPreset));
    }

    private void UpdateCustomRangeLabel()
    {
        var from = (_fromDate ?? DateTime.Today).Date;
        var to = (_toDate ?? from).Date;

        RangeLabel = from == to
            ? from.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
            : $"{from:MMM d} - {to:MMM d, yyyy}";
    }

    private void ApplyFiltersAndMetrics()
    {
        _metricScopeSales = _rangeSales
            .OrderByDescending(s => s.CreatedAt)
            .ToList();

        DailySales = _metricScopeSales.Sum(s => s.Total);
        TaxCollected = _metricScopeSales.Sum(s => s.Tax);
        NetSales = DailySales - TaxCollected;
        Transactions = _metricScopeSales.Count;
        AverageTicket = Transactions == 0
            ? 0
            : decimal.Round(DailySales / Transactions, 2, MidpointRounding.AwayFromZero);

        CashSales = DailySales;
        CardSales = 0;
        OtherSales = 0;

        if (DailySales <= 0)
        {
            CashSharePercent = 0;
            CardSharePercent = 0;
            OtherSharePercent = 0;
            TopPaymentMethod = "No sales";
            TopPaymentAmount = 0;
        }
        else
        {
            CashSharePercent = 100;
            CardSharePercent = 0;
            OtherSharePercent = 0;
            TopPaymentMethod = "Cash";
            TopPaymentAmount = CashSales;
        }

        ApplySearchFilter();
    }

    private void ApplySearchFilter()
    {
        IEnumerable<Sale> scoped = _metricScopeSales;
        var query = _searchText?.Trim() ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(query))
        {
            scoped = scoped.Where(s => MatchesSearch(s, query));
        }

        Sales = new ObservableCollection<Sale>(scoped);
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
            return localTime
                .ToString("ddd", CultureInfo.InvariantCulture)
                .Contains(normalized, StringComparison.OrdinalIgnoreCase)
                   || localTime
                       .ToString("MMMM", CultureInfo.InvariantCulture)
                       .Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static string NormalizePaymentType(string? paymentType)
    {
        if (string.IsNullOrWhiteSpace(paymentType)) return "Other";

        var normalized = paymentType.Trim();
        if (normalized.Equals("cash", StringComparison.OrdinalIgnoreCase)) return "Cash";
        if (normalized.Equals("card", StringComparison.OrdinalIgnoreCase)) return "Card";

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
    }

    private void UpdateStatusText()
    {
        var from = (_fromDate ?? DateTime.Today).Date;
        var to = (_toDate ?? from).Date;

        var rangeText = from == to
            ? from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}";

        StatusText = $"{rangeText} | {VisibleTransactions} of {Transactions} transactions shown.";
    }

    private void OpenReceiptsFolder()
    {
        try
        {
            var samplePath = _receiptPdf.GetExpectedPath(1);
            var folder = Path.GetDirectoryName(samplePath);

            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusText = "Unable to resolve receipts folder path.";
                return;
            }

            Directory.CreateDirectory(folder);

            Process.Start(new ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Unable to open receipts folder: {ex.Message}";
            AppServices.ReportException(ex, "ReportsViewModel.OpenReceiptsFolder");
        }
    }

    private void ReprintReceipt(Sale? sale)
    {
        if (sale == null) return;

        // Try the saved PDF first
        var expectedPath = _receiptPdf.GetExpectedPath(sale.ReceiptNumber);
        if (File.Exists(expectedPath))
        {
            ReceiptPdfService.OpenPdf(expectedPath);
            return;
        }

        // PDF missing — regenerate from saved sale data
        var summary = new ReceiptSummary
        {
            SaleId = sale.Id,
            ReceiptNumber = sale.ReceiptNumber,
            CreatedAt = sale.CreatedAt,
            Subtotal = sale.Subtotal,
            Tax = sale.Tax,
            Total = sale.Total,
            Tendered = sale.Tendered,
            Change = sale.Change,
            Items = sale.Items.Select(i => new CartItem
            {
                Name = i.Name,
                Price = i.Price,
                Quantity = i.Quantity,
                TaxRatePercent = i.TaxRatePercent
            }).ToList()
        };

        var path = _receiptPdf.ArchiveReceipt(summary);
        if (!string.IsNullOrEmpty(path))
            ReceiptPdfService.OpenPdf(path);
    }
}
