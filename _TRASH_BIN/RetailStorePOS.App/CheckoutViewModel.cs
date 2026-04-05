using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App;

public sealed class CheckoutViewModel : ViewModelBase
{
    private const int SearchDebounceMs = 90;
    private const int BrowseResultsLimit = 24;
    private const int ActiveSearchResultsLimit = 16;

    private readonly SaleRepository _saleRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly ProductSearchService _searchService;
    private readonly Services.AuditLogService _audit;
    private readonly Services.IReceiptService _receiptPdf;
    private readonly Services.ILocalPreferencesService _localPreferences;

    private CancellationTokenSource? _searchDebounceCts;

    private string _barcodeEntry = string.Empty;
    private string _searchTerm = string.Empty;
    private Product? _selectedProduct;
    private decimal _tenderedCash;
    private string _currencyCode = "USD";
    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private bool _taxEnabled; private decimal _taxRatePercent;
    private decimal _subtotal;
    private decimal _tax;
    private decimal _total;
    private decimal _changeDue;
    private ReceiptSummary? _receipt;
    private bool _isReceiptVisible;
    private bool _isConfirmClearVisible;
    private string _statusMessage = string.Empty;

    // Quick Cash Buttons
    private decimal _quickCash1 = 5m;
    private decimal _quickCash2 = 10m;
    private decimal _quickCash3 = 20m;

    public CheckoutViewModel(
        SaleRepository saleRepository,
        SettingsRepository settingsRepository,
        ProductSearchService searchService,
        Services.AuditLogService audit,
        Services.IReceiptService receiptPdf,
        Services.ILocalPreferencesService localPreferences)
    {
        _saleRepository = saleRepository;
        _settingsRepository = settingsRepository;
        _searchService = searchService;
        _audit = audit;
        _receiptPdf = receiptPdf;
        _localPreferences = localPreferences;

        CartItems.CollectionChanged += OnCartItemsChanged;

        AddBarcodeCommand = new RelayCommand(AddByBarcode);
        AddSelectedCommand = new RelayCommand(AddSelectedProduct);
        IncreaseQtyCommand = new RelayCommand<CartItem>(IncreaseQuantity);
        DecreaseQtyCommand = new RelayCommand<CartItem>(DecreaseQuantity);
        RemoveItemCommand = new RelayCommand<CartItem>(RemoveItem);
        OverridePriceCommand = new RelayCommand<CartItem>(OverridePrice);
        TenderExactCommand = new RelayCommand(SetTenderExact);
        TenderPlus1Command = new RelayCommand(() => AddTenderAmount(QuickCash1));
        TenderPlus2Command = new RelayCommand(() => AddTenderAmount(QuickCash2));
        TenderPlus3Command = new RelayCommand(() => AddTenderAmount(QuickCash3));
        ClearCartCommand = new RelayCommand(ClearCart);
        ConfirmClearCartCommand = new RelayCommand(ConfirmClearCart);
        CancelClearCartCommand = new RelayCommand(() => IsConfirmClearVisible = false);
        CompleteSaleCommand = new RelayCommand(CompleteSale, () => CanCompleteSale);
        DismissReceiptCommand = new RelayCommand(DismissReceipt);
        OpenReceiptPdfCommand = new RelayCommand(OpenReceiptPdf);
        OpenReceiptsFolderCommand = new RelayCommand(OpenReceiptsFolder);

        LoadSettings();
        LoadLocalPreferences();
        RefreshSearchResultsImmediate();
        AppServices.SettingsUpdated += (_, _) => LoadSettings();
        AppServices.ProductsUpdated += (_, _) => RefreshSearchResultsImmediate();
        _localPreferences.PreferencesChanged += (_, e) => ApplyLocalPreferences(e.Preferences);

        AppServices.Auth.LoginStateChanged += (_, _) =>
        {
            ClearCartCore();
            OnPropertyChanged(nameof(CanOverridePrice));
            OnPropertyChanged(nameof(CurrentCashierDisplay));
        };
    }

    public ObservableCollection<CartItem> CartItems { get; } = new();
    public ObservableRangeCollection<Product> SearchResults { get; } = new();

    public string BarcodeEntry
    {
        get => _barcodeEntry;
        set => SetProperty(ref _barcodeEntry, value);
    }

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                QueueSearchRefresh();
            }
        }
    }

    public Product? SelectedProduct
    {
        get => _selectedProduct;
        set => SetProperty(ref _selectedProduct, value);
    }

    public decimal TenderedCash
    {
        get => _tenderedCash;
        set
        {
            if (SetProperty(ref _tenderedCash, value))
            {
                UpdateChangeDue();
            }
        }
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        private set => SetProperty(ref _currencyCode, value);
    }

    public string StoreName
    {
        get => _storeName;
        private set => SetProperty(ref _storeName, value);
    }

    public string StoreAddress
    {
        get => _storeAddress;
        private set => SetProperty(ref _storeAddress, value);
    }

    public bool TaxEnabled
    {
        get => _taxEnabled;
        private set
        {
            if (SetProperty(ref _taxEnabled, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal TaxRatePercent
    {
        get => _taxRatePercent;
        private set
        {
            if (SetProperty(ref _taxRatePercent, value))
            {
                RecalculateTotals();
            }
        }
    }

    public decimal Subtotal
    {
        get => _subtotal;
        private set => SetProperty(ref _subtotal, value);
    }

    public decimal Tax
    {
        get => _tax;
        private set => SetProperty(ref _tax, value);
    }

    public decimal Total
    {
        get => _total;
        private set => SetProperty(ref _total, value);
    }

    public decimal ChangeDue
    {
        get => _changeDue;
        private set => SetProperty(ref _changeDue, value);
    }

    public ReceiptSummary? Receipt
    {
        get => _receipt;
        private set => SetProperty(ref _receipt, value);
    }

    public bool IsReceiptVisible
    {
        get => _isReceiptVisible;
        private set => SetProperty(ref _isReceiptVisible, value);
    }

    public bool IsConfirmClearVisible
    {
        get => _isConfirmClearVisible;
        private set => SetProperty(ref _isConfirmClearVisible, value);
    }

    public event EventHandler<ReceiptSummary>? PrintRequested;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool CanCompleteSale => CartItems.Count > 0 && Total > 0 && TenderedCash >= Total;
    public bool CanOverridePrice => AppServices.Auth.CanOverridePrice;
    public string CurrentCashierDisplay =>
        AppServices.Auth.CurrentUser?.DisplayName
        ?? AppServices.Auth.CurrentUser?.Username
        ?? "Not signed in";
    public int SearchResultsCount => SearchResults.Count;

    public decimal QuickCash1
    {
        get => _quickCash1;
        private set => SetProperty(ref _quickCash1, value);
    }

    public decimal QuickCash2
    {
        get => _quickCash2;
        private set => SetProperty(ref _quickCash2, value);
    }

    public decimal QuickCash3
    {
        get => _quickCash3;
        private set => SetProperty(ref _quickCash3, value);
    }

    public RelayCommand AddBarcodeCommand { get; }
    public RelayCommand AddSelectedCommand { get; }
    public RelayCommand<CartItem> IncreaseQtyCommand { get; }
    public RelayCommand<CartItem> DecreaseQtyCommand { get; }
    public RelayCommand<CartItem> RemoveItemCommand { get; }
    public RelayCommand<CartItem> OverridePriceCommand { get; }
    public RelayCommand TenderExactCommand { get; }
    public RelayCommand TenderPlus1Command { get; }
    public RelayCommand TenderPlus2Command { get; }
    public RelayCommand TenderPlus3Command { get; }
    public RelayCommand ClearCartCommand { get; }
    public RelayCommand ConfirmClearCartCommand { get; }
    public RelayCommand CancelClearCartCommand { get; }
    public RelayCommand CompleteSaleCommand { get; }
    public RelayCommand DismissReceiptCommand { get; }
    public RelayCommand OpenReceiptPdfCommand { get; }
    public RelayCommand OpenReceiptsFolderCommand { get; }

    public bool HandleSearchKey(Key key)
    {
        switch (key)
        {
            case Key.Down:
                MoveSelection(1);
                return true;
            case Key.Up:
                MoveSelection(-1);
                return true;
            case Key.Enter:
                if (SelectedProduct == null && SearchResults.Count > 0)
                {
                    SelectedProduct = SearchResults[0];
                }

                if (SelectedProduct != null)
                {
                    AddSelectedProduct();
                }

                return true;
            case Key.Escape:
                SearchTerm = string.Empty;
                StatusMessage = string.Empty;
                return true;
            default:
                return false;
        }
    }

    public bool HandleBarcodeKey(Key key)
    {
        switch (key)
        {
            case Key.Enter:
                AddByBarcode();
                return true;
            case Key.Escape:
                BarcodeEntry = string.Empty;
                StatusMessage = string.Empty;
                return true;
            default:
                return false;
        }
    }

    private void MoveSelection(int direction)
    {
        if (SearchResults.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedProduct == null ? -1 : SearchResults.IndexOf(SelectedProduct);
        var nextIndex = currentIndex + direction;

        if (currentIndex == -1)
        {
            nextIndex = direction >= 0 ? 0 : SearchResults.Count - 1;
        }

        if (nextIndex < 0)
        {
            nextIndex = 0;
        }

        if (nextIndex >= SearchResults.Count)
        {
            nextIndex = SearchResults.Count - 1;
        }

        SelectedProduct = SearchResults[nextIndex];
    }

    private void LoadSettings()
    {
        CurrencyCode = _settingsRepository.GetCurrencyCode();
        StoreName = _settingsRepository.GetStoreName();
        StoreAddress = _settingsRepository.GetStoreAddress();
        var taxSettings = _settingsRepository.GetTaxSettings();
        TaxEnabled = taxSettings.Enabled;
        TaxRatePercent = taxSettings.RatePercent;
    }

    private async void LoadLocalPreferences()
    {
        var prefs = await _localPreferences.LoadPreferencesAsync();
        ApplyLocalPreferences(prefs);
    }

    private void ApplyLocalPreferences(LocalPreferences prefs)
    {
        if (Application.Current?.Dispatcher?.CheckAccess() == false)
        {
            Application.Current.Dispatcher.Invoke(() => ApplyLocalPreferences(prefs));
            return;
        }

        if (prefs.QuickCashAmounts != null && prefs.QuickCashAmounts.Length >= 3)
        {
            QuickCash1 = prefs.QuickCashAmounts[0];
            QuickCash2 = prefs.QuickCashAmounts[1];
            QuickCash3 = prefs.QuickCashAmounts[2];
        }
    }


    private void QueueSearchRefresh()
    {
        _searchDebounceCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchDebounceCts = cts;
        _ = RefreshSearchResultsDebouncedAsync(cts);
    }

    private async Task RefreshSearchResultsDebouncedAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(SearchDebounceMs, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (cts.IsCancellationRequested || !ReferenceEquals(_searchDebounceCts, cts))
        {
            return;
        }

        RefreshSearchResultsInternal(SearchTerm);
    }

    private void RefreshSearchResultsImmediate()
    {
        _searchDebounceCts?.Cancel();
        RefreshSearchResultsInternal(SearchTerm);
    }

    private void RefreshSearchResultsInternal(string query)
    {
        // Keep browse/search results intentionally small so checkout stays responsive
        // on low-end POS hardware.
        var limit = string.IsNullOrWhiteSpace(query) ? BrowseResultsLimit : ActiveSearchResultsLimit;
        var results = _searchService.Search(query, limit);

        SearchResults.ReplaceRange(results);

        SelectedProduct = SearchResults.FirstOrDefault();
        OnPropertyChanged(nameof(SearchResultsCount));
    }

    private void AddByBarcode()
    {
        var input = BarcodeEntry?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var product = _searchService.Search(input, 5).FirstOrDefault();

        if (product == null)
        {
            StatusMessage = "No product found for that barcode or name.";
            return;
        }

        AddProductToCart(product);
        BarcodeEntry = string.Empty;
        StatusMessage = string.Empty;
    }

    private void AddSelectedProduct()
    {
        if (SelectedProduct == null)
        {
            StatusMessage = "Select a product to add.";
            return;
        }

        AddProductToCart(SelectedProduct);
        StatusMessage = string.Empty;
    }

    private void AddProductToCart(Product product)
    {
        var existing = CartItems.FirstOrDefault(item => item.ProductId == product.Id);
        if (existing != null)
        {
            existing.Quantity += 1m;
            CartItems.Remove(existing);
            CartItems.Insert(0, existing);
            return;
        }

        CartItems.Insert(0, new CartItem
        {
            ProductId = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            Price = product.Price,
            OriginalPrice = product.Price,
            TaxRatePercent = product.TaxRatePercent,
            Quantity = 1m
        });
    }

    private void SetTenderExact()
    {
        TenderedCash = Total;
    }

    private void AddTenderAmount(decimal amount)
    {
        if (amount <= 0m)
        {
            return;
        }

        TenderedCash = Math.Round(TenderedCash + amount, 2, MidpointRounding.AwayFromZero);
    }

    private void IncreaseQuantity(CartItem? item)
    {
        if (item == null)
        {
            return;
        }

        item.Quantity += 1m;
    }

    private void DecreaseQuantity(CartItem? item)
    {
        if (item == null)
        {
            return;
        }

        if (item.Quantity <= 1m)
        {
            CartItems.Remove(item);
            return;
        }

        item.Quantity -= 1m;
    }

    private void RemoveItem(CartItem? item)
    {
        if (item == null)
        {
            return;
        }

        CartItems.Remove(item);
    }

    private void OverridePrice(CartItem? item)
    {
        if (item == null) return;

        if (!AppServices.Auth.CanOverridePrice)
        {
            _audit.Log("PRICE_OVERRIDE_DENIED", $"Product: {item.Name} ({item.Barcode}), User: {AppServices.Auth.CurrentUser?.Username}", AppServices.Auth.CurrentUser?.Id);
            StatusMessage = "You do not have permission to override prices.";
            return;
        }

        var oldPrice = item.Price;
        var dialog = new PriceOverrideWindow(item.Price)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.NewPrice.HasValue)
        {
            var newPrice = dialog.NewPrice.Value;
            item.Price = newPrice;
            item.IsPriceOverridden = item.Price != item.OriginalPrice;
            _audit.Log("PRICE_OVERRIDE", $"Product: {item.Name} ({item.Barcode}), Old: {oldPrice:C}, New: {newPrice:C}", AppServices.Auth.CurrentUser?.Id);
            StatusMessage = $"Price updated to {item.Price:C}.";
        }
    }

    // Shows the styled confirmation overlay — no native dialog.
    private void ClearCart()
    {
        if (CartItems.Count == 0) return;
        IsConfirmClearVisible = true;
    }

    // Called when cashier confirms "Yes, clear all" in the modal.
    private void ConfirmClearCart()
    {
        IsConfirmClearVisible = false;
        ClearCartCore();
    }

    // Clears the cart silently — called after confirmation or after CompleteSale.
    private void ClearCartCore()
    {
        CartItems.Clear();
        TenderedCash = 0m;
        StatusMessage = string.Empty;
    }

    private void CompleteSale()
    {
        if (CartItems.Count == 0)
        {
            StatusMessage = "Cart is empty.";
            return;
        }

        if (TenderedCash < Total)
        {
            StatusMessage = "Cash tendered is less than the total.";
            return;
        }

        var receiptItems = CartItems.Select(item => new CartItem
        {
            ProductId = item.ProductId,
            Name = item.Name,
            Barcode = item.Barcode,
            Price = item.Price,
            TaxRatePercent = item.TaxRatePercent,
            Quantity = item.Quantity
        }).ToList();

        var sale = new Sale
        {
            Subtotal = Subtotal,
            Tax = Tax,
            Total = Total,
            Tendered = TenderedCash,
            Change = Math.Round(TenderedCash - Total, 2, MidpointRounding.AwayFromZero),
            PaymentType = "cash",
            CashierName = CurrentCashierDisplay
        };

        foreach (var item in CartItems)
        {
            sale.Items.Add(new SaleItem
            {
                ProductId = item.ProductId,
                Name = item.Name,
                Barcode = item.Barcode,
                Price = item.Price,
                Quantity = item.Quantity,
                TaxRatePercent = item.TaxRatePercent,
                TaxAmount = item.TaxAmount,
                LineTotal = item.LineTotal
            });
        }

        var receiptNumber = _saleRepository.CreateSale(sale);

        var summary = new ReceiptSummary
        {
            SaleId = sale.Id,
            ReceiptNumber = receiptNumber,
            CreatedAt = sale.CreatedAt,
            CashierName = sale.CashierName,
            Subtotal = sale.Subtotal,
            Tax = sale.Tax,
            Total = sale.Total,
            Tendered = sale.Tendered,
            Change = sale.Change,
            Items = receiptItems
        };

        // Generate PDF silently — never blocks the sale
        summary.PdfPath = _receiptPdf.ArchiveReceipt(summary);

        if (string.IsNullOrWhiteSpace(summary.PdfPath))
        {
            var expectedPath = _receiptPdf.GetExpectedPath(receiptNumber);
            if (File.Exists(expectedPath))
            {
                summary.PdfPath = expectedPath;
            }
        }

        Receipt = summary;

        // Trigger print dialog
        PrintRequested?.Invoke(this, summary);

        IsReceiptVisible = true;
        ClearCartCore();
    }

    private void DismissReceipt()
    {
        IsReceiptVisible = false;
        Receipt = null;
    }

    private void OpenReceiptPdf()
    {
        if (Receipt == null)
        {
            StatusMessage = "No receipt is selected.";
            return;
        }

        var pdfPath = Receipt.PdfPath;
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            var expectedPath = _receiptPdf.GetExpectedPath(Receipt.ReceiptNumber);
            if (File.Exists(expectedPath))
            {
                pdfPath = expectedPath;
            }
        }

        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
        {
            StatusMessage = "Receipt PDF not found for this sale.";
            return;
        }

        ReceiptPdfService.OpenPdf(pdfPath);
    }

    private void OpenReceiptsFolder()
    {
        try
        {
            var samplePath = _receiptPdf.GetExpectedPath(1);
            var folder = Path.GetDirectoryName(samplePath);

            if (string.IsNullOrWhiteSpace(folder))
            {
                StatusMessage = "Unable to locate receipts folder.";
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
            StatusMessage = $"Unable to open receipts folder: {ex.Message}";
            AppServices.ReportException(ex, "CheckoutViewModel.OpenReceiptsFolder");
        }
    }

    private void RecalculateTotals()
    {
        Subtotal = Math.Round(CartItems.Sum(item => item.LineTotal), 2, MidpointRounding.AwayFromZero);
        // Per-item tax: sum of each item's TaxAmount (calculated from its own TaxRatePercent)
        Tax = Math.Round(CartItems.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero);
        Total = Math.Round(Subtotal + Tax, 2, MidpointRounding.AwayFromZero);
        UpdateChangeDue();
    }

    private void UpdateChangeDue()
    {
        ChangeDue = Math.Round(TenderedCash - Total, 2, MidpointRounding.AwayFromZero);
        OnPropertyChanged(nameof(CanCompleteSale));
        CompleteSaleCommand?.RaiseCanExecuteChanged();
    }

    private void OnCartItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CartItem item in e.NewItems)
            {
                item.PropertyChanged += CartItemOnPropertyChanged;
            }
        }

        if (e.OldItems != null)
        {
            foreach (CartItem item in e.OldItems)
            {
                item.PropertyChanged -= CartItemOnPropertyChanged;
            }
        }

        RecalculateTotals();
    }

    private void CartItemOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CartItem.Quantity) || e.PropertyName == nameof(CartItem.LineTotal))
        {
            RecalculateTotals();
        }
    }
}


