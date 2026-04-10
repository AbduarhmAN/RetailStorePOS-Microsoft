using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Models;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class CheckoutViewModel : ObservableObject, IDisposable
{
    private const int BrowseResultsLimit = 50;
    private const int SearchResultsLimit = 50;
    private const decimal MaxQuickCashAmount = 999999.99m;

    private readonly SaleRepository _saleRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly ProductSearchService _searchService;
    private readonly ProductRepository _productRepository;
    private readonly AuditLogService _audit;
    private readonly ILocalPreferencesService _localPreferences;
    private readonly AuthService _authService;
    private readonly DispatcherQueue _dispatcherQueue;

    private string _searchTerm = string.Empty;
    private SearchResultItem? _selectedSearchResult;
    private decimal _tenderedCash;
    private string _tenderedInputText = "0";
    private bool _isTenderedInputValid = true;
    private string _currencyCode = "USD";
    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private bool _taxEnabled;
    private decimal _subtotal;
    private decimal _tax;
    private decimal _total;
    private decimal _changeDue;
    private long _lastReceiptNumber;
    private bool _hasLastReceipt;
    private decimal _lastReceiptTotal;
    private decimal _lastReceiptChange;
    private ReceiptSummary? _receipt;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private string _statusMessage = string.Empty;
    private bool _isStatusOpen;
    private bool _isInventoryAlertOpen;
    private string _inventoryAlertMessage = string.Empty;
    private decimal _quickCash1 = 5m;
    private decimal _quickCash2 = 10m;
    private decimal _quickCash3 = 20m;
    private bool _isDiscountDialogOpen;
    private decimal _discountAmount;
    private bool _isDiscountPercentage;
    private bool _disposed;

    public CheckoutViewModel(
        SaleRepository saleRepository,
        SettingsRepository settingsRepository,
        ProductSearchService searchService,
        ProductRepository productRepository,
        AuditLogService audit,
        ILocalPreferencesService localPreferences,
        AuthService authService,
        DispatcherQueue dispatcherQueue)
    {
        _saleRepository = saleRepository;
        _settingsRepository = settingsRepository;
        _searchService = searchService;
        _productRepository = productRepository;
        _audit = audit;
        _localPreferences = localPreferences;
        _authService = authService;
        _dispatcherQueue = dispatcherQueue;

        AddSelectedCommand = new RelayCommand(() => TryAddSelectedProduct(), () => SelectedSearchResult is not null);
        IncreaseQtyCommand = new RelayCommand<CheckoutCartItem>(IncreaseQuantity);
        DecreaseQtyCommand = new RelayCommand<CheckoutCartItem>(DecreaseQuantity);
        RemoveItemCommand = new RelayCommand<CheckoutCartItem>(RemoveItem);
        TenderExactCommand = new RelayCommand(SetTenderExact);
        TenderPlus1Command = new RelayCommand(() => AddTenderAmount(QuickCash1));
        TenderPlus2Command = new RelayCommand(() => AddTenderAmount(QuickCash2));
        TenderPlus3Command = new RelayCommand(() => AddTenderAmount(QuickCash3));
        ClearCartCommand = new RelayCommand(ClearCart, () => CanClearCart);
        DismissReceiptCommand = new RelayCommand(DismissReceipt);
        OpenReceiptPdfCommand = new RelayCommand(OpenReceiptPdf);
        OpenReceiptsFolderCommand = new RelayCommand(OpenReceiptsFolder);
        CompleteSaleCommand = new RelayCommand(CompleteSale, () => CanCompleteSale);
        AddDiscountCommand = new RelayCommand(AddDiscount);

        CartItems.CollectionChanged += OnCartItemsChanged;
        _localPreferences.PreferencesChanged += OnPreferencesChanged;
        _authService.LoginStateChanged += OnLoginStateChanged;
        LoginRuntime.ProductsUpdated += OnProductsUpdated;

        LoadSettings();
        _ = LoadLocalPreferencesAsync();
        RefreshSearchResults();
    }

    public ObservableCollection<CheckoutCartItem> CartItems { get; } = new();

    public ObservableCollection<SearchResultItem> SearchResults { get; } = new();

    public RelayCommand AddSelectedCommand { get; }

    public RelayCommand<CheckoutCartItem> IncreaseQtyCommand { get; }

    public RelayCommand<CheckoutCartItem> DecreaseQtyCommand { get; }

    public RelayCommand<CheckoutCartItem> RemoveItemCommand { get; }

    public RelayCommand TenderExactCommand { get; }

    public RelayCommand TenderPlus1Command { get; }

    public RelayCommand TenderPlus2Command { get; }

    public RelayCommand TenderPlus3Command { get; }

    public RelayCommand ClearCartCommand { get; }

    public RelayCommand DismissReceiptCommand { get; }

    public RelayCommand OpenReceiptPdfCommand { get; }

    public RelayCommand OpenReceiptsFolderCommand { get; }

    public RelayCommand CompleteSaleCommand { get; }

    public RelayCommand AddDiscountCommand { get; }

    public bool CanOverridePrice => _authService.CurrentUser?.CanOverridePrice ?? false;

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                RefreshSearchResults();
            }
        }
    }

    public SearchResultItem? SelectedSearchResult
    {
        get => _selectedSearchResult;
        set
        {
            if (ReferenceEquals(_selectedSearchResult, value))
            {
                return;
            }

            if (_selectedSearchResult is not null)
            {
                _selectedSearchResult.IsSelected = false;
            }

            if (SetProperty(ref _selectedSearchResult, value))
            {
                if (_selectedSearchResult is not null)
                {
                    _selectedSearchResult.IsSelected = true;
                }

                OnPropertyChanged(nameof(CanAddSelectedProduct));
                AddSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double TenderedAmount
    {
        get => (double)_tenderedCash;
        set
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
            {
                value = 0;
            }

            if (value > (double)decimal.MaxValue)
            {
                value = (double)decimal.MaxValue;
            }

            decimal normalized;
            try
            {
                normalized = Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
            }
            catch (OverflowException)
            {
                normalized = decimal.MaxValue;
            }

            SetTenderedInputValidity(true);
            UpdateTenderedCash(normalized, syncText: true);
        }
    }

    public string TenderedInputText
    {
        get => _tenderedInputText;
        set
        {
            if (!SetProperty(ref _tenderedInputText, value))
            {
                return;
            }

            ApplyTenderedInputText(value);
        }
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        private set
        {
            if (SetProperty(ref _currencyCode, value))
            {
                RaiseMoneyTextPropertiesChanged();
                foreach (var item in CartItems)
                {
                    item.UpdateCurrencyCode(value);
                }
            }
        }
    }

    public string StoreName
    {
        get => _storeName;
        private set => SetProperty(ref _storeName, value);
    }

    public string StoreAddress
    {
        get => _storeAddress;
        private set
        {
            if (SetProperty(ref _storeAddress, value))
            {
                OnPropertyChanged(nameof(StoreAddressVisibility));
            }
        }
    }

    public Visibility StoreAddressVisibility => string.IsNullOrWhiteSpace(StoreAddress) ? Visibility.Collapsed : Visibility.Visible;

    public decimal Subtotal
    {
        get => _subtotal;
        private set
        {
            if (SetProperty(ref _subtotal, value))
            {
                OnPropertyChanged(nameof(SubtotalText));
            }
        }
    }

    public decimal Tax
    {
        get => _tax;
        private set
        {
            if (SetProperty(ref _tax, value))
            {
                OnPropertyChanged(nameof(TaxText));
            }
        }
    }

    public decimal Total
    {
        get => _total;
        private set
        {
            if (SetProperty(ref _total, value))
            {
                OnPropertyChanged(nameof(TotalText));
                OnPropertyChanged(nameof(CanCompleteSale));
            }
        }
    }

    public decimal ChangeDue
    {
        get => _changeDue;
        private set
        {
            if (SetProperty(ref _changeDue, value))
            {
                OnPropertyChanged(nameof(ChangeDueText));
            }
        }
    }

    public bool CanCompleteSale => CartItems.Count > 0 && Total > 0m && _isTenderedInputValid && _tenderedCash >= Total;

    public string CurrentCashierDisplay =>
        _authService.CurrentUser?.DisplayName
        ?? _authService.CurrentUser?.Username
        ?? "Cashier";

    public string SubtotalText => FormatMoney(Subtotal);

    public string TaxText => FormatMoney(Tax);

    public string TotalText => FormatMoney(Total);

    public string TenderedText => FormatMoney(_tenderedCash);

    public string ChangeDueText => FormatMoney(ChangeDue);

    public bool CanAddSelectedProduct => SelectedSearchResult is not null;

    public string QuickCash1Text => FormatMoney(QuickCash1);

    public string QuickCash2Text => FormatMoney(QuickCash2);

    public string QuickCash3Text => FormatMoney(QuickCash3);

    public string FormatMoneyText(decimal amount) => FormatMoney(amount);

    public decimal QuickCash1
    {
        get => _quickCash1;
        private set
        {
            var normalized = NormalizeQuickCashAmount(value);
            if (SetProperty(ref _quickCash1, normalized))
            {
                OnPropertyChanged(nameof(QuickCash1Text));
            }
        }
    }

    public decimal QuickCash2
    {
        get => _quickCash2;
        private set
        {
            var normalized = NormalizeQuickCashAmount(value);
            if (SetProperty(ref _quickCash2, normalized))
            {
                OnPropertyChanged(nameof(QuickCash2Text));
            }
        }
    }

    public decimal QuickCash3
    {
        get => _quickCash3;
        private set
        {
            var normalized = NormalizeQuickCashAmount(value);
            if (SetProperty(ref _quickCash3, normalized))
            {
                OnPropertyChanged(nameof(QuickCash3Text));
            }
        }
    }

    public bool HasLastReceipt
    {
        get => _hasLastReceipt;
        private set => SetProperty(ref _hasLastReceipt, value);
    }

    public ReceiptSummary? Receipt
    {
        get => _receipt;
        private set
        {
            if (SetProperty(ref _receipt, value))
            {
                OnPropertyChanged(nameof(ReceiptVisibility));
                OnPropertyChanged(nameof(ReceiptItems));
                OnPropertyChanged(nameof(ReceiptPdfAvailable));
                OpenReceiptPdfCommand.RaiseCanExecuteChanged();
                OpenReceiptsFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public Visibility ReceiptVisibility => Receipt is null ? Visibility.Collapsed : Visibility.Visible;

    public IEnumerable<ReceiptLineItem> ReceiptItems => Receipt is null
        ? Enumerable.Empty<ReceiptLineItem>()
        : Receipt.Items;

    public bool ReceiptPdfAvailable => Receipt is not null && !string.IsNullOrWhiteSpace(Receipt.PdfPath) && File.Exists(Receipt.PdfPath);

    public decimal CartItemsCount => CartItems.Sum(item => item.Quantity);

    public string CartItemsCountText => FormatCount(CartItemsCount, "PRODUCT", "PRODUCTS");

    public bool CanClearCart => CartItems.Count > 0;

    public Visibility EmptyCartVisibility => CartItemsCount == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CartListVisibility => CartItemsCount == 0 ? Visibility.Collapsed : Visibility.Visible;

    public int SearchResultsCount => SearchResults.Count;

    public string SearchResultsCountText => SearchResultsCount == 1 ? "1 PRODUCT" : $"{SearchResultsCount} PRODUCTS";

    public string LastReceiptNumberLabel => _lastReceiptNumber > 0 ? $"Receipt #{_lastReceiptNumber:D6}" : "No sale yet";

    public string LastReceiptTotalText => FormatMoney(_lastReceiptTotal);

    public string LastReceiptChangeText => FormatMoney(_lastReceiptChange);

    public bool IsStatusOpen
    {
        get => _isStatusOpen;
        private set => SetProperty(ref _isStatusOpen, value);
    }

    public bool IsInventoryAlertOpen
    {
        get => _isInventoryAlertOpen;
        set => SetProperty(ref _isInventoryAlertOpen, value);
    }

    public string InventoryAlertMessage
    {
        get => _inventoryAlertMessage;
        set => SetProperty(ref _inventoryAlertMessage, value);
    }

    public bool IsDiscountDialogOpen
    {
        get => _isDiscountDialogOpen;
        set => SetProperty(ref _isDiscountDialogOpen, value);
    }

    public double DiscountAmount
    {
        get => (double)_discountAmount;
        set
        {
            var normalized = Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
            SetProperty(ref _discountAmount, normalized);
            OnPropertyChanged(nameof(DiscountAmountText));
        }
    }

    public string DiscountAmountText => _isDiscountPercentage ? $"{_discountAmount:0.##}%" : $"{_currencyCode} {_discountAmount:0.00}";

    public bool IsDiscountPercentage
    {
        get => _isDiscountPercentage;
        set
        {
            if (SetProperty(ref _isDiscountPercentage, value))
            {
                OnPropertyChanged(nameof(DiscountAmountText));
            }
        }
    }

    public InfoBarSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public Visibility StatusVisibility => IsStatusOpen ? Visibility.Visible : Visibility.Collapsed;

    public Brush StatusBackgroundBrush => StatusSeverity switch
    {
        InfoBarSeverity.Success => new SolidColorBrush(ColorHelper.FromArgb(255, 238, 249, 241)),
        InfoBarSeverity.Warning => new SolidColorBrush(ColorHelper.FromArgb(255, 255, 248, 228)),
        InfoBarSeverity.Error => new SolidColorBrush(ColorHelper.FromArgb(255, 255, 242, 242)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 239, 246, 255))
    };

    public Brush StatusBorderBrush => StatusSeverity switch
    {
        InfoBarSeverity.Success => new SolidColorBrush(ColorHelper.FromArgb(255, 202, 232, 211)),
        InfoBarSeverity.Warning => new SolidColorBrush(ColorHelper.FromArgb(255, 245, 214, 118)),
        InfoBarSeverity.Error => new SolidColorBrush(ColorHelper.FromArgb(255, 247, 202, 202)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 191, 219, 254))
    };

    public Brush StatusTextBrush => StatusSeverity switch
    {
        InfoBarSeverity.Success => new SolidColorBrush(ColorHelper.FromArgb(255, 15, 138, 67)),
        InfoBarSeverity.Warning => new SolidColorBrush(ColorHelper.FromArgb(255, 146, 64, 14)),
        InfoBarSeverity.Error => new SolidColorBrush(ColorHelper.FromArgb(255, 209, 67, 67)),
        _ => new SolidColorBrush(ColorHelper.FromArgb(255, 30, 64, 175))
    };

    public void SelectSearchResult(SearchResultItem? result)
    {
        SelectedSearchResult = result;
    }

    public SearchResultItem? MoveSearchSelection(int direction)
    {
        if (SearchResults.Count == 0)
        {
            SelectedSearchResult = null;
            return null;
        }

        var currentIndex = SelectedSearchResult is null
            ? -1
            : SearchResults.IndexOf(SelectedSearchResult);

        var nextIndex = currentIndex switch
        {
            < 0 when direction > 0 => 0,
            < 0 => SearchResults.Count - 1,
            _ => Math.Clamp(currentIndex + direction, 0, SearchResults.Count - 1)
        };

        if (nextIndex < 0 || nextIndex >= SearchResults.Count)
        {
            return SelectedSearchResult;
        }

        var nextResult = SearchResults[nextIndex];
        SelectedSearchResult = nextResult;
        return nextResult;
    }

    public bool HandleSearchResultClick(SearchResultItem? result)
    {
        if (result is null)
        {
            return false;
        }

        if (SelectedSearchResult?.Product.Id == result.Product.Id)
        {
            AddProduct(result.Product);
            return true;
        }

        SelectSearchResult(result);
        return false;
    }

    public bool TryAddSelectedProduct()
    {
        return AddSelectedProduct();
    }

    public bool TrySubmitSearch()
    {
        if (TryAddExactBarcode(SearchTerm))
        {
            return true;
        }

        if (SelectedSearchResult is null && SearchResults.Count == 1)
        {
            SelectedSearchResult = SearchResults[0];
        }

        return SelectedSearchResult is not null && AddSelectedProduct();
    }

    public void SubmitCurrentSearch()
    {
        TrySubmitSearch();
    }

    public void AddProduct(Product? product, bool resetSearch = false)
    {
        if (product is null)
        {
            return;
        }

        AddProductToCart(product);
        if (resetSearch)
        {
            SearchTerm = string.Empty;
            SelectedSearchResult = null;
        }
        else
        {
            var matchingResult = SearchResults.FirstOrDefault(result => result.Product.Id == product.Id);
            SelectedSearchResult = matchingResult;
        }

        ClearStatus();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CartItems.CollectionChanged -= OnCartItemsChanged;
        _localPreferences.PreferencesChanged -= OnPreferencesChanged;
        _authService.LoginStateChanged -= OnLoginStateChanged;
        LoginRuntime.ProductsUpdated -= OnProductsUpdated;
    }

    private async Task LoadLocalPreferencesAsync()
    {
        try
        {
            var preferences = await _localPreferences.LoadPreferencesAsync();
            ApplyLocalPreferences(preferences);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.LoadLocalPreferencesAsync");
        }
    }

    private void LoadSettings()
    {
        CurrencyCode = NormalizeCurrencyCode(_settingsRepository.GetCurrencyCode());
        StoreName = _settingsRepository.GetStoreName();
        StoreAddress = _settingsRepository.GetStoreAddress();

        var taxSettings = _settingsRepository.GetTaxSettings();
        _taxEnabled = taxSettings.Enabled;
        RecalculateTotals();
    }

    private void ApplyLocalPreferences(LocalPreferences preferences)
    {
        void Apply()
        {
            if (preferences.QuickCashAmounts is { Length: >= 3 })
            {
                QuickCash1 = preferences.QuickCashAmounts[0];
                QuickCash2 = preferences.QuickCashAmounts[1];
                QuickCash3 = preferences.QuickCashAmounts[2];
            }
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            Apply();
            return;
        }

        _dispatcherQueue.TryEnqueue(Apply);
    }

    private void RefreshSearchResults()
    {
        var selectedProductId = SelectedSearchResult?.Product.Id;
        var limit = string.IsNullOrWhiteSpace(SearchTerm) ? BrowseResultsLimit : SearchResultsLimit;
        var results = _searchService.Search(SearchTerm, limit);
        if (results.Count == 0)
        {
            _searchService.RefreshIndex();
            results = _searchService.Search(SearchTerm, limit);
        }

        SearchResultItem? matchingSelectedResult = null;

        SearchResults.Clear();
        foreach (var result in results)
        {
            var searchResult = new SearchResultItem(result);
            SearchResults.Add(searchResult);

            if (selectedProductId.HasValue && result.Id == selectedProductId.Value)
            {
                matchingSelectedResult = searchResult;
            }
        }

        SelectedSearchResult = matchingSelectedResult;
        OnPropertyChanged(nameof(SearchResultsCount));
        OnPropertyChanged(nameof(SearchResultsCountText));
    }

    private bool TryAddExactBarcode(string query)
    {
        var input = query?.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var product = _searchService.Search(input, 1).FirstOrDefault();
        if (product is null)
        {
            return false;
        }

        var normalizedInput = input.ToUpperInvariant();
        if (!string.Equals(product.Barcode?.Trim().ToUpperInvariant(), normalizedInput, StringComparison.Ordinal))
        {
            return false;
        }

        AddProduct(product, resetSearch: true);
        return true;
    }

    private bool AddSelectedProduct()
    {
        if (SelectedSearchResult?.Product is null)
        {
            return false;
        }

        AddProduct(SelectedSearchResult.Product);
        return true;
    }

    private void AddProductToCart(Product product)
    {
        var canOverridePrice = CanOverridePrice;
        var existing = CartItems.FirstOrDefault(item => item.ProductId == product.Id);
        if (existing is not null)
        {
            var existingIndex = CartItems.IndexOf(existing);
            if (existingIndex > 0)
            {
                CartItems.Move(existingIndex, 0);
            }

            existing.CanOverridePrice = canOverridePrice;
            existing.Quantity += 1m;

            return;
        }

        var appliedRules = ResolveAppliedTaxRules(product);

        // Combined percentage-only rate for backward compatibility with sale_items.tax_rate_percent
        var combinedPercentageRate = appliedRules
            .Where(r => r.IsActive && r.CalcType == "PERCENTAGE")
            .Sum(r => r.RateValue);

        var item = new CheckoutCartItem
        {
            ProductId = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            Price = product.Price,
            OriginalPrice = product.Price,
            CostPrice = product.CostPrice,
            TaxRatePercent = combinedPercentageRate,
            AppliedTaxRules = appliedRules,
            Quantity = 1m,
            CanOverridePrice = canOverridePrice
        };

        item.UpdateCurrencyCode(CurrencyCode);
        item.PropertyChanged += OnCartItemPropertyChanged;
        CartItems.Insert(0, item);
    }

    /// <summary>
    /// Resolves the full list of active TaxRules for a product by looking up its
    /// tax_group_id → tax_group_rules → tax_rules.
    /// Returns an empty list if tax is disabled or no rules are found.
    /// </summary>
    private List<TaxRule> ResolveAppliedTaxRules(Product product)
    {
        if (!_taxEnabled) return new List<TaxRule>();

        try
        {
            var groupId = product.TaxGroupId ?? 0;
            if (groupId <= 0)
            {
                // No tax group assigned — fall back to legacy rate via empty rules
                // (CheckoutCartItem.TaxAmount will use TaxRatePercent as fallback)
                return new List<TaxRule>();
            }

            var allGroups = LoginRuntime.TaxGroups.GetAllWithRules();
            var group = allGroups.FirstOrDefault(g => g.Id == groupId);
            if (group == null || group.Rules.Count == 0)
            {
                return new List<TaxRule>();
            }

            return group.Rules.Where(r => r.IsActive).ToList();
        }
        catch
        {
            return new List<TaxRule>();
        }
    }

    private void IncreaseQuantity(CheckoutCartItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.Quantity += 1m;
    }

    private void DecreaseQuantity(CheckoutCartItem? item)
    {
        if (item is null)
        {
            return;
        }

        if (item.Quantity <= 1m)
        {
            RemoveItem(item);
            return;
        }

        item.Quantity -= 1m;
    }

    private void RemoveItem(CheckoutCartItem? item)
    {
        if (item is null)
        {
            return;
        }

        item.PropertyChanged -= OnCartItemPropertyChanged;
        CartItems.Remove(item);
        ClearStatus();
    }

    private void SetTenderExact()
    {
        TenderedAmount = (double)Total;
    }

    private void AddTenderAmount(decimal amount)
    {
        var normalizedAmount = NormalizeQuickCashAmount(amount);
        if (normalizedAmount <= 0m)
        {
            return;
        }

        var nextAmount = _tenderedCash > decimal.MaxValue - normalizedAmount
            ? decimal.MaxValue
            : _tenderedCash + normalizedAmount;

        TenderedAmount = (double)Math.Round(nextAmount, 2, MidpointRounding.AwayFromZero);
    }

    private void ClearCart()
    {
        foreach (var item in CartItems)
        {
            item.PropertyChanged -= OnCartItemPropertyChanged;
        }

        CartItems.Clear();
        SearchTerm = string.Empty;
        TenderedAmount = 0d;
        ClearStatus();
    }

    private void AddDiscount()
    {
        if (CartItems.Count == 0) return;

        DiscountAmount = 0;
        IsDiscountPercentage = true;
        IsDiscountDialogOpen = true;
    }

    public void ApplyDiscount(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            if (_discountAmount <= 0) return;

            decimal discountValue;
            if (_isDiscountPercentage)
            {
                discountValue = Subtotal * (_discountAmount / 100m);
            }
            else
            {
                discountValue = _discountAmount;
            }

            discountValue = Math.Round(discountValue, 2, MidpointRounding.AwayFromZero);
            if (discountValue <= 0) return;

            if (discountValue > Subtotal)
            {
                discountValue = Subtotal;
            }

            var discountItem = new CheckoutCartItem
            {
                ProductId = 0,
                Name = _isDiscountPercentage ? $"Discount ({_discountAmount:0.##}%)" : "Discount",
                OriginalPrice = -discountValue,
                CostPrice = 0m,
                Price = -discountValue,
                Quantity = 1,
                TaxRatePercent = 0
            };

            discountItem.UpdateCurrencyCode(_currencyCode);
            CartItems.Add(discountItem);
        }
        catch (Exception ex)
        {
            SetStatus(InfoBarSeverity.Error, "Unable to apply the discount right now.");
            LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.ApplyDiscount");
        }
    }

    private void CompleteSale()
    {
        try
        {
            if (CartItems.Count == 0)
            {
                SetStatus(InfoBarSeverity.Warning, "Cart is empty.");
                return;
            }

            if (_tenderedCash < Total)
            {
                SetStatus(InfoBarSeverity.Warning, "Tendered cash is less than the total.");
                return;
            }

            var sale = new Sale
            {
                Subtotal = Subtotal,
                Tax = Tax,
                Total = Total,
                Tendered = _tenderedCash,
                Change = Math.Round(_tenderedCash - Total, 2, MidpointRounding.AwayFromZero),
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
                    ItemCost = item.CostPrice,
                    Quantity = item.Quantity,
                    TaxRatePercent = _taxEnabled ? item.TaxRatePercent : 0m,
                    TaxAmount = _taxEnabled ? item.TaxAmount : 0m,
                    LineTotal = item.LineTotal,
                    TaxSnapshot = _taxEnabled ? item.TaxSnapshotJson : null
                });
            }

            var receiptNumber = _saleRepository.CreateSale(sale);
            _audit.Log("SALE_COMPLETED", $"Receipt #{receiptNumber:D6}, Total: {sale.Total:0.00}", _authService.CurrentUser?.Id);

            // Refresh product search index and notify other views so the live catalog stays in sync.
            LoginRuntime.RaiseProductsUpdated();

            // Check for low-stock products and send Windows toast notification
            CheckAndNotifyLowStock(sale.Items);

            var receipt = new ReceiptSummary
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
                PdfPath = GetReceiptPdfPath(receiptNumber)
            };

            foreach (var item in CartItems)
            {
                receipt.Items.Add(new ReceiptLineItem
                {
                    ProductId = item.ProductId,
                    Name = item.Name,
                    Barcode = item.Barcode,
                    Price = item.Price,
                    Quantity = item.Quantity,
                    CurrencyCode = CurrencyCode
                });
            }

            try
            {
                receipt.PdfPath = ReceiptHelper.ArchiveReceipt(receipt);
            }
            catch (Exception ex)
            {
                LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.ArchiveReceipt");
                receipt.PdfPath = GetReceiptPdfPath(receiptNumber);
            }

            Receipt = receipt;

            _lastReceiptNumber = receiptNumber;
            _lastReceiptTotal = sale.Total;
            _lastReceiptChange = sale.Change;
            HasLastReceipt = true;
            OnPropertyChanged(nameof(LastReceiptNumberLabel));
            OnPropertyChanged(nameof(LastReceiptTotalText));
            OnPropertyChanged(nameof(LastReceiptChangeText));

            ClearCart();
            SetStatus(InfoBarSeverity.Success, $"Sale completed. Receipt #{receiptNumber:D6} saved to the local database.");
        }
        catch (Exception ex)
        {
            SetStatus(InfoBarSeverity.Error, "Unable to complete the sale right now.");
            LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.CompleteSale");
        }
    }

    private void CheckAndNotifyLowStock(IReadOnlyList<SaleItem> soldItems)
    {
        var lowStockNames = new List<string>();
        var outOfStockNames = new List<string>();

        foreach (var item in soldItems)
        {
            var product = _productRepository.GetById(item.ProductId);
            if (product is null) continue;

            if (product.QuantityStore <= 0)
            {
                outOfStockNames.Add(product.Name);
            }
            else if (product.QuantityStore <= product.MinThresholdStore)
            {
                lowStockNames.Add($"{product.Name} ({product.QuantityStore:N0} left)");
            }
        }

        if (outOfStockNames.Count == 0 && lowStockNames.Count == 0) return;

        // Build a combined message for the in-app status
        var sb = new StringBuilder();
        if (outOfStockNames.Count > 0)
        {
            sb.Append($"⚠ OUT OF STOCK: {string.Join(", ", outOfStockNames)}. ");
        }
        if (lowStockNames.Count > 0)
        {
            sb.Append($"⚠ LOW STOCK: {string.Join(", ", lowStockNames)}. ");
        }

        var alertMessage = sb.ToString().Trim();

        // Show in-app warning in the dedicated dismissible alert
        _dispatcherQueue.TryEnqueue(() =>
        {
            InventoryAlertMessage = alertMessage;
            IsInventoryAlertOpen = true;
        });

        // Send Windows toast notification
        try
        {
            var toastTitle = outOfStockNames.Count > 0 ? "⚠ Inventory Alert" : "Low Stock Warning";
            var toastBody = alertMessage;

            var toastXml = $"""
                <toast>
                    <visual>
                        <binding template="ToastGeneric">
                            <text>{System.Security.SecurityElement.Escape(toastTitle)}</text>
                            <text>{System.Security.SecurityElement.Escape(toastBody)}</text>
                        </binding>
                    </visual>
                    <audio silent="false"/>
                </toast>
                """;

            var notification = new Microsoft.Windows.AppNotifications.AppNotification(toastXml);
            Microsoft.Windows.AppNotifications.AppNotificationManager.Default.Show(notification);
        }
        catch
        {
            // Toast notifications may fail in some environments — swallow silently
        }
    }

    private void DismissReceipt()
    {
        Receipt = null;
        ClearStatus();
    }

    private void OpenReceiptPdf()
    {
        try
        {
            var pdfPath = Receipt?.PdfPath ?? (_lastReceiptNumber > 0 ? GetReceiptPdfPath(_lastReceiptNumber) : string.Empty);
            if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            {
                if (Receipt is not null)
                {
                    try
                    {
                        pdfPath = ReceiptHelper.ArchiveReceipt(Receipt);
                        Receipt.PdfPath = pdfPath;
                    }
                    catch (Exception ex)
                    {
                        SetStatus(InfoBarSeverity.Error, "Unable to generate the receipt PDF.");
                        LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.OpenReceiptPdfArchive");
                        return;
                    }
                }
            }

            if (!ReceiptHelper.TryOpenReceiptPdf(pdfPath))
            {
                SetStatus(InfoBarSeverity.Warning, "Receipt PDF not found for this sale.");
                return;
            }
        }
        catch (Exception ex)
        {
            SetStatus(InfoBarSeverity.Error, "Unable to open the receipt PDF.");
            LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.OpenReceiptPdf");
        }
    }

    private void OpenReceiptsFolder()
    {
        try
        {
            ReceiptHelper.OpenReceiptsFolder();
        }
        catch (Exception ex)
        {
            SetStatus(InfoBarSeverity.Error, $"Unable to open receipts folder: {ex.Message}");
            LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.OpenReceiptsFolder");
        }
    }

    private void RecalculateTotals()
    {
        var subtotalSource = _taxEnabled
            ? CartItems.Sum(item => item.NetLineTotal)
            : CartItems.Sum(item => item.LineTotal);

        Subtotal = Math.Round(subtotalSource, 2, MidpointRounding.AwayFromZero);
        Tax = _taxEnabled
            ? Math.Round(CartItems.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero)
            : 0m;
        Total = Math.Round(Subtotal + Tax, 2, MidpointRounding.AwayFromZero);
        UpdateChangeDue();
    }

    private void UpdateChangeDue()
    {
        ChangeDue = _isTenderedInputValid
            ? Math.Round(_tenderedCash - Total, 2, MidpointRounding.AwayFromZero)
            : 0m;
        CompleteSaleCommand.RaiseCanExecuteChanged();
    }

    private void OnCartItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<CheckoutCartItem>())
            {
                oldItem.PropertyChanged -= OnCartItemPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<CheckoutCartItem>())
            {
                newItem.PropertyChanged -= OnCartItemPropertyChanged;
                newItem.PropertyChanged += OnCartItemPropertyChanged;
            }
        }

        RecalculateTotals();
        RaiseCartCommandStates();
        OnPropertyChanged(nameof(CartItemsCount));
        OnPropertyChanged(nameof(CartItemsCountText));
        OnPropertyChanged(nameof(CanClearCart));
        OnPropertyChanged(nameof(EmptyCartVisibility));
        OnPropertyChanged(nameof(CartListVisibility));
    }

    private void OnCartItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CheckoutCartItem.Quantity) or nameof(CheckoutCartItem.Price) or nameof(CheckoutCartItem.LineTotal))
        {
            RecalculateTotals();
            OnPropertyChanged(nameof(CartItemsCount));
            OnPropertyChanged(nameof(CartItemsCountText));
        }
    }

    private void OnPreferencesChanged(object? sender, LocalPreferencesChangedEventArgs e)
    {
        ApplyLocalPreferences(e.Preferences);
    }

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        UpdateCartPriceOverridePermissions();
        OnPropertyChanged(nameof(CurrentCashierDisplay));
        OnPropertyChanged(nameof(CanOverridePrice));
    }

    private void OnProductsUpdated(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (_dispatcherQueue.HasThreadAccess)
        {
            RefreshSearchResults();
            return;
        }

        _dispatcherQueue.TryEnqueue(RefreshSearchResults);
    }

    public bool TryOverridePrice(CheckoutCartItem? item, decimal newPrice)
    {
        try
        {
            if (item is null)
            {
                return false;
            }

            if (!CanOverridePrice)
            {
                _audit.Log("PRICE_OVERRIDE_DENIED", $"Product: {item.Name} ({item.Barcode})", _authService.CurrentUser?.Id);
                SetStatus(InfoBarSeverity.Warning, "You do not have permission to override prices.");
                return false;
            }

            if (item.ProductId <= 0)
            {
                SetStatus(InfoBarSeverity.Warning, "This line item cannot be price overridden.");
                return false;
            }

            if (newPrice < 0m)
            {
                SetStatus(InfoBarSeverity.Warning, "Price must be zero or greater.");
                return false;
            }

            var normalized = Math.Round(newPrice, 2, MidpointRounding.AwayFromZero);
            var oldPrice = item.Price;
            if (oldPrice == normalized)
            {
                SetStatus(InfoBarSeverity.Informational, "No price change applied.");
                return false;
            }

            item.Price = normalized;
            item.CanOverridePrice = CanOverridePrice;

            var auditMessage = $"Product: {item.Name} ({item.Barcode}), Old: {oldPrice:C}, New: {normalized:C}";
            _audit.Log("PRICE_OVERRIDE", auditMessage, _authService.CurrentUser?.Id);
            SetStatus(InfoBarSeverity.Success, $"Price updated for {item.Name}.");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus(InfoBarSeverity.Error, "Unable to update the price right now.");
            LoginRuntime.ReportException(ex, "WinUiCheckoutViewModel.TryOverridePrice");
            return false;
        }
    }

    private void RaiseCartCommandStates()
    {
        TenderExactCommand.RaiseCanExecuteChanged();
        TenderPlus1Command.RaiseCanExecuteChanged();
        TenderPlus2Command.RaiseCanExecuteChanged();
        TenderPlus3Command.RaiseCanExecuteChanged();
        ClearCartCommand.RaiseCanExecuteChanged();
        CompleteSaleCommand.RaiseCanExecuteChanged();
    }

    private void UpdateCartPriceOverridePermissions()
    {
        var canOverridePrice = CanOverridePrice;
        foreach (var item in CartItems)
        {
            item.CanOverridePrice = canOverridePrice;
        }
    }

    private void RaiseMoneyTextPropertiesChanged()
    {
        OnPropertyChanged(nameof(SubtotalText));
        OnPropertyChanged(nameof(TaxText));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(TenderedText));
        OnPropertyChanged(nameof(ChangeDueText));
        OnPropertyChanged(nameof(QuickCash1Text));
        OnPropertyChanged(nameof(QuickCash2Text));
        OnPropertyChanged(nameof(QuickCash3Text));
        OnPropertyChanged(nameof(LastReceiptTotalText));
        OnPropertyChanged(nameof(LastReceiptChangeText));
    }

    private void SetStatus(InfoBarSeverity severity, string message)
    {
        StatusSeverity = severity;
        StatusMessage = message;
        IsStatusOpen = !string.IsNullOrWhiteSpace(message);
        RaiseStatusPresentationChanged();
    }

    private void ClearStatus()
    {
        StatusMessage = string.Empty;
        IsStatusOpen = false;
        RaiseStatusPresentationChanged();
    }

    private void RaiseStatusPresentationChanged()
    {
        OnPropertyChanged(nameof(StatusVisibility));
        OnPropertyChanged(nameof(StatusBackgroundBrush));
        OnPropertyChanged(nameof(StatusBorderBrush));
        OnPropertyChanged(nameof(StatusTextBrush));
    }

    private static string GetReceiptPdfPath(long receiptNumber)
    {
        return ReceiptHelper.GetReceiptPdfPath(receiptNumber);
    }

    private string FormatMoney(decimal amount)
    {
        return $"{CurrencyCode} {amount:0.00}";
    }

    private void SyncTenderedInputText()
    {
        var normalizedText = _tenderedCash == decimal.Truncate(_tenderedCash)
            ? _tenderedCash.ToString("0", CultureInfo.InvariantCulture)
            : _tenderedCash.ToString("0.##", CultureInfo.InvariantCulture);

        SetProperty(ref _tenderedInputText, normalizedText, nameof(TenderedInputText));
    }

    private void ApplyTenderedInputText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            SetTenderedInputValidity(false);
            UpdateTenderedCash(0m, syncText: false);
            return;
        }

        if (NumericInputNormalization.TryParseDecimal(text, out var parsedValue))
        {
            SetTenderedInputValidity(true);
            UpdateTenderedCash(parsedValue, syncText: false);
            return;
        }

        SetTenderedInputValidity(false);
    }

    private void UpdateTenderedCash(decimal value, bool syncText = true)
    {
        var normalized = value < 0 ? 0m : Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (_tenderedCash == normalized)
        {
            return;
        }

        _tenderedCash = normalized;
        OnPropertyChanged(nameof(TenderedAmount));
        if (syncText)
        {
            SyncTenderedInputText();
        }
        OnPropertyChanged(nameof(TenderedText));
        UpdateChangeDue();
    }

    private void SetTenderedInputValidity(bool isValid)
    {
        if (_isTenderedInputValid == isValid)
        {
            return;
        }

        _isTenderedInputValid = isValid;
        CompleteSaleCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanCompleteSale));
    }

    private static string FormatCount(decimal count, string singular, string plural)
    {
        var formatted = count == decimal.Truncate(count)
            ? count.ToString("0")
            : count.ToString("0.##");

        return count == 1m
            ? $"{formatted} {singular}"
            : $"{formatted} {plural}";
    }

    private static string NormalizeCurrencyCode(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? "USD"
            : currencyCode.Trim().ToUpperInvariant();
    }

    private static decimal NormalizeQuickCashAmount(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        if (value > MaxQuickCashAmount)
        {
            value = MaxQuickCashAmount;
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}


