using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.App.Modules.Products;
using RetailStorePOS.Data.Modules.Contracts;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Modules.Products;
using RetailStorePOS.Data.Modules.Tax;
using RetailStorePOS.WinUiLogin.Common;
using RetailStorePOS.WinUiLogin.Models;
using RetailStorePOS.App.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RetailStorePOS.WinUiLogin.Views;

public sealed partial class ProductsPage : Page, INotifyPropertyChanged
{
    private static readonly WorkflowBoundary ProductMaintenanceWorkflow = InventoryWorkflowContract.ProductMaintenanceBoundary;
    private static readonly IReadOnlyList<ModuleContract> ProductMaintenanceContracts = InventoryWorkflowContract.Contracts;
    private static ProductRepository ProductsModule => InventoryWorkflowContract.ResolveProductCatalog(LoginRuntime.Products);
    private static ProductSearchService ProductSearchModule => InventoryWorkflowContract.ResolveCatalogSearch(LoginRuntime.ProductSearch);
    private static TaxCategoryRepository TaxCategoriesModule => InventoryWorkflowContract.ResolveTaxCategoryReference(LoginRuntime.TaxCategories);
    private static TaxRuleRepository TaxRulesModule => InventoryWorkflowContract.ResolveTaxRuleReference(LoginRuntime.TaxRules);
    private static TaxGroupRepository TaxGroupsModule => InventoryWorkflowContract.ResolveTaxGroupReference(LoginRuntime.TaxGroups);

    private string _searchTerm = string.Empty;
    private ProductListItem? _selectedProduct;
    private bool _isEditingSelectedProduct;
    private bool _isAddingProduct;
    private string _statusMessage = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private bool _suppressDraftPropertyChanged;
    private bool _isReloading;
    private bool _isImportInProgress;
    private bool _isImportCancelRequested;
    private int _importTotalRows;
    private int _importProcessedRows;
    private int _importCreatedCount;
    private int _importUpdatedCount;
    private int _importSkippedCount;
    private double _importProgressPercent;
    private string _importFailureTipTitle = string.Empty;
    private string _importFailureTipMessage = string.Empty;
    private bool _isImportFailureTipOpen;
    private CancellationTokenSource? _importCancellationSource;

    public ProductsPage()
    {
        InitializeComponent();
        _ = ProductMaintenanceWorkflow;
        _ = ProductMaintenanceContracts;
        Draft.PropertyChanged += Draft_PropertyChanged;
        LoginRuntime.Auth.LoginStateChanged += OnLoginStateChanged;
        Loaded += ProductsPage_Loaded;
        Unloaded += ProductsPage_Unloaded;
        TracePageState("ctor:end");
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<ProductListItem> Products { get; } = new();

    // Legacy mapping support
    private ObservableCollection<TaxCategory> _taxCategories = new();

    // New unified mapping support
    public ObservableCollection<TaxPickerItem> TaxPickerItems { get; } = new();

    private TaxPickerItem? _selectedTaxPickerItem;
    public TaxPickerItem? SelectedTaxPickerItem
    {
        get => _selectedTaxPickerItem;
        set
        {
            if (SetProperty(ref _selectedTaxPickerItem, value))
            {
                OnPropertyChanged(nameof(TaxInfoChipText));
                OnPropertyChanged(nameof(TaxInfoChipVisibility));
                RaiseEditorStateProperties();
            }
        }
    }

    public string TaxInfoChipText => SelectedTaxPickerItem?.InfoChipText ?? string.Empty;
    public Visibility TaxInfoChipVisibility => string.IsNullOrEmpty(TaxInfoChipText) ? Visibility.Collapsed : Visibility.Visible;

    public ObservableCollection<TaxCategory> TaxCategories
    {
        get => _taxCategories;
        private set
        {
            if (ReferenceEquals(_taxCategories, value))
            {
                return;
            }

            _taxCategories = value;
            OnPropertyChanged(nameof(TaxCategories));
        }
    }

    public ProductEditDraft Draft { get; } = new();

    public string SearchTerm
    {
        get => _searchTerm;
        set
        {
            if (SetProperty(ref _searchTerm, value))
            {
                ReloadProducts();
            }
        }
    }

    public ProductListItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetProperty(ref _selectedProduct, value))
            {
                try
                {
                    _suppressDraftPropertyChanged = true;
                    if (_selectedProduct is null)
                    {
                        IsEditingSelectedProduct = false;
                        Draft.Reset();
                        ResetTaxPickerToDefault();
                    }
                    else
                    {
                        Draft.LoadFrom(_selectedProduct.Product);
                        SelectMatchingTaxPickerItem();
                    }
                }
                finally
                {
                    _suppressDraftPropertyChanged = false;
                }

                RaiseSelectedProductProperties();
                RaiseEditorStateProperties();
            }
        }
    }

    public bool HasSelection => SelectedProduct is not null;
    public bool CanOverridePrice => LoginRuntime.Auth.CanOverridePrice;
    public Visibility PriceVisibility => CanOverridePrice ? Visibility.Visible : Visibility.Collapsed;
    public int ProductValueColumnSpan => CanOverridePrice ? 1 : 2;

    public bool IsEditingSelectedProduct
    {
        get => _isEditingSelectedProduct;
        private set
        {
            if (SetProperty(ref _isEditingSelectedProduct, value))
            {
                RaiseEditorStateProperties();
            }
        }
    }

    public bool IsImportInProgress
    {
        get => _isImportInProgress;
        private set
        {
            if (SetProperty(ref _isImportInProgress, value))
            {
                RaiseImportStateProperties();
                RaiseEditorStateProperties();
            }
        }
    }

    public bool CanBrowseProducts => !IsEditingSelectedProduct && !IsAddingProduct && !IsImportInProgress;

    public bool IsAddingProduct
    {
        get => _isAddingProduct;
        private set
        {
            if (SetProperty(ref _isAddingProduct, value))
            {
                RaiseEditorStateProperties();
            }
        }
    }

    public Visibility ImportProgressVisibility => IsImportInProgress ? Visibility.Visible : Visibility.Collapsed;

    public bool IsImportProgressIndeterminate => IsImportInProgress && _importTotalRows <= 0;

    public double ImportProgressPercent => _importProgressPercent;

    public string ImportProgressTitle => _isImportCancelRequested
        ? LocalizationHelper.GetString("Products_ImportProgress_Canceling")
        : LocalizationHelper.GetString("Products_ImportProgress_Title");

    public string ImportProgressDetail => _importTotalRows <= 0
        ? LocalizationHelper.GetString("Products_ImportProgress_Preparing")
        : LocalizationHelper.Format(
            "Products_ImportProgress_Detail",
            _importProcessedRows.ToString("N0", CultureInfo.CurrentCulture),
            _importTotalRows.ToString("N0", CultureInfo.CurrentCulture),
            _importCreatedCount.ToString("N0", CultureInfo.CurrentCulture),
            _importUpdatedCount.ToString("N0", CultureInfo.CurrentCulture),
            _importSkippedCount.ToString("N0", CultureInfo.CurrentCulture));

    public string ImportProgressPercentText => IsImportProgressIndeterminate
        ? string.Empty
        : LocalizationHelper.Format(
            "Products_ImportProgress_Percent",
            Math.Round(_importProgressPercent, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture));

    public bool CanCancelImport => IsImportInProgress && !_isImportCancelRequested;

    public string ImportCancelButtonText => LocalizationHelper.GetString("Products_ImportProgress_Cancel");

    public bool CanUseProductDetails => !IsImportInProgress;

    public string ImportFailureTipTitle
    {
        get => _importFailureTipTitle;
        private set => SetProperty(ref _importFailureTipTitle, value);
    }

    public string ImportFailureTipMessage
    {
        get => _importFailureTipMessage;
        private set => SetProperty(ref _importFailureTipMessage, value);
    }

    public Visibility ImportFailureTipVisibility => _isImportFailureTipOpen ? Visibility.Visible : Visibility.Collapsed;

    public string ImportFailureTipSkipText => LocalizationHelper.GetString("Products_ImportFailureTip_Skip");

    public bool CanSaveSelectedProduct => HasSelection && IsEditingSelectedProduct && HasPendingChanges() && IsDraftValid();
    public bool CanSaveNewProduct => IsAddingProduct
        && !string.IsNullOrWhiteSpace(Draft.Name)
        && Draft.TryGetPrice(out var price)
        && price >= 0;
    public bool CanExecuteSelectedProductAction => IsAddingProduct
        ? CanSaveNewProduct
        : (HasSelection && !IsEditingSelectedProduct) || CanSaveSelectedProduct;

    public string RightPanelTitleText => IsAddingProduct 
        ? LocalizationHelper.GetString("Products_Panel_NewProduct") 
        : LocalizationHelper.GetString("Products_Panel_SelectedProduct");
    public Visibility AddProductVisibility => IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DeleteButtonVisibility => HasSelection && !IsEditingSelectedProduct && !IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;

    public string CatalogSummaryText => string.IsNullOrWhiteSpace(SearchTerm)
        ? LocalizationHelper.GetString("Products_Search_Summary_All")
        : LocalizationHelper.Format("Products_Search_Summary_Filter", SearchTerm.Trim());

    public string ProductsCountText => Products.Count == 1
        ? LocalizationHelper.GetString("Products_Count_Format_Single")
        : LocalizationHelper.Format("Products_Count_Format_Plural", Products.Count.ToString("N0"));

    public string SearchStatusText => string.IsNullOrWhiteSpace(SearchTerm)
        ? LocalizationHelper.GetString("Products_Search_Status_All")
        : LocalizationHelper.GetString("Products_Search_Status_Typing");

    public string CatalogInventoryAlertText => BuildCatalogInventoryAlertText();

    public bool HasCatalogInventoryAlerts
        => Products.Any(product => product.Product.IsShelfOutOfStock || product.Product.IsWarehouseOutOfStock || product.Product.HasShelfLowAlert || product.Product.HasWarehouseLowAlert || product.Product.HasLegacyStockAlert);

    public InfoBarSeverity CatalogInventoryAlertSeverity
        => Products.Any(product => product.Product.HasLegacyStockAlert || product.Product.IsShelfOutOfStock || product.Product.IsWarehouseOutOfStock)
            ? InfoBarSeverity.Error
            : Products.Any(product => product.Product.HasShelfLowAlert || product.Product.HasWarehouseLowAlert)
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Success;

    public Visibility CatalogInventoryAlertVisibility => HasCatalogInventoryAlerts ? Visibility.Visible : Visibility.Collapsed;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(StatusVisibility));
            }
        }
    }

    public InfoBarSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public Visibility StatusVisibility => IsStatusOpen
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool IsStatusOpen => !string.IsNullOrWhiteSpace(StatusMessage);

    public string SelectedProductNameText => SelectedProduct?.Name ?? LocalizationHelper.GetString("Products_Selection_None");

    public string SelectedProductSubtitleText => SelectedProduct is null
        ? LocalizationHelper.GetString("Products_Selection_Subtitle_None")
        : IsEditingSelectedProduct
            ? LocalizationHelper.GetString("Products_Selection_Subtitle_Editing")
            : LocalizationHelper.GetString("Products_Selection_Subtitle_Normal");

    public string SelectedProductModeText => IsAddingProduct
        ? LocalizationHelper.GetString("Products_Mode_Adding")
        : SelectedProduct is null
        ? LocalizationHelper.GetString("Products_Mode_None")
        : IsEditingSelectedProduct
            ? LocalizationHelper.GetString("Products_Mode_Editing")
            : LocalizationHelper.GetString("Products_Mode_Normal");

    public string SelectedProductBarcodeText => FormatText(SelectedProduct?.Product.Barcode);
    public string SelectedProductSkuText => FormatText(SelectedProduct?.Product.Sku);
    public string SelectedProductUnitText => FormatText(SelectedProduct?.Product.Unit);
    public string SelectedProductPriceText => SelectedProduct is null
        ? "—"
        : ProductPriceFormatter.Format(SelectedProduct.Product.Price);
    public string SelectedProductCostPriceText => SelectedProduct is null
        ? "—"
        : ProductPriceFormatter.Format(SelectedProduct.Product.CostPrice);
    public string SelectedProductTaxText
    {
        get
        {
            if (SelectedProduct is null) return "—";
            var groupId = SelectedProduct.Product.TaxGroupId ?? 1;

            // First check non-auto-managed profiles already in the picker list
            var pickerItem = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Profile && i.Profile?.Id == groupId);
            if (pickerItem != null)
            {
                return $"{pickerItem.DisplayName} ({pickerItem.InfoChipText})";
            }

            // Check if it maps to a Rule item in the picker (user selected a single rule)
            var ruleItem = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Rule && i.Rule?.Id == groupId);
            if (ruleItem != null)
            {
                return ruleItem.InfoChipText;
            }

            // Resolve auto-managed profiles by querying the database
            try
            {
                var group = TaxGroupsModule.GetAllWithRules().FirstOrDefault(g => g.Id == groupId);
                if (group != null)
                {
                    if (group.IsAutoManaged && group.Rules.Count == 1)
                        return LocalizationHelper.Format("Products_Tax_AutoManaged_Format", group.Rules[0].Name, FormatTaxRuleDisplaySummary(group.Rules[0]), FormatTaxRuleInclusiveLabel(group.Rules[0]));
                    return $"{group.Name} ({FormatTaxGroupRulesSummary(group)})";
                }
            }
            catch { /* Graceful fallback */ }

            return "—";
        }
    }

    public string SelectedProductStoreQuantityText => SelectedProduct is null
        ? "—"
        : FormatNumberWithUnit(SelectedProduct.Product.QuantityStore, SelectedProduct.Product.Unit);
    public string SelectedProductWarehouseQuantityText => SelectedProduct is null
        ? "—"
        : FormatNumberWithUnit(SelectedProduct.Product.QuantityWarehouse, SelectedProduct.Product.Unit);
    public string SelectedProductTotalQuantityText => SelectedProduct is null
        ? "—"
        : ProductPriceFormatter.FormatNumber(SelectedProduct.Product.TotalQuantity);
    public string SelectedProductStoreThresholdText => SelectedProduct is null
        ? "—"
        : FormatNumberWithUnit(SelectedProduct.Product.MinThresholdStore, SelectedProduct.Product.Unit);
    public string SelectedProductWarehouseThresholdText => SelectedProduct is null
        ? "—"
        : FormatNumberWithUnit(SelectedProduct.Product.MinThresholdWarehouse, SelectedProduct.Product.Unit);
    public string SelectedProductPurchasedAtText => SelectedProduct is null
        ? "—"
        : FormatDate(SelectedProduct.Product.PurchasedAt);
    public string SelectedProductLastSaleAtText => SelectedProduct is null
        ? "—"
        : FormatDateTime(SelectedProduct.Product.LastSaleAt);
    public string SelectedProductQuantityText => SelectedProductStoreQuantityText;
    public string SelectedProductInventoryAlertText => SelectedProduct is null
        ? string.Empty
        : BuildInventoryAlertText(SelectedProduct.Product);
    public bool SelectedProductHasInventoryAlert => SelectedProduct is not null && HasInventoryAlert(SelectedProduct.Product);
    public InfoBarSeverity SelectedProductInventoryAlertSeverity => SelectedProduct is null
        ? InfoBarSeverity.Informational
        : GetInventoryAlertSeverity(SelectedProduct.Product);
    public Visibility SelectedProductInventoryAlertVisibility => SelectedProduct is not null && HasInventoryAlert(SelectedProduct.Product)
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string SelectedProductActionText => IsAddingProduct 
        ? LocalizationHelper.GetString("Products_Action_Save") 
        : IsEditingSelectedProduct 
            ? LocalizationHelper.GetString("Products_Action_Save") 
            : LocalizationHelper.GetString("Products_Action_Edit");
    public Symbol SelectedProductActionSymbol => (IsAddingProduct || IsEditingSelectedProduct) ? Symbol.Save : Symbol.Edit;

    public string SelectedProductEditHintText => IsEditingSelectedProduct
        ? LocalizationHelper.GetString("Products_Edit_Hint")
        : string.Empty;

    public string SelectedProductDraftTaxRateText => GetTaxCategoryRateText(Draft.TaxCategoryId);

    public Visibility HasSelectionVisibility => HasSelection ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProductImageContainerVisibility => HasSelection || IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProductImageEditorVisibility => IsEditingSelectedProduct || IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;
    public string ProductImageActionText => Draft.HasImage
        ? LocalizationHelper.GetString("Products_ImageAction_ChangePicture")
        : LocalizationHelper.GetString("Products_ImageAction_AddPicture");
    public Visibility NoSelectionVisibility => !HasSelection && !IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedProductViewVisibility => HasSelection && !IsEditingSelectedProduct && !IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedProductEditVisibility => HasSelection && IsEditingSelectedProduct && !IsAddingProduct ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SelectedProductCancelVisibility => (IsEditingSelectedProduct || IsAddingProduct) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoProductsVisibility => Products.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void ProductsPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            Loaded -= ProductsPage_Loaded;
            TracePageState("Loaded:after unsubscribe");
            TracePageState("Loaded:start");
            LoadTaxCategories();
            LoadTaxPickerItems();
            TracePageState("Loaded:after LoadTaxCategories");
            ReloadProducts();
            TracePageState("Loaded:after ReloadProducts");
            SearchTextBox.Focus(FocusState.Programmatic);
            TracePageState("Loaded:after SearchTextBox.Focus");
            TracePageState("Loaded:end");
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.Loaded");
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_Load"), InfoBarSeverity.Error);
        }
    }

    private void ProductsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        RequestImportCancellation();
        DismissImportFailureTip();
        LoginRuntime.Auth.LoginStateChanged -= OnLoginStateChanged;
        Unloaded -= ProductsPage_Unloaded;
    }

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        RaiseSelectedProductProperties();
        RaiseEditorStateProperties();
    }

    private void ReloadProducts()
    {
        if (_isReloading) return;
        _isReloading = true;
        try
        {
            TracePageState("ReloadProducts:start");
            var previousId = SelectedProduct?.Id;
            var products = ProductSearchModule.Search(SearchTerm, 500);

            Products.Clear();
            foreach (var product in products)
            {
                Products.Add(new ProductListItem(product));
            }

            ProductListItem? nextSelected = null;
            if (previousId is > 0)
            {
                nextSelected = Products.FirstOrDefault(item => item.Id == previousId);
            }

            SelectedProduct = nextSelected ?? Products.FirstOrDefault();
            ProductsListView.SelectedItem = SelectedProduct;

            if (Products.Count == 0)
            {
                IsEditingSelectedProduct = false;
                ResetDraftSilently();
            }

            RaiseCatalogProperties();
            RaiseEditorStateProperties();
            TracePageState("ReloadProducts:end");
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.ReloadProducts");
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_Refresh"), InfoBarSeverity.Error);
        }
        finally
        {
            _isReloading = false;
        }
    }

    private void LoadTaxCategories()
    {
        try
        {
            var categories = TaxCategoriesModule.GetAll().ToList();
            TaxCategories = new ObservableCollection<TaxCategory>(categories);

            if (TaxCategories.Count == 0)
            {
                Draft.TaxCategoryId = 0;
            }
            else if (!TaxCategories.Any(category => category.Id == Draft.TaxCategoryId))
            {
                Draft.TaxCategoryId = TaxCategories[0].Id;
            }

            RaiseSelectedProductProperties();
            RaiseEditorStateProperties();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.LoadTaxCategories");
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_TaxLoad"), InfoBarSeverity.Error);
        }
    }

    private void LoadTaxPickerItems()
    {
        try
        {
            TaxPickerItems.Clear();

            var allRules = TaxRulesModule.GetAll().ToList();
            var allProfiles = TaxGroupsModule.GetAllWithRules()
                                .Where(g => !g.IsAutoManaged)
                                .ToList();

            // Group 1: Common
            TaxPickerItems.Add(new TaxPickerItem { Kind = TaxPickerKind.Header, DisplayName = LocalizationHelper.GetString("Products_Tax_Header_Common") });
            var noTax = allProfiles.FirstOrDefault(g => g.IsDefault);
            if (noTax != null) TaxPickerItems.Add(BuildProfileItem(noTax));
            foreach (var rule in allRules.Take(5))
                TaxPickerItems.Add(BuildRuleItem(rule));

            // Group 2: Tax Profiles
            if (allProfiles.Any(g => !g.IsDefault))
            {
                TaxPickerItems.Add(new TaxPickerItem { Kind = TaxPickerKind.Header, DisplayName = LocalizationHelper.GetString("Products_Tax_Header_Profiles") });
                foreach (var profile in allProfiles.Where(g => !g.IsDefault))
                    TaxPickerItems.Add(BuildProfileItem(profile));
            }

            // Group 3: All Individual Rates
            TaxPickerItems.Add(new TaxPickerItem { Kind = TaxPickerKind.Header, DisplayName = LocalizationHelper.GetString("Products_Tax_Header_AllRates") });
            foreach (var rule in allRules)
                TaxPickerItems.Add(BuildRuleItem(rule));

            // Select matching item for current draft
            SelectMatchingTaxPickerItem();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.LoadTaxPickerItems");
        }
    }

    private TaxPickerItem BuildRuleItem(TaxRule rule) => new()
    {
        Kind = TaxPickerKind.Rule,
        Rule = rule,
        DisplayName = rule.Name,
        Subtitle = LocalizationHelper.Format("Products_Tax_Subtitle_Format", FormatTaxRuleDisplaySummary(rule), FormatTaxRuleScopeLabel(rule)),
        InfoChipText = LocalizationHelper.Format("Products_Tax_InfoChip_Format", FormatTaxRuleDisplaySummary(rule), FormatTaxRuleInclusiveLabel(rule), FormatTaxRuleScopeLabel(rule))
    };

    private TaxPickerItem BuildProfileItem(TaxGroup group) => new()
    {
        Kind = TaxPickerKind.Profile,
        Profile = group,
        DisplayName = group.Name,
        Subtitle = FormatTaxGroupDisplaySummary(group),
        InfoChipText = FormatTaxGroupRulesSummary(group)
    };

    private static string FormatTaxRuleDisplaySummary(TaxRule rule) => rule.CalcType switch
    {
        "PERCENTAGE" => $"{rule.RateValue:G}%",
        "FIXED_AMOUNT" => ProductPriceFormatter.Format(rule.RateValue),
        "TIERED" => LocalizationHelper.GetString("Products_Tax_Display_TieredRates"),
        "PER_UNIT_MEASURE" => LocalizationHelper.Format("Products_Tax_Display_PerUnit_Format", ProductPriceFormatter.Format(rule.RateValue)),
        "PERCENTAGE_ON_MARGIN" => LocalizationHelper.Format("Products_Tax_Display_PercentageOnMargin_Format", $"{rule.RateValue:G}%"),
        "REVERSE_CHARGE" => LocalizationHelper.GetString("Products_Tax_Display_ReverseCharge"),
        _ => rule.RateValue.ToString("G", CultureInfo.CurrentCulture)
    };

    private static string FormatTaxRuleInclusiveLabel(TaxRule rule)
        => LocalizationHelper.GetString(rule.IsInclusive ? "Products_Tax_Inclusive" : "Products_Tax_Exclusive");

    private static string FormatTaxRuleScopeLabel(TaxRule rule)
        => LocalizationHelper.GetString(string.Equals(rule.Scope, "ORDER", StringComparison.OrdinalIgnoreCase)
            ? "Products_Tax_Scope_Order"
            : "Products_Tax_Scope_Product");

    private static string FormatTaxGroupDisplaySummary(TaxGroup group)
        => group.IsActive
            ? group.Name
            : LocalizationHelper.Format("Products_Tax_Inactive_Format", group.Name);

    private static string FormatTaxGroupRulesSummary(TaxGroup group)
        => group.Rules is { Count: > 0 }
            ? string.Join(", ", group.Rules.Select(FormatTaxRuleDisplaySummary))
            : LocalizationHelper.GetString("Products_Tax_NoRules");

    private void TaxPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox combo || combo.SelectedItem is not TaxPickerItem item) return;

        if (item.IsHeader)
        {
            // Revert selection if user somehow clicks a header
            combo.SelectedItem = SelectedTaxPickerItem;
            return;
        }

        SelectedTaxPickerItem = item;
    }

    private void SelectMatchingTaxPickerItem()
    {
        if (Draft.TaxGroupId <= 0) return;

        // Find profile match (including default)
        var profileMatch = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Profile && i.Profile?.Id == Draft.TaxGroupId);
        if (profileMatch != null)
        {
            SelectedTaxPickerItem = profileMatch;
            return;
        }

        // If it was an auto-managed profile, we need to match the single rule
        try
        {
            var matchGroup = TaxGroupsModule.GetAllWithRules().FirstOrDefault(g => g.Id == Draft.TaxGroupId);
            if (matchGroup is { IsAutoManaged: true } && matchGroup.Rules.Count == 1)
            {
                var ruleId = matchGroup.Rules[0].Id;
                var ruleMatch = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Rule && i.Rule?.Id == ruleId);
                if (ruleMatch != null)
                {
                    SelectedTaxPickerItem = ruleMatch;
                    return;
                }
            }
        }
        catch { /* Ignore lookup errors during selection match */ }

        // Fallback to default
        SelectedTaxPickerItem = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Profile && i.Profile?.IsDefault == true);
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!CanBrowseProducts)
            {
                return;
            }

            LoginRuntime.RaiseProductsUpdated();
            ReloadProducts();
            SearchTextBox.Focus(FocusState.Programmatic);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.RefreshButton_Click");
            SetStatus("Unable to refresh the catalog right now.", InfoBarSeverity.Error);
        }
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanBrowseProducts)
        {
            return;
        }

        SearchTerm = string.Empty;
        SearchTextBox.Focus(FocusState.Programmatic);
    }

    private void ProductsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isReloading) return;

        if (!CanBrowseProducts)
        {
            TracePageState("SelectionChanged:blocked");
            ProductsListView.SelectedItem = SelectedProduct;
            return;
        }

        TracePageState("SelectionChanged:before");
        SelectedProduct = ProductsListView.SelectedItem as ProductListItem;
        TracePageState("SelectionChanged:after");
    }

    private void AddProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanBrowseProducts) return;
        TracePageState("AddButton:start");
        // Clear selection first so its setter fires cleanly before we enter Add mode
        SelectedProduct = null;
        ProductsListView.SelectedItem = null;
        IsEditingSelectedProduct = false;
        ResetDraftSilently();
        ResetTaxPickerToDefault();
        // Set IsAddingProduct last so all visibility/state properties compute correctly
        IsAddingProduct = true;
        SetStatus(LocalizationHelper.GetString("Products_Mode_Adding"), InfoBarSeverity.Informational);
        RaiseSelectedProductProperties();
        RaiseEditorStateProperties();
        TracePageState("AddButton:end");
    }

    private async void ImportDataButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanBrowseProducts) return;

        try
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.List;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".csv");

            var hwnd = WindowNative.GetWindowHandle(MainWindow.Current);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file is null) return;

            DismissImportFailureTip();
            SetStatus(LocalizationHelper.GetString("Products_Status_Importing"), InfoBarSeverity.Informational);
            BeginImportOperation();

            var service = new ProductImportService(LoginRuntime.ConnectionFactory);
            var importDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(file.Path)) ?? string.Empty;
            var importFileStem = System.IO.Path.GetFileNameWithoutExtension(file.Path);
            var importImageDirectories = Directory.Exists(importDirectory)
                ? Directory.GetDirectories(importDirectory, "*_images", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();
            var progress = new Progress<ProductImportProgress>(progressUpdate =>
            {
                if (DispatcherQueue.HasThreadAccess)
                {
                    UpdateImportProgress(progressUpdate);
                    return;
                }

                _ = DispatcherQueue.TryEnqueue(() => UpdateImportProgress(progressUpdate));
            });
            var cancellationToken = _importCancellationSource?.Token ?? CancellationToken.None;

            var result = await Task.Run<ImportResult?>(() =>
            {
                try
                {
                    var imageService = new ProductImageService();
                    var importedThumbnailCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

                    string? ResolveImportedSourcePath(string imagePath)
                    {
                        if (string.IsNullOrWhiteSpace(imagePath))
                        {
                            return null;
                        }

                        var fileName = System.IO.Path.GetFileName(imagePath);
                        var declaredFolderName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(imagePath)?.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
                        var candidates = new List<string>();

                        if (System.IO.Path.IsPathRooted(imagePath))
                        {
                            candidates.Add(System.IO.Path.GetFullPath(imagePath));
                        }
                        else
                        {
                            candidates.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(importDirectory, imagePath)));
                            candidates.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(importDirectory, fileName)));
                            candidates.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(importDirectory, $"{importFileStem}_images", fileName)));

                            if (!string.IsNullOrWhiteSpace(declaredFolderName))
                            {
                                candidates.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(importDirectory, declaredFolderName, fileName)));
                            }

                            foreach (var imageDirectory in importImageDirectories)
                            {
                                candidates.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(imageDirectory, fileName)));
                            }
                        }

                        return candidates
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .FirstOrDefault(System.IO.File.Exists);
                    }

                    string? ResolveImportedThumbnail(ProductCsvRow row)
                    {
                        if (string.IsNullOrWhiteSpace(row.ImagePath))
                        {
                            return null;
                        }

                        var sourcePath = ResolveImportedSourcePath(row.ImagePath);
                        if (string.IsNullOrWhiteSpace(sourcePath))
                        {
                            return null;
                        }

                        if (importedThumbnailCache.TryGetValue(sourcePath, out var cachedThumbnailPath))
                        {
                            return cachedThumbnailPath;
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        var generatedThumbnailPath = imageService.GenerateThumbnailAsync(sourcePath).GetAwaiter().GetResult();
                        importedThumbnailCache[sourcePath] = generatedThumbnailPath;
                        return generatedThumbnailPath;
                    }

                    return service.ImportFromCsv(
                        file.Path,
                        progress,
                        cancellationToken,
                        ResolveImportedThumbnail);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            });

            if (result is null)
            {
                EndImportOperation();
                SetStatus(LocalizationHelper.GetString("Products_Status_ImportCanceled"), InfoBarSeverity.Informational);
                return;
            }

            EndImportOperation();
            LoginRuntime.RaiseProductsUpdated();
            ReloadProducts();

            SetStatus(
                LocalizationHelper.Format(
                    "Products_Status_ImportComplete",
                    result.CreatedCount.ToString("N0", CultureInfo.CurrentCulture),
                    result.UpdatedCount.ToString("N0", CultureInfo.CurrentCulture),
                    result.SkippedCount.ToString("N0", CultureInfo.CurrentCulture)),
                result.SkippedCount > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);

            if (result.HasErrors)
            {
                ShowImportWarningsTip(result.Errors);
            }
        }
        catch (Exception ex)
        {
            EndImportOperation();
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.ImportDataButton_Click");
            ShowImportFailureTip(ex.Message);
            SetStatus(LocalizationHelper.GetString("Products_Status_ImportFailed"), InfoBarSeverity.Error);
        }
    }

    private void CancelImportButton_Click(object sender, RoutedEventArgs e)
    {
        RequestImportCancellation();
    }

    private void ImportFailureTipSkipButton_Click(object sender, RoutedEventArgs e)
    {
        DismissImportFailureTip();
    }

    private async void ExportDataButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CanBrowseProducts) return;

        try
        {
            var picker = new FileSavePicker();
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("CSV Document", new System.Collections.Generic.List<string>() { ".csv" });
            picker.SuggestedFileName = "ProductsExport";

            var hwnd = WindowNative.GetWindowHandle(MainWindow.Current);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            SetStatus(LocalizationHelper.GetString("Products_Status_Exporting"), InfoBarSeverity.Informational);

            var products = ProductsModule.GetAll();
            var service = new ProductExportService();
            var imageService = new ProductImageService();

            await Task.Run(() => service.ExportToCsv(file.Path, products, p => imageService.ResolveThumbnailPath(p.ThumbnailPath)));

            SetStatus(LocalizationHelper.GetString("Products_Status_ExportComplete"), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.ExportDataButton_Click");
            SetStatus($"Export failed: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void SelectedProductActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsAddingProduct)
        {
            await SaveNewProduct();
            return;
        }

        if (!HasSelection)
        {
            return;
        }

        if (IsEditingSelectedProduct)
        {
            await SaveSelectedProduct();
            return;
        }

        BeginEdit();
    }

    private async void ChangePictureButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");

            var hwnd = WindowNative.GetWindowHandle(MainWindow.Current);
            InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                Draft.PendingSourceImagePath = file.Path;
                Draft.PendingRemoveImage = false;
                RaiseEditorStateProperties();
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.ChangePictureButton_Click");
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_ImagePick"), InfoBarSeverity.Error);
        }
    }

    private void RemovePictureButton_Click(object sender, RoutedEventArgs e)
    {
        Draft.PendingSourceImagePath = null;
        Draft.PendingRemoveImage = true;
        RaiseEditorStateProperties();
    }

    private void CancelEditButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEdit();
    }

    private async void TransferStockButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (SelectedProduct is null) return;

            TransferQuantityBox.Value = 1;
            TransferQuantityBox.Maximum = Math.Max(1, (double)SelectedProduct.Product.QuantityWarehouse);

            await TransferStockDialog.ShowAsync();
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.TransferStockButton_Click");
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_TransferOpen"), InfoBarSeverity.Error);
        }
    }

    private void TransferStockDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        try
        {
            if (SelectedProduct is null) return;

            var transferAmount = (decimal)TransferQuantityBox.Value;
            if (transferAmount <= 0) return;

            var product = SelectedProduct.Product;

            product.QuantityWarehouse -= transferAmount;
            product.QuantityStore += transferAmount;

            ProductsModule.Update(product);
            LoginRuntime.RaiseProductsUpdated();

            ReloadProducts();

            SetStatus(LocalizationHelper.Format("Products_Status_Transferred", transferAmount), InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.TransferStockDialog_PrimaryButtonClick");
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_Transfer"), InfoBarSeverity.Error);
        }
    }

    private void BeginEdit()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        Draft.LoadFrom(SelectedProduct.Product);
        SelectMatchingTaxPickerItem();
        IsEditingSelectedProduct = true;
        SetStatus(LocalizationHelper.GetString("Products_Status_EditMode"), InfoBarSeverity.Informational);
        RaiseEditorStateProperties();
    }

    private void CancelEdit()
    {
        TracePageState("CancelEdit:start");
        if (IsAddingProduct)
        {
            IsEditingSelectedProduct = false;
            TracePageState("CancelEdit:after IsEditingSelectedProduct false");
            ResetDraftSilently();
            TracePageState("CancelEdit:after Draft.Reset");
            ProductsListView.SelectedItem = null;
            TracePageState("CancelEdit:after ProductsListView.SelectedItem null");
            SelectedProduct = null;
            TracePageState("CancelEdit:after SelectedProduct null");
            IsAddingProduct = false;
            TracePageState("CancelEdit:after IsAddingProduct false");
            SetStatus(string.Empty, InfoBarSeverity.Informational);
            RaiseCatalogProperties();
            RaiseSelectedProductProperties();
            RaiseEditorStateProperties();
            TracePageState("CancelEdit:after RaiseProperties");

            if (Products.Count == 0)
            {
                SearchTextBox.Focus(FocusState.Programmatic);
            }
            TracePageState("CancelEdit:end");
            return;
        }

        if (SelectedProduct is null)
        {
            IsEditingSelectedProduct = false;
            ResetDraftSilently();
            SetStatus(string.Empty, InfoBarSeverity.Informational);
            TracePageState("CancelEdit:no-selection:end");
            return;
        }

        Draft.LoadFrom(SelectedProduct.Product);
        SelectMatchingTaxPickerItem();
        IsEditingSelectedProduct = false;
        SetStatus(LocalizationHelper.GetString("Products_Status_Discarded"), InfoBarSeverity.Informational);
        RaiseEditorStateProperties();
        TracePageState("CancelEdit:edit-end");
    }

    private async Task SaveSelectedProduct()
    {
        if (SelectedProduct is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Draft.Name))
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_NameRequired"), InfoBarSeverity.Error);
            return;
        }

        var price = SelectedProduct.Product.Price;
        var costPrice = SelectedProduct.Product.CostPrice;

        if (CanOverridePrice)
        {
            if (!Draft.TryGetPrice(out price))
            {
                SetStatus(LocalizationHelper.GetString("Products_Status_Error_InvalidPrice"), InfoBarSeverity.Error);
                return;
            }

            if (price < 0)
            {
                SetStatus(LocalizationHelper.GetString("Products_Status_Error_PriceNegative"), InfoBarSeverity.Error);
                return;
            }

            if (!Draft.TryGetCostPrice(out costPrice))
            {
                SetStatus(LocalizationHelper.GetString("Products_Status_Error_InvalidCostPrice"), InfoBarSeverity.Error);
                return;
            }

            if (costPrice < 0)
            {
                SetStatus(LocalizationHelper.GetString("Products_Status_Error_CostPriceNegative"), InfoBarSeverity.Error);
                return;
            }
        }

        if (!Draft.TryGetStoreQuantity(out var storeQuantity))
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_InvalidStoreQty"), InfoBarSeverity.Error);
            return;
        }

        if (!Draft.TryGetWarehouseQuantity(out var warehouseQuantity))
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_InvalidWarehouseQty"), InfoBarSeverity.Error);
            return;
        }

        if (storeQuantity < 0 || warehouseQuantity < 0)
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_QtyNegative"), InfoBarSeverity.Error);
            return;
        }

        if (!Draft.TryGetStoreThreshold(out var storeThreshold))
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_InvalidStoreThreshold"), InfoBarSeverity.Error);
            return;
        }

        if (!Draft.TryGetWarehouseThreshold(out var warehouseThreshold))
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_InvalidWarehouseThreshold"), InfoBarSeverity.Error);
            return;
        }

        if (storeThreshold < 0 || warehouseThreshold < 0)
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_Error_ThresholdNegative"), InfoBarSeverity.Error);
            return;
        }

        string? finalThumbnailPath = SelectedProduct.Product.ThumbnailPath;

        try
        {
            if (Draft.PendingRemoveImage)
            {
                var imageService = new ProductImageService();
                imageService.TryCleanupThumbnail(finalThumbnailPath);
                finalThumbnailPath = null;
            }
            else if (!string.IsNullOrEmpty(Draft.PendingSourceImagePath))
            {
                var imageService = new ProductImageService();
                imageService.TryCleanupThumbnail(finalThumbnailPath);
                finalThumbnailPath = await imageService.GenerateThumbnailAsync(Draft.PendingSourceImagePath);
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.SaveSelectedProduct.ImageProcessing");
        }

        var product = new Product
        {
            Id = SelectedProduct.Id,
            Name = Draft.Name.Trim(),
            Barcode = Clean(Draft.Barcode),
            Sku = Clean(Draft.Sku),
            Unit = Clean(Draft.Unit),
            Price = price,
            CostPrice = costPrice,
            TaxCategoryId = Draft.TaxCategoryId > 0 ? Draft.TaxCategoryId : 1,
            TaxGroupId = ResolveTaxGroupId(),
            QuantityStore = storeQuantity,
            QuantityWarehouse = warehouseQuantity,
            MinThresholdStore = storeThreshold,
            MinThresholdWarehouse = warehouseThreshold,
            PurchasedAt = Draft.PurchasedAtDate.UtcDateTime,
            LastSaleAt = SelectedProduct.Product.LastSaleAt,
            ThumbnailPath = finalThumbnailPath
        };

        ProductsModule.Update(product);
        LoginRuntime.RaiseProductsUpdated();

        IsEditingSelectedProduct = false;
        ReloadProducts();
        SetStatus(LocalizationHelper.GetString("Products_Status_Updated"), InfoBarSeverity.Success);
        SearchTextBox.Focus(FocusState.Programmatic);
    }

    private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProduct is null || IsEditingSelectedProduct || IsAddingProduct) return;
        var id = SelectedProduct.Id;
        var thumbnailPath = SelectedProduct.Product.ThumbnailPath;
        ProductsModule.Delete(id);

        if (!string.IsNullOrEmpty(thumbnailPath))
        {
            new ProductImageService().TryCleanupThumbnail(thumbnailPath);
        }
        LoginRuntime.RaiseProductsUpdated();
        SetStatus(LocalizationHelper.GetString("Products_Status_Deleted"), InfoBarSeverity.Success);
        ReloadProducts();
    }

    private async Task SaveNewProduct()
    {
        TracePageState("SaveNewProduct:start");
        if (string.IsNullOrWhiteSpace(Draft.Name))
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_NameRequired"), InfoBarSeverity.Error);
            return;
        }

        if (!Draft.TryGetPrice(out var price) || price < 0)
        {
            SetStatus(LocalizationHelper.GetString("Products_Status_InvalidPrice"), InfoBarSeverity.Error);
            return;
        }

        Draft.TryGetCostPrice(out var costPrice);
        Draft.TryGetStoreQuantity(out var storeQty);
        Draft.TryGetWarehouseQuantity(out var warehouseQty);
        Draft.TryGetStoreThreshold(out var storeThreshold);
        Draft.TryGetWarehouseThreshold(out var warehouseThreshold);

        string? finalThumbnailPath = null;

        try
        {
            if (!string.IsNullOrEmpty(Draft.PendingSourceImagePath))
            {
                var imageService = new ProductImageService();
                finalThumbnailPath = await imageService.GenerateThumbnailAsync(Draft.PendingSourceImagePath);
            }
        }
        catch (Exception ex)
        {
            LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.SaveNewProduct.ImageProcessing");
        }

        var product = new Product
        {
            Name = Draft.Name.Trim(),
            Barcode = string.IsNullOrWhiteSpace(Draft.Barcode) ? null : Draft.Barcode.Trim(),
            Sku = string.IsNullOrWhiteSpace(Draft.Sku) ? null : Draft.Sku.Trim(),
            Unit = string.IsNullOrWhiteSpace(Draft.Unit) ? null : Draft.Unit.Trim(),
            Price = price,
            CostPrice = costPrice,
            TaxCategoryId = Draft.TaxCategoryId > 0 ? Draft.TaxCategoryId : 1,
            TaxGroupId = ResolveTaxGroupId(),
            QuantityStore = storeQty,
            QuantityWarehouse = warehouseQty,
            MinThresholdStore = storeThreshold > 0 ? storeThreshold : 5,
            MinThresholdWarehouse = warehouseThreshold > 0 ? warehouseThreshold : 10,
            PurchasedAt = DateTime.UtcNow,
            ThumbnailPath = finalThumbnailPath,
            CashierName = LoginRuntime.Auth.CurrentUser?.DisplayName
                          ?? LoginRuntime.Auth.CurrentUser?.Username
                          ?? "Cashier"
        };

        ProductsModule.Create(product);
        LoginRuntime.RaiseProductsUpdated();
        IsAddingProduct = false;
        Draft.Reset();
        ReloadProducts();

        // Select the newly created product
        var created = Products.FirstOrDefault(p => p.Id == product.Id);
        if (created is not null)
        {
            SelectedProduct = created;
            ProductsListView.SelectedItem = SelectedProduct;
        }

        SetStatus(LocalizationHelper.Format("Products_Status_AddedFormat", product.Name), InfoBarSeverity.Success);
        RaiseEditorStateProperties();
        TracePageState("SaveNewProduct:end");
    }

    private long ResolveTaxGroupId()
    {
        // 1 is the implicit fallback ID (often "No Tax" or default)
        long GetDefaultGroupId()
        {
            var def = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Profile && i.Profile?.IsDefault == true);
            return def?.Profile?.Id ?? 1;
        }

        if (SelectedTaxPickerItem is null) return GetDefaultGroupId();

        // User picked a Profile directly → use it as-is
        if (SelectedTaxPickerItem.Kind == TaxPickerKind.Profile)
            return SelectedTaxPickerItem.Profile!.Id;

        // User picked a single Rule → find or silently create an auto-managed profile
        if (SelectedTaxPickerItem.Kind == TaxPickerKind.Rule)
        {
            var rule = SelectedTaxPickerItem.Rule!;
            try
            {
                return TaxGroupsModule.GetOrCreateAutoManagedProfile(rule.Id, rule.Name);
            }
            catch (Exception ex)
            {
                LoginRuntime.ReportException(ex, "WinUiLogin.ProductsPage.ResolveTaxGroupId");
            }
        }

        return GetDefaultGroupId();
    }

    private void ResetTaxPickerToDefault()
    {
        SelectedTaxPickerItem = TaxPickerItems.FirstOrDefault(i => i.Kind == TaxPickerKind.Profile && i.Profile?.IsDefault == true);
    }

    private void RaiseCatalogProperties()
    {
        OnPropertyChanged(nameof(CatalogSummaryText));
        OnPropertyChanged(nameof(ProductsCountText));
        OnPropertyChanged(nameof(SearchStatusText));
        OnPropertyChanged(nameof(CatalogInventoryAlertText));
        OnPropertyChanged(nameof(HasCatalogInventoryAlerts));
        OnPropertyChanged(nameof(CatalogInventoryAlertSeverity));
        OnPropertyChanged(nameof(CatalogInventoryAlertVisibility));
        OnPropertyChanged(nameof(NoProductsVisibility));
    }

    private void RaiseSelectedProductProperties()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSelectionVisibility));
        OnPropertyChanged(nameof(NoSelectionVisibility));
        OnPropertyChanged(nameof(CanOverridePrice));
        OnPropertyChanged(nameof(PriceVisibility));
        OnPropertyChanged(nameof(ProductValueColumnSpan));
        OnPropertyChanged(nameof(SelectedProductNameText));
        OnPropertyChanged(nameof(SelectedProductSubtitleText));
        OnPropertyChanged(nameof(SelectedProductModeText));
        OnPropertyChanged(nameof(SelectedProductBarcodeText));
        OnPropertyChanged(nameof(SelectedProductSkuText));
        OnPropertyChanged(nameof(SelectedProductUnitText));
        OnPropertyChanged(nameof(SelectedProductPriceText));
        OnPropertyChanged(nameof(SelectedProductCostPriceText));
        OnPropertyChanged(nameof(SelectedProductTaxText));
        OnPropertyChanged(nameof(SelectedProductStoreQuantityText));
        OnPropertyChanged(nameof(SelectedProductWarehouseQuantityText));
        OnPropertyChanged(nameof(SelectedProductTotalQuantityText));
        OnPropertyChanged(nameof(SelectedProductStoreThresholdText));
        OnPropertyChanged(nameof(SelectedProductWarehouseThresholdText));
        OnPropertyChanged(nameof(SelectedProductPurchasedAtText));
        OnPropertyChanged(nameof(SelectedProductLastSaleAtText));
        OnPropertyChanged(nameof(SelectedProductQuantityText));
        OnPropertyChanged(nameof(SelectedProductInventoryAlertText));
        OnPropertyChanged(nameof(SelectedProductHasInventoryAlert));
        OnPropertyChanged(nameof(SelectedProductInventoryAlertSeverity));
        OnPropertyChanged(nameof(SelectedProductInventoryAlertVisibility));
        OnPropertyChanged(nameof(SelectedProductDraftTaxRateText));
        OnPropertyChanged(nameof(ProductImageContainerVisibility));
        OnPropertyChanged(nameof(ProductImageEditorVisibility));
        OnPropertyChanged(nameof(ProductImageActionText));
    }

    private void BeginImportOperation()
    {
        _importCancellationSource?.Dispose();
        _importCancellationSource = new CancellationTokenSource();
        _isImportCancelRequested = false;
        _importTotalRows = 0;
        _importProcessedRows = 0;
        _importCreatedCount = 0;
        _importUpdatedCount = 0;
        _importSkippedCount = 0;
        _importProgressPercent = 0;
        IsImportInProgress = true;
        RaiseImportProgressProperties();
    }

    private void UpdateImportProgress(ProductImportProgress progress)
    {
        _importTotalRows = progress.TotalRows;
        _importProcessedRows = progress.ProcessedRows;
        _importCreatedCount = progress.CreatedCount;
        _importUpdatedCount = progress.UpdatedCount;
        _importSkippedCount = progress.SkippedCount;
        _importProgressPercent = progress.ProgressPercent;
        RaiseImportProgressProperties();
    }

    private void RequestImportCancellation()
    {
        if (!IsImportInProgress || _isImportCancelRequested)
        {
            return;
        }

        _isImportCancelRequested = true;
        _importCancellationSource?.Cancel();
        RaiseImportStateProperties();
        RaiseImportProgressProperties();
    }

    private void EndImportOperation()
    {
        _importCancellationSource?.Dispose();
        _importCancellationSource = null;
        _isImportCancelRequested = false;
        _importTotalRows = 0;
        _importProcessedRows = 0;
        _importCreatedCount = 0;
        _importUpdatedCount = 0;
        _importSkippedCount = 0;
        _importProgressPercent = 0;
        IsImportInProgress = false;
        RaiseImportProgressProperties();
    }

    private void RaiseImportStateProperties()
    {
        OnPropertyChanged(nameof(ImportProgressVisibility));
        OnPropertyChanged(nameof(IsImportProgressIndeterminate));
        OnPropertyChanged(nameof(ImportProgressTitle));
        OnPropertyChanged(nameof(ImportProgressPercent));
        OnPropertyChanged(nameof(ImportProgressPercentText));
        OnPropertyChanged(nameof(CanCancelImport));
        OnPropertyChanged(nameof(ImportCancelButtonText));
        OnPropertyChanged(nameof(CanUseProductDetails));
    }

    private void RaiseImportProgressProperties()
    {
        OnPropertyChanged(nameof(ImportProgressTitle));
        OnPropertyChanged(nameof(ImportProgressDetail));
        OnPropertyChanged(nameof(ImportProgressPercent));
        OnPropertyChanged(nameof(ImportProgressPercentText));
        OnPropertyChanged(nameof(IsImportProgressIndeterminate));
        OnPropertyChanged(nameof(CanCancelImport));
    }

    private void ShowImportFailureTip(string errorMessage)
    {
        ImportFailureTipTitle = LocalizationHelper.GetString("Products_ImportFailureTip_Title");
        ImportFailureTipMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? LocalizationHelper.GetString("Products_ImportFailureTip_DefaultMessage")
            : LocalizationHelper.Format("Products_ImportFailureTip_Message", errorMessage);
        SetImportFailureTipOpen(true);
    }

    private void ShowImportWarningsTip(IEnumerable<string> warnings)
    {
        var messages = warnings
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Take(5)
            .ToList();

        if (messages.Count == 0)
        {
            return;
        }

        ImportFailureTipTitle = LocalizationHelper.GetString("Products_ImportWarningTip_Title");
        ImportFailureTipMessage = LocalizationHelper.GetString("Products_ImportWarningTip_Message")
            + Environment.NewLine
            + Environment.NewLine
            + string.Join(Environment.NewLine, messages);
        SetImportFailureTipOpen(true);
    }

    private void DismissImportFailureTip()
    {
        SetImportFailureTipOpen(false);
    }

    private void SetImportFailureTipOpen(bool isOpen)
    {
        if (_isImportFailureTipOpen == isOpen)
        {
            return;
        }

        _isImportFailureTipOpen = isOpen;
        OnPropertyChanged(nameof(ImportFailureTipVisibility));
    }

    private void RaiseEditorStateProperties()
    {
        OnPropertyChanged(nameof(IsEditingSelectedProduct));
        OnPropertyChanged(nameof(IsAddingProduct));
        OnPropertyChanged(nameof(CanBrowseProducts));
        OnPropertyChanged(nameof(CanOverridePrice));
        OnPropertyChanged(nameof(PriceVisibility));
        OnPropertyChanged(nameof(ProductValueColumnSpan));
        OnPropertyChanged(nameof(CanSaveSelectedProduct));
        OnPropertyChanged(nameof(CanSaveNewProduct));
        OnPropertyChanged(nameof(CanExecuteSelectedProductAction));
        OnPropertyChanged(nameof(SelectedProductActionText));
        OnPropertyChanged(nameof(SelectedProductActionSymbol));
        OnPropertyChanged(nameof(SelectedProductViewVisibility));
        OnPropertyChanged(nameof(SelectedProductEditVisibility));
        OnPropertyChanged(nameof(AddProductVisibility));
        OnPropertyChanged(nameof(ProductImageContainerVisibility));
        OnPropertyChanged(nameof(ProductImageEditorVisibility));
        OnPropertyChanged(nameof(ProductImageActionText));
        OnPropertyChanged(nameof(NoSelectionVisibility));
        OnPropertyChanged(nameof(DeleteButtonVisibility));
        OnPropertyChanged(nameof(RightPanelTitleText));
        OnPropertyChanged(nameof(SelectedProductCancelVisibility));
        OnPropertyChanged(nameof(SelectedProductSubtitleText));
        OnPropertyChanged(nameof(SelectedProductModeText));
        OnPropertyChanged(nameof(SelectedProductEditHintText));
        OnPropertyChanged(nameof(SelectedProductStoreQuantityText));
        OnPropertyChanged(nameof(SelectedProductWarehouseQuantityText));
        OnPropertyChanged(nameof(SelectedProductTotalQuantityText));
        OnPropertyChanged(nameof(SelectedProductStoreThresholdText));
        OnPropertyChanged(nameof(SelectedProductWarehouseThresholdText));
        OnPropertyChanged(nameof(SelectedProductPurchasedAtText));
        OnPropertyChanged(nameof(SelectedProductLastSaleAtText));
        OnPropertyChanged(nameof(SelectedProductQuantityText));
        OnPropertyChanged(nameof(SelectedProductInventoryAlertText));
        OnPropertyChanged(nameof(SelectedProductHasInventoryAlert));
        OnPropertyChanged(nameof(SelectedProductInventoryAlertSeverity));
        OnPropertyChanged(nameof(SelectedProductInventoryAlertVisibility));
        OnPropertyChanged(nameof(SelectedProductDraftTaxRateText));
    }

    private DispatcherTimer? _statusTimer;
    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusMessage = message;
        StatusSeverity = severity;

        _statusTimer?.Stop();
        if (!string.IsNullOrWhiteSpace(message) && severity != InfoBarSeverity.Error)
        {
            _statusTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (s, e) => 
            {
                StatusMessage = string.Empty;
                _statusTimer.Stop();
            };
            _statusTimer.Start();
        }
    }

    private bool HasPendingChanges()
    {
        if (SelectedProduct is null)
        {
            return false;
        }

        var product = SelectedProduct.Product;
        if (CanOverridePrice)
        {
            if (!Draft.TryGetPrice(out var draftPrice) || !Draft.TryGetCostPrice(out var draftCostPrice))
            {
                return true;
            }

            if (draftPrice != product.Price || draftCostPrice != product.CostPrice)
            {
                return true;
            }
        }

        return !string.Equals(Normalize(Draft.Name), Normalize(product.Name), StringComparison.Ordinal)
               || !string.Equals(Normalize(Draft.Barcode), Normalize(product.Barcode), StringComparison.Ordinal)
               || !string.Equals(Normalize(Draft.Sku), Normalize(product.Sku), StringComparison.Ordinal)
               || !string.Equals(Normalize(Draft.Unit), Normalize(product.Unit), StringComparison.Ordinal)
               || HasPendingImageChanges(product)
               || ResolveTaxGroupId() != (product.TaxGroupId ?? 1)
               || (Draft.TryGetStoreQuantity(out var storeQuantity) && storeQuantity != product.QuantityStore)
               || (Draft.TryGetWarehouseQuantity(out var warehouseQuantity) && warehouseQuantity != product.QuantityWarehouse)
               || (Draft.TryGetStoreThreshold(out var storeThreshold) && storeThreshold != product.MinThresholdStore)
               || (Draft.TryGetWarehouseThreshold(out var warehouseThreshold) && warehouseThreshold != product.MinThresholdWarehouse)
               || (!SameCalendarDay(Draft.PurchasedAtDate, product.PurchasedAt)
                   && !SameCalendarDay(Draft.PurchasedAtDate, Draft.InitialPurchasedAtDate));
    }

    private bool IsDraftValid()
    {
        if (CanOverridePrice)
        {
            return !string.IsNullOrWhiteSpace(Draft.Name)
                   && Draft.TryGetPrice(out var price)
                   && price >= 0
                   && Draft.TryGetCostPrice(out var costPrice)
                   && costPrice >= 0
                   && Draft.TryGetStoreQuantity(out var storeQuantity)
                   && storeQuantity >= 0
                   && Draft.TryGetWarehouseQuantity(out var warehouseQuantity)
                   && warehouseQuantity >= 0
                   && Draft.TryGetStoreThreshold(out var storeThreshold)
                   && storeThreshold >= 0
                   && Draft.TryGetWarehouseThreshold(out var warehouseThreshold)
                   && warehouseThreshold >= 0;
        }

        return !string.IsNullOrWhiteSpace(Draft.Name)
               && Draft.TryGetStoreQuantity(out var storeQty)
               && storeQty >= 0
               && Draft.TryGetWarehouseQuantity(out var warehouseQty)
               && warehouseQty >= 0
               && Draft.TryGetStoreThreshold(out var storeThresholdOnly)
               && storeThresholdOnly >= 0
                   && Draft.TryGetWarehouseThreshold(out var warehouseThresholdOnly)
                   && warehouseThresholdOnly >= 0;
    }

    private bool HasPendingImageChanges(Product product)
    {
        if (Draft.PendingRemoveImage)
        {
            return !string.IsNullOrWhiteSpace(product.ThumbnailPath);
        }

        if (!string.IsNullOrWhiteSpace(Draft.PendingSourceImagePath))
        {
            return true;
        }

        return false;
    }

    private string GetTaxCategoryName(long taxCategoryId)
    {
        return TaxCategories.FirstOrDefault(category => category.Id == taxCategoryId)?.Name
               ?? $"Category #{taxCategoryId:N0}";
    }

    private string GetTaxCategoryRateText(long taxCategoryId)
    {
        var rate = TaxCategories.FirstOrDefault(category => category.Id == taxCategoryId)?.RatePercent;
        return rate.HasValue
            ? $"{rate.Value:N2}%"
            : "—";
    }

    private static string FormatText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? ProductPriceFormatter.FormatDate(value.Value.ToLocalTime()) : "—";
    }

    private static string FormatDateTime(DateTime? value)
    {
        return value.HasValue ? ProductPriceFormatter.FormatDateTime(value.Value.ToLocalTime()) : "—";
    }

    private static string FormatNumberWithUnit(decimal value, string? unit)
    {
        var formattedValue = ProductPriceFormatter.FormatNumber(value);
        return string.IsNullOrWhiteSpace(unit)
            ? formattedValue
            : $"{formattedValue} {unit.Trim()}";
    }

    private static string BuildInventoryAlertText(Product product)
    {
        var messages = new List<string>();

        if (product.IsShelfOutOfStock)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_ShelfOut_Format", product.QuantityStore.ToString("N0"), product.MinThresholdStore.ToString("N0")));
        }
        else if (product.HasShelfLowAlert)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_ShelfLow_Format", product.QuantityStore.ToString("N0"), product.MinThresholdStore.ToString("N0")));
        }

        if (product.IsWarehouseOutOfStock)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_WarehouseOut_Format", product.QuantityWarehouse.ToString("N0"), product.MinThresholdWarehouse.ToString("N0")));
        }
        else if (product.HasWarehouseLowAlert)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_WarehouseLow_Format", product.QuantityWarehouse.ToString("N0"), product.MinThresholdWarehouse.ToString("N0")));
        }

        if (product.HasLegacyStockAlert)
        {
            var purchasedText = FormatDate(product.PurchasedAt);
            var lastSaleText = FormatDate(product.LastSaleAt);
            messages.Add(LocalizationHelper.Format("Products_Inventory_LegacyStock_Format", purchasedText, lastSaleText));
        }

        return string.Join(" ", messages);
    }

    private string BuildCatalogInventoryAlertText()
    {
        var shelfOut = Products.Count(product => product.Product.IsShelfOutOfStock);
        var warehouseOut = Products.Count(product => product.Product.IsWarehouseOutOfStock);
        var shelfLow = Products.Count(product => product.Product.HasShelfLowAlert);
        var warehouseLow = Products.Count(product => product.Product.HasWarehouseLowAlert);
        var legacy = Products.Count(product => product.Product.HasLegacyStockAlert);
        var messages = new List<string>();

        if (legacy > 0)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_LegacyNeedReview", legacy, ItemLabel(legacy)));
        }

        if (shelfOut > 0)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_ShelfOutOfStock", shelfOut, ItemLabel(shelfOut)));
        }

        if (warehouseOut > 0)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_WarehouseOutOfStock", warehouseOut, ItemLabel(warehouseOut)));
        }

        if (shelfLow > 0)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_ShelfBelowThreshold", shelfLow, ItemLabel(shelfLow)));
        }

        if (warehouseLow > 0)
        {
            messages.Add(LocalizationHelper.Format("Products_Inventory_WarehouseBelowThreshold", warehouseLow, ItemLabel(warehouseLow)));
        }

        return messages.Count == 0
            ? LocalizationHelper.GetString("Products_Inventory_HealthyAcrossCatalog")
            : string.Join(" ", messages);
    }

    private static string ItemLabel(int count)
    {
        return count == 1 
            ? LocalizationHelper.GetString("Products_Inventory_Product") 
            : LocalizationHelper.GetString("Products_Inventory_Products");
    }

    private static InfoBarSeverity GetInventoryAlertSeverity(Product product)
    {
        if (product.HasLegacyStockAlert || product.IsShelfOutOfStock || product.IsWarehouseOutOfStock)
        {
            return InfoBarSeverity.Error;
        }

        if (product.HasShelfLowAlert || product.HasWarehouseLowAlert)
        {
            return InfoBarSeverity.Warning;
        }

        return InfoBarSeverity.Success;
    }

    private static bool HasInventoryAlert(Product product)
    {
        return product.IsShelfOutOfStock || product.IsWarehouseOutOfStock || product.HasShelfLowAlert || product.HasWarehouseLowAlert || product.HasLegacyStockAlert;
    }

    private static bool SameCalendarDay(DateTimeOffset draftDate, DateTimeOffset productDate)
    {
        return draftDate.Date == productDate.Date;
    }

    private static bool SameCalendarDay(DateTimeOffset draftDate, DateTime? productDate)
    {
        if (!productDate.HasValue)
        {
            return false;
        }

        return draftDate.Date == productDate.Value.ToLocalTime().Date;
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private void TracePageState(string stage)
    {
        try
        {
            var selected = SelectedProduct is null
                ? "null"
                : $"{SelectedProduct.Id}:{SelectedProduct.Name}";

            var listSelected = ProductsListView.SelectedItem as ProductListItem;
            var listSelectedText = listSelected is null
                ? "null"
                : $"{listSelected.Id}:{listSelected.Name}";

            StartupTrace.Write(
                $"ProductsPage.{stage}: count={Products.Count}, productsNull={Products is null}, selectedNull={SelectedProduct is null}, listSelectedNull={ProductsListView.SelectedItem is null}, taxCategories={TaxCategories.Count}, taxCategoriesNull={TaxCategories is null}, selected={selected}, listSelected={listSelectedText}, adding={IsAddingProduct}, editing={IsEditingSelectedProduct}, canBrowse={CanBrowseProducts}, hasSelection={HasSelection}, canSaveNew={CanSaveNewProduct}, canSaveSelected={CanSaveSelectedProduct}, canExecuteAction={CanExecuteSelectedProductAction}, actionText='{SelectedProductActionText}', actionSymbol={SelectedProductActionSymbol}, rightPanel='{RightPanelTitleText}', status='{StatusMessage}', statusSeverity={StatusSeverity}, noProducts={NoProductsVisibility}, noSelection={NoSelectionVisibility}, viewVisibility={SelectedProductViewVisibility}, editVisibility={SelectedProductEditVisibility}, cancelVisibility={SelectedProductCancelVisibility}, search='{SearchTerm}', searchTextBoxNull={SearchTextBox is null}, productsListViewNull={ProductsListView is null}, draft={Draft.GetTraceSummary()}");
        }
        catch
        {
        }
    }

    private void Draft_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressDraftPropertyChanged)
        {
            return;
        }

        StartupTrace.Write($"ProductsPage.Draft.PropertyChanged:{e.PropertyName}: {Draft.GetTraceSummary()}");
        RaiseEditorStateProperties();
    }

    private void ResetDraftSilently()
    {
        try
        {
            _suppressDraftPropertyChanged = true;
            StartupTrace.Write($"ProductsPage.ResetDraftSilently:start: {Draft.GetTraceSummary()}");
            Draft.Reset();
            StartupTrace.Write($"ProductsPage.ResetDraftSilently:end: {Draft.GetTraceSummary()}");
        }
        catch (Exception ex)
        {
            StartupTrace.Write($"ProductsPage.ResetDraftSilently:exception:{ex}");
            throw;
        }
        finally
        {
            _suppressDraftPropertyChanged = false;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(nameof(CatalogSummaryText));
        return true;
    }
}

public sealed partial class ProductEditDraft : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string? _barcode;
    private string? _sku;
    private string? _unit;
    private string _priceText = string.Empty;
    private string _costPriceText = string.Empty;
    private string _quantityStoreText = string.Empty;
    private string _quantityWarehouseText = string.Empty;
    private string _minThresholdStoreText = "5";
    private string _minThresholdWarehouseText = "10";
    private decimal _priceValue;
    private decimal _costPriceValue;
    private decimal _quantityStoreValue;
    private decimal _quantityWarehouseValue;
    private decimal _minThresholdStoreValue;
    private decimal _minThresholdWarehouseValue;
    private bool _isPriceValid;
    private bool _isCostPriceValid;
    private bool _isQuantityStoreValid;
    private bool _isQuantityWarehouseValid;
    private bool _isMinThresholdStoreValid;
    private bool _isMinThresholdWarehouseValid;
    private DateTimeOffset _purchasedAtDate = DateTimeOffset.Now;
    private DateTimeOffset _initialPurchasedAtDate = DateTimeOffset.Now;
    private long _taxCategoryId = 1;
    private long _taxGroupId = 1;
    private string? _pendingSourceImagePath;
    private bool _pendingRemoveImage;
    private string? _savedThumbnailPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ProductEditDraft()
    {
        RefreshNumericState();
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string? Barcode
    {
        get => _barcode;
        set => SetProperty(ref _barcode, value);
    }

    public string? Sku
    {
        get => _sku;
        set => SetProperty(ref _sku, value);
    }

    public string? Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    public string PriceText
    {
        get => _priceText;
        set => SetNumericText(ref _priceText, value, ApplyPriceText);
    }

    public string CostPriceText
    {
        get => _costPriceText;
        set => SetNumericText(ref _costPriceText, value, ApplyCostPriceText);
    }

    public string QuantityStoreText
    {
        get => _quantityStoreText;
        set => SetNumericText(ref _quantityStoreText, value, ApplyStoreQuantityText);
    }

    public string QuantityWarehouseText
    {
        get => _quantityWarehouseText;
        set => SetNumericText(ref _quantityWarehouseText, value, ApplyWarehouseQuantityText);
    }

    public string MinThresholdStoreText
    {
        get => _minThresholdStoreText;
        set => SetNumericText(ref _minThresholdStoreText, value, ApplyStoreThresholdText);
    }

    public string MinThresholdWarehouseText
    {
        get => _minThresholdWarehouseText;
        set => SetNumericText(ref _minThresholdWarehouseText, value, ApplyWarehouseThresholdText);
    }

    public DateTimeOffset PurchasedAtDate
    {
        get => _purchasedAtDate;
        set => SetProperty(ref _purchasedAtDate, value);
    }
    
    public string? PendingSourceImagePath
    {
        get => _pendingSourceImagePath;
        set
        {
            if (SetProperty(ref _pendingSourceImagePath, value))
            {
                OnPropertyChanged(nameof(PreviewImageSource));
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(HasNoImage));
            }
        }
    }

    public bool PendingRemoveImage
    {
        get => _pendingRemoveImage;
        set
        {
            if (SetProperty(ref _pendingRemoveImage, value))
            {
                OnPropertyChanged(nameof(PreviewImageSource));
                OnPropertyChanged(nameof(HasImage));
                OnPropertyChanged(nameof(HasNoImage));
            }
        }
    }

    public Microsoft.UI.Xaml.Media.ImageSource? PreviewImageSource
    {
        get
        {
            if (PendingRemoveImage) return null;
            if (!string.IsNullOrEmpty(PendingSourceImagePath)) return CreateThumbnailImageSource(PendingSourceImagePath, 240);
            if (string.IsNullOrEmpty(_savedThumbnailPath)) return null;

            var service = new ProductImageService();
            return CreateThumbnailImageSource(service.ResolveThumbnailPath(_savedThumbnailPath), 240);
        }
    }

    private static Microsoft.UI.Xaml.Media.ImageSource? CreateThumbnailImageSource(string? path, int decodePixelWidth)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            var image = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage
            {
                DecodePixelWidth = Math.Max(1, decodePixelWidth)
            };
            image.UriSource = new Uri(path, UriKind.Absolute);
            return image;
        }
        catch { return null; }
    }

    public bool HasImage => PreviewImageSource != null;
    public bool HasNoImage => !HasImage;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public DateTimeOffset InitialPurchasedAtDate => _initialPurchasedAtDate;

    public long TaxCategoryId
    {
        get => _taxCategoryId;
        set => SetProperty(ref _taxCategoryId, value);
    }

    public long TaxGroupId
    {
        get => _taxGroupId;
        set => SetProperty(ref _taxGroupId, value);
    }

    public void LoadFrom(Product product)
    {
        TraceDraftState($"LoadFrom:start productId={product.Id} name='{product.Name}'");
        Name = product.Name;
        TraceDraftState("LoadFrom:after Name");
        Barcode = product.Barcode;
        TraceDraftState("LoadFrom:after Barcode");
        Sku = product.Sku;
        TraceDraftState("LoadFrom:after Sku");
        Unit = product.Unit;
        TraceDraftState("LoadFrom:after Unit");
        PriceText = product.Price.ToString(CultureInfo.InvariantCulture);
        TraceDraftState("LoadFrom:after PriceText");
        CostPriceText = product.CostPrice.ToString(CultureInfo.InvariantCulture);
        TraceDraftState("LoadFrom:after CostPriceText");
        QuantityStoreText = product.QuantityStore.ToString(CultureInfo.InvariantCulture);
        TraceDraftState("LoadFrom:after QuantityStoreText");
        QuantityWarehouseText = product.QuantityWarehouse.ToString(CultureInfo.InvariantCulture);
        TraceDraftState("LoadFrom:after QuantityWarehouseText");
        MinThresholdStoreText = product.MinThresholdStore.ToString(CultureInfo.InvariantCulture);
        TraceDraftState("LoadFrom:after MinThresholdStoreText");
        MinThresholdWarehouseText = product.MinThresholdWarehouse.ToString(CultureInfo.InvariantCulture);
        TraceDraftState("LoadFrom:after MinThresholdWarehouseText");
        PurchasedAtDate = new DateTimeOffset((product.PurchasedAt ?? DateTime.UtcNow).ToLocalTime());
        _initialPurchasedAtDate = PurchasedAtDate;
        TraceDraftState("LoadFrom:after PurchasedAtDate");
        TaxCategoryId = product.TaxCategoryId;
        TaxGroupId = product.TaxGroupId ?? 1;
        _savedThumbnailPath = product.ThumbnailPath;
        PendingSourceImagePath = null;
        PendingRemoveImage = false;
        OnPropertyChanged(nameof(PreviewImageSource));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(HasNoImage));
        TraceDraftState("LoadFrom:end");
    }

    public void Reset()
    {
        TraceDraftState("Reset:start");
        Name = string.Empty;
        TraceDraftState("Reset:after Name");
        Barcode = string.Empty;
        TraceDraftState("Reset:after Barcode");
        Sku = string.Empty;
        TraceDraftState("Reset:after Sku");
        Unit = string.Empty;
        TraceDraftState("Reset:after Unit");
        PriceText = string.Empty;
        TraceDraftState("Reset:after PriceText");
        CostPriceText = string.Empty;
        TraceDraftState("Reset:after CostPriceText");
        QuantityStoreText = "0";
        TraceDraftState("Reset:after QuantityStoreText");
        QuantityWarehouseText = "0";
        TraceDraftState("Reset:after QuantityWarehouseText");
        MinThresholdStoreText = "5";
        TraceDraftState("Reset:after MinThresholdStoreText");
        MinThresholdWarehouseText = "10";
        TraceDraftState("Reset:after MinThresholdWarehouseText");
        PurchasedAtDate = DateTimeOffset.Now;
        _initialPurchasedAtDate = PurchasedAtDate;
        TraceDraftState("Reset:after PurchasedAtDate");
        TaxCategoryId = 1;
        TaxGroupId = 1;
        _savedThumbnailPath = null;
        PendingSourceImagePath = null;
        PendingRemoveImage = false;
        OnPropertyChanged(nameof(PreviewImageSource));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(HasNoImage));
        TraceDraftState("Reset:end");
    }

    public bool TryGetStoreQuantity(out decimal quantity)
    {
        quantity = _quantityStoreValue;
        return _isQuantityStoreValid;
    }

    public bool TryGetWarehouseQuantity(out decimal quantity)
    {
        quantity = _quantityWarehouseValue;
        return _isQuantityWarehouseValid;
    }

    public bool TryGetStoreThreshold(out decimal threshold)
    {
        threshold = _minThresholdStoreValue;
        return _isMinThresholdStoreValid;
    }

    public bool TryGetWarehouseThreshold(out decimal threshold)
    {
        threshold = _minThresholdWarehouseValue;
        return _isMinThresholdWarehouseValid;
    }

    public bool TryGetPrice(out decimal price)
    {
        price = _priceValue;
        return _isPriceValid;
    }

    public string GetTraceSummary()
    {
        return $"name='{Name}', barcode='{Barcode}', sku='{Sku}', unit='{Unit}', price='{PriceText}' valid={_isPriceValid} parsed={_priceValue}, cost='{CostPriceText}' valid={_isCostPriceValid} parsed={_costPriceValue}, qtyStore='{QuantityStoreText}' valid={_isQuantityStoreValid} parsed={_quantityStoreValue}, qtyWarehouse='{QuantityWarehouseText}' valid={_isQuantityWarehouseValid} parsed={_quantityWarehouseValue}, minStore='{MinThresholdStoreText}' valid={_isMinThresholdStoreValid} parsed={_minThresholdStoreValue}, minWarehouse='{MinThresholdWarehouseText}' valid={_isMinThresholdWarehouseValid} parsed={_minThresholdWarehouseValue}, purchasedAt='{PurchasedAtDate:O}', initialPurchasedAt='{InitialPurchasedAtDate:O}', taxCategoryId={TaxCategoryId}";
    }

    private void TraceDraftState(string stage)
    {
        // Removed heavy synchronous disk I/O to improve search and selection performance
        // StartupTrace.Write($"ProductEditDraft.{stage}: {GetTraceSummary()}");
    }

    public bool TryGetCostPrice(out decimal price)
    {
        price = _costPriceValue;
        return _isCostPriceValid;
    }

    private void RefreshNumericState()
    {
        ApplyPriceText(_priceText);
        ApplyCostPriceText(_costPriceText);
        ApplyStoreQuantityText(_quantityStoreText);
        ApplyWarehouseQuantityText(_quantityWarehouseText);
        ApplyStoreThresholdText(_minThresholdStoreText);
        ApplyWarehouseThresholdText(_minThresholdWarehouseText);
    }

    private void ApplyPriceText(string? text)
    {
        ApplyParsedDecimal(text, ref _priceValue, ref _isPriceValid);
    }

    private void ApplyCostPriceText(string? text)
    {
        ApplyParsedDecimal(text, ref _costPriceValue, ref _isCostPriceValid);
    }

    private void ApplyStoreQuantityText(string? text)
    {
        ApplyParsedDecimal(text, ref _quantityStoreValue, ref _isQuantityStoreValid);
    }

    private void ApplyWarehouseQuantityText(string? text)
    {
        ApplyParsedDecimal(text, ref _quantityWarehouseValue, ref _isQuantityWarehouseValid);
    }

    private void ApplyStoreThresholdText(string? text)
    {
        ApplyParsedDecimal(text, ref _minThresholdStoreValue, ref _isMinThresholdStoreValid);
    }

    private void ApplyWarehouseThresholdText(string? text)
    {
        ApplyParsedDecimal(text, ref _minThresholdWarehouseValue, ref _isMinThresholdWarehouseValid);
    }

    private static void ApplyParsedDecimal(string? text, ref decimal parsedValue, ref bool isValid)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            parsedValue = 0m;
            isValid = false;
            return;
        }

        if (NumericInputNormalization.TryParseDecimal(text, out var value))
        {
            parsedValue = value;
            isValid = true;
            return;
        }

        parsedValue = 0m;
        isValid = false;
    }

    private void SetNumericText(ref string field, string? value, Action<string?> applyParsedValue, [CallerMemberName] string? propertyName = null)
    {
        var nextValue = value ?? string.Empty;
        var changed = !string.Equals(field, nextValue, StringComparison.Ordinal);
        field = nextValue;
        applyParsedValue(nextValue);

        if (changed)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
