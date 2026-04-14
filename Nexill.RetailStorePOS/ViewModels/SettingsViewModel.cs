using System.Collections.ObjectModel;
using System.Globalization;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const decimal MaxQuickCashAmount = 999999.99m;

    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private string _currencyCode = "USD";
    private RegionOption? _selectedRegionOption;
    private CurrencyOption? _selectedCurrencyOption;
    private bool _taxEnabled;
    private bool _isLoading;
    private bool _isSynchronizingCurrencySelections;
    private decimal _taxRatePercent;
    private string _statusMessage = string.Empty;

    // Local Preferences
    private bool _isDarkMode;
    private decimal _quickCash1 = 5m;
    private decimal _quickCash2 = 10m;
    private decimal _quickCash3 = 20m;

    // Navigation selections
    private bool _isStoreManagementSelected;
    private bool _isMyPreferencesSelected;
    private bool _isTaxConfigSelected;

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(SaveSettings, () => CanManageSettings);
        SaveStoreSettingsCommand = new RelayCommand(SaveSettings, () => CanManageSettings);
        ReloadCommand = new RelayCommand(LoadSettings);
        BuildRegionAndCurrencyOptions();

        LoadSettings();
        LoadTaxData();

        // Default selection based on whether the user can manage store settings
        ResetSectionSelection();

        LoginRuntime.Auth.LoginStateChanged += (_, _) =>
        {
            LoadSettings();
            LoadTaxData();
            ResetSectionSelection();
            OnPropertyChanged(nameof(CanManageSettings));
            SaveStoreSettingsCommand.RaiseCanExecuteChanged();
        };
    }

    private void ResetSectionSelection()
    {
        if (CanManageSettings)
        {
            IsStoreManagementSelected = true;
            IsTaxConfigSelected = false;
            IsMyPreferencesSelected = false;
        }
        else
        {
            IsStoreManagementSelected = false;
            IsTaxConfigSelected = false;
            IsMyPreferencesSelected = true;
        }
    }

    public bool CanManageSettings => LoginRuntime.Auth.CanManageSettings;

    public bool IsStoreManagementSelected
    {
        get => _isStoreManagementSelected;
        set => SetProperty(ref _isStoreManagementSelected, value);
    }

    public bool IsTaxConfigSelected
    {
        get => _isTaxConfigSelected;
        set => SetProperty(ref _isTaxConfigSelected, value);
    }

    public bool IsMyPreferencesSelected
    {
        get => _isMyPreferencesSelected;
        set => SetProperty(ref _isMyPreferencesSelected, value);
    }

    public ObservableCollection<TaxRule> TaxRules { get; } = new();
    public ObservableCollection<TaxGroup> TaxGroups { get; } = new();
    public ObservableCollection<TaxAuthority> TaxAuthorities { get; } = new();
    public ObservableCollection<RegionOption> RegionOptions { get; } = new();
    public ObservableCollection<CurrencyOption> CurrencyOptions { get; } = new();

    public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> RoundingStrategies { get; } = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
    {
        new("HALF_UP", "Standard (Half Up)"),
        new("HALF_EVEN", "Banker's (Half Even)"),
        new("ROUND_UP", "Always Round Up"),
        new("ROUND_DOWN", "Always Round Down")
    };

    public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> CashRoundingUnits { get; } = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
    {
        new("0.01", "Exact (1¢/penny)"),
        new("0.05", "Round to 5¢ (Swedish rounding)"),
        new("0.10", "Round to 10¢"),
        new("0.50", "Round to 50¢")
    };

    private string _selectedRoundingStrategy = "HALF_UP";
    public string SelectedRoundingStrategy
    {
        get => _selectedRoundingStrategy;
        set { if (SetProperty(ref _selectedRoundingStrategy, value)) SaveSettings(); }
    }

    private string _selectedCashRoundingUnit = "0.01";
    public string SelectedCashRoundingUnit
    {
        get => _selectedCashRoundingUnit;
        set { if (SetProperty(ref _selectedCashRoundingUnit, value)) SaveSettings(); }
    }

    public string StoreName
    {
        get => _storeName;
        set => SetProperty(ref _storeName, value);
    }

    public string StoreAddress
    {
        get => _storeAddress;
        set => SetProperty(ref _storeAddress, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                SaveLocalPreferences();
            }
        }
    }

    public decimal QuickCash1
    {
        get => _quickCash1;
        set => SetQuickCashAmount(ref _quickCash1, value, nameof(QuickCash1), nameof(QuickCash1Value));
    }

    public decimal QuickCash2
    {
        get => _quickCash2;
        set => SetQuickCashAmount(ref _quickCash2, value, nameof(QuickCash2), nameof(QuickCash2Value));
    }

    public decimal QuickCash3
    {
        get => _quickCash3;
        set => SetQuickCashAmount(ref _quickCash3, value, nameof(QuickCash3), nameof(QuickCash3Value));
    }

    public double QuickCash1Value
    {
        get => (double)_quickCash1;
        set => SetQuickCashAmount(ref _quickCash1, ConvertToQuickCashAmount(value), nameof(QuickCash1), nameof(QuickCash1Value));
    }

    public double QuickCash2Value
    {
        get => (double)_quickCash2;
        set => SetQuickCashAmount(ref _quickCash2, ConvertToQuickCashAmount(value), nameof(QuickCash2), nameof(QuickCash2Value));
    }

    public double QuickCash3Value
    {
        get => (double)_quickCash3;
        set => SetQuickCashAmount(ref _quickCash3, ConvertToQuickCashAmount(value), nameof(QuickCash3), nameof(QuickCash3Value));
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        set
        {
            var normalized = NormalizeCurrencyCode(value);
            if (SetProperty(ref _currencyCode, normalized))
            {
                SyncSelectedCurrencyFromCode(normalized);
                OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
                OnPropertyChanged(nameof(CurrencySelectionPreviewText));
            }
        }
    }

    public RegionOption? SelectedRegionOption
    {
        get => _selectedRegionOption;
        set
        {
            if (SetProperty(ref _selectedRegionOption, value))
            {
                if (!_isSynchronizingCurrencySelections && value is not null)
                {
                    ApplyRegionSelection(value);
                }

                OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
                OnPropertyChanged(nameof(CurrencySelectionPreviewText));
            }
        }
    }

    public CurrencyOption? SelectedCurrencyOption
    {
        get => _selectedCurrencyOption;
        set
        {
            if (SetProperty(ref _selectedCurrencyOption, value))
            {
                if (!_isSynchronizingCurrencySelections && value is not null)
                {
                    ApplyCurrencySelection(value);
                }

                OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
                OnPropertyChanged(nameof(CurrencySelectionPreviewText));
            }
        }
    }

    public string CurrencySelectionPreviewText
    {
        get
        {
            if (SelectedRegionOption is null && SelectedCurrencyOption is null)
            {
                return "Region and currency selection preview unavailable.";
            }

            if (SelectedRegionOption is null)
            {
                return $"Active store currency: {ActiveCurrencyCodeDisplay}.";
            }

            var regionText = SelectedRegionOption.DisplayName;
            var defaultCurrencyText = SelectedRegionOption.DefaultCurrencyDisplayName;

            if (SelectedCurrencyOption is null)
            {
                return $"Default currency for {regionText}: {defaultCurrencyText}. Active store currency: {ActiveCurrencyCodeDisplay}.";
            }

            return $"Default currency for {regionText}: {defaultCurrencyText}. Active store currency: {SelectedCurrencyOption.DisplayName}.";
        }
    }

    public string ActiveCurrencyCodeDisplay => SelectedCurrencyOption?.LocalizedCode ?? CurrencyCode;

    public bool TaxEnabled
    {
        get => _taxEnabled;
        set { if (SetProperty(ref _taxEnabled, value)) SaveSettings(); }
    }

    public decimal TaxRatePercent
    {
        get => _taxRatePercent;
        set => SetTaxRatePercent(value);
    }

    public double TaxRatePercentValue
    {
        get => (double)_taxRatePercent;
        set => SetTaxRatePercent(ConvertToTaxRatePercent(value));
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveStoreSettingsCommand { get; }
    public RelayCommand ReloadCommand { get; }

    private async void LoadSettings()
    {
        if (_isLoading) return;
        _isLoading = true;
        try
        {
            LoadStoreManagementSettingsCore();
            
            _selectedRoundingStrategy = LoginRuntime.Settings.GetTaxRoundingStrategy();
            _selectedCashRoundingUnit = LoginRuntime.Settings.GetCashRoundingUnit();
            OnPropertyChanged(nameof(SelectedRoundingStrategy));
            OnPropertyChanged(nameof(SelectedCashRoundingUnit));
            
            var prefs = await LoginRuntime.LocalPreferences.LoadPreferencesAsync();

            MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
            {
                _isDarkMode = prefs.IsDarkMode;
                if (prefs.QuickCashAmounts != null && prefs.QuickCashAmounts.Length >= 3)
                {
                    _quickCash1 = NormalizeQuickCashAmount(prefs.QuickCashAmounts[0]);
                    _quickCash2 = NormalizeQuickCashAmount(prefs.QuickCashAmounts[1]);
                    _quickCash3 = NormalizeQuickCashAmount(prefs.QuickCashAmounts[2]);
                }
                OnPropertyChanged(nameof(IsDarkMode));
                OnPropertyChanged(nameof(QuickCash1));
                OnPropertyChanged(nameof(QuickCash2));
                OnPropertyChanged(nameof(QuickCash3));
                OnPropertyChanged(nameof(QuickCash1Value));
                OnPropertyChanged(nameof(QuickCash2Value));
                OnPropertyChanged(nameof(QuickCash3Value));

                StatusMessage = "Settings loaded.";
            });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.DispatcherQueue.TryEnqueue(() => StatusMessage = "Error loading settings.");
            LoginRuntime.ReportException(ex, "SettingsViewModel.LoadSettings");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async void SaveLocalPreferences()
    {
        try
        {
            // Snapshot properties early on the UI thread before dropping to background 
            var isDark = IsDarkMode;
            var q1 = NormalizeQuickCashAmount(QuickCash1);
            var q2 = NormalizeQuickCashAmount(QuickCash2);
            var q3 = NormalizeQuickCashAmount(QuickCash3);

            var prefs = await LoginRuntime.LocalPreferences.LoadPreferencesAsync();
            prefs.IsDarkMode = isDark;
            prefs.QuickCashAmounts = new[] { q1, q2, q3 };

            await LoginRuntime.LocalPreferences.SavePreferencesAsync(prefs);

            MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessage = "Preferences save automatically.";
            });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.DispatcherQueue.TryEnqueue(() => StatusMessage = "Failed to save preferences.");
            LoginRuntime.ReportException(ex, "SettingsViewModel.SaveLocalPreferences");
        }
    }

    public void LoadTaxData()
    {
        try
        {
            TaxRules.Clear();
            foreach (var rule in LoginRuntime.TaxRules.GetAll())
            {
                TaxRules.Add(rule);
            }

            TaxGroups.Clear();
            foreach (var group in LoginRuntime.TaxGroups.GetAllWithRules())
            {
                TaxGroups.Add(group);
            }

            TaxAuthorities.Clear();
            foreach (var auth in LoginRuntime.TaxAuthorities.GetAll())
            {
                TaxAuthorities.Add(auth);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to load tax config.";
            LoginRuntime.ReportException(ex, "SettingsViewModel.LoadTaxData");
        }
    }

    private void SaveSettings()
    {
        if (_isLoading) return;

        if (!CanManageSettings)
        {
            LoginRuntime.Audit.Log("SECURITY_VIOLATION", "Unauthorized attempt to save store settings.", LoginRuntime.Auth.CurrentUser?.Id);
            StatusMessage = "Permission denied: store settings access required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrencyCode))
        {
            StatusMessage = "Currency code is required.";
            return;
        }

        if (TaxRatePercent < 0)
        {
            StatusMessage = "Tax rate must be zero or greater.";
            return;
        }

        try
        {
            LoginRuntime.Settings.SetStoreName(StoreName ?? string.Empty);
            LoginRuntime.Settings.SetStoreAddress(StoreAddress ?? string.Empty);
            LoginRuntime.Settings.SetRegionCode(SelectedRegionOption?.Code ?? RegionInfo.CurrentRegion.TwoLetterISORegionName);
            LoginRuntime.Settings.SetCurrencyCode(CurrencyCode);
            LoginRuntime.Settings.SetTaxSettings(new TaxSettings
            {
                Enabled = TaxEnabled,
                RatePercent = TaxRatePercent
            });

            StatusMessage = $"Settings saved successfully at {DateTime.Now:t}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to save settings.";
            LoginRuntime.ReportException(ex, "SettingsViewModel.SaveSettings");
        }
    }

    private void LoadStoreManagementSettingsCore()
    {
        StoreName = LoginRuntime.Settings.GetStoreName() ?? string.Empty;
        StoreAddress = LoginRuntime.Settings.GetStoreAddress() ?? string.Empty;

        var regionCode = NormalizeRegionCode(LoginRuntime.Settings.GetRegionCode());
        var currencyCode = NormalizeCurrencyCode(LoginRuntime.Settings.GetCurrencyCode());
        ApplyLoadedRegionAndCurrency(regionCode, currencyCode);

        var tax = LoginRuntime.Settings.GetTaxSettings();
        TaxEnabled = tax.Enabled;
        TaxRatePercent = tax.RatePercent;
    }

    private void SetQuickCashAmount(ref decimal field, decimal value, string amountPropertyName, string valuePropertyName)
    {
        var normalized = NormalizeQuickCashAmount(value);
        if (field == normalized)
        {
            return;
        }

        field = normalized;
        OnPropertyChanged(amountPropertyName);
        OnPropertyChanged(valuePropertyName);
        SaveLocalPreferences();
    }

    private void SetTaxRatePercent(decimal value)
    {
        var normalized = NormalizeTaxRatePercent(value);
        if (_taxRatePercent == normalized)
        {
            return;
        }

        _taxRatePercent = normalized;
        OnPropertyChanged(nameof(TaxRatePercent));
        OnPropertyChanged(nameof(TaxRatePercentValue));
    }

    private static decimal ConvertToQuickCashAmount(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
        {
            return 0m;
        }

        if (value > (double)MaxQuickCashAmount)
        {
            return MaxQuickCashAmount;
        }

        try
        {
            return (decimal)value;
        }
        catch (OverflowException)
        {
            return MaxQuickCashAmount;
        }
    }

    private static decimal ConvertToTaxRatePercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
        {
            return 0m;
        }

        if (value > (double)decimal.MaxValue)
        {
            return decimal.MaxValue;
        }

        try
        {
            return Math.Round((decimal)value, 2, MidpointRounding.AwayFromZero);
        }
        catch (OverflowException)
        {
            return decimal.MaxValue;
        }
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

    private static decimal NormalizeTaxRatePercent(decimal value)
    {
        if (value < 0m)
        {
            return 0m;
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private void BuildRegionAndCurrencyOptions()
    {
        if (RegionOptions.Count > 0 || CurrencyOptions.Count > 0)
        {
            return;
        }

        var regionsByCode = new Dictionary<string, RegionOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                var regionCode = NormalizeRegionCode(region.TwoLetterISORegionName);
                if (regionsByCode.ContainsKey(regionCode))
                {
                    continue;
                }

                regionsByCode[regionCode] = new RegionOption(
                    regionCode,
                    region.EnglishName,
                    region.NativeName,
                    NormalizeCurrencyCode(region.ISOCurrencySymbol),
                    region.CurrencySymbol,
                    region.CurrencyEnglishName,
                    region.CurrencyNativeName);
            }
            catch (ArgumentException)
            {
            }
        }

        foreach (var region in regionsByCode.Values.OrderBy(item => item.EnglishName, StringComparer.CurrentCultureIgnoreCase))
        {
            RegionOptions.Add(region);
        }

        var currenciesByCode = new Dictionary<string, CurrencyOption>(StringComparer.OrdinalIgnoreCase);
        foreach (var region in RegionOptions)
        {
            if (currenciesByCode.ContainsKey(region.DefaultCurrencyCode))
            {
                continue;
            }

            currenciesByCode[region.DefaultCurrencyCode] = new CurrencyOption(
                region.DefaultCurrencyCode,
                region.CurrencySymbol,
                region.CurrencyEnglishName,
                region.CurrencyNativeName);
        }

        foreach (var currency in currenciesByCode.Values.OrderBy(item => item.Code, StringComparer.OrdinalIgnoreCase))
        {
            CurrencyOptions.Add(currency);
        }
    }

    public void RestoreStoreManagementDraft()
    {
        var wasLoading = _isLoading;
        _isLoading = true;

        try
        {
            LoadStoreManagementSettingsCore();
            StatusMessage = "Store changes canceled. Restored last saved values.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to restore the last saved store values.";
            LoginRuntime.ReportException(ex, "SettingsViewModel.RestoreStoreManagementDraft");
        }
        finally
        {
            _isLoading = wasLoading;
        }
    }

    private void ApplyLoadedRegionAndCurrency(string? regionCode, string? currencyCode)
    {
        var fallbackRegionCode = NormalizeRegionCode(RegionInfo.CurrentRegion.TwoLetterISORegionName);
        var resolvedRegion = FindRegionOption(regionCode)
            ?? FindRegionOption(fallbackRegionCode)
            ?? RegionOptions.FirstOrDefault();

        var resolvedCurrencyCode = NormalizeCurrencyCode(currencyCode);
        if (string.IsNullOrWhiteSpace(resolvedCurrencyCode))
        {
            resolvedCurrencyCode = resolvedRegion?.DefaultCurrencyCode ?? NormalizeCurrencyCode(RegionInfo.CurrentRegion.ISOCurrencySymbol);
        }

        var resolvedCurrency = FindCurrencyOption(resolvedCurrencyCode)
            ?? (resolvedRegion is null ? null : FindCurrencyOption(resolvedRegion.DefaultCurrencyCode))
            ?? CurrencyOptions.FirstOrDefault();

        _isSynchronizingCurrencySelections = true;
        try
        {
            _selectedRegionOption = resolvedRegion;
            _selectedCurrencyOption = resolvedCurrency;
            _currencyCode = resolvedCurrency?.Code ?? resolvedCurrencyCode ?? "USD";
        }
        finally
        {
            _isSynchronizingCurrencySelections = false;
        }

        OnPropertyChanged(nameof(SelectedRegionOption));
        OnPropertyChanged(nameof(SelectedCurrencyOption));
        OnPropertyChanged(nameof(CurrencyCode));
        OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
        OnPropertyChanged(nameof(CurrencySelectionPreviewText));
    }

    private void ApplyRegionSelection(RegionOption region)
    {
        var matchingCurrency = FindCurrencyOption(region.DefaultCurrencyCode);

        _isSynchronizingCurrencySelections = true;
        try
        {
            _selectedCurrencyOption = matchingCurrency;
            _currencyCode = matchingCurrency?.Code ?? region.DefaultCurrencyCode;
        }
        finally
        {
            _isSynchronizingCurrencySelections = false;
        }

        OnPropertyChanged(nameof(SelectedCurrencyOption));
        OnPropertyChanged(nameof(CurrencyCode));
        OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
        OnPropertyChanged(nameof(CurrencySelectionPreviewText));
    }

    private void ApplyCurrencySelection(CurrencyOption currency)
    {
        _isSynchronizingCurrencySelections = true;
        try
        {
            _currencyCode = currency.Code;
        }
        finally
        {
            _isSynchronizingCurrencySelections = false;
        }

        OnPropertyChanged(nameof(CurrencyCode));
        OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
        OnPropertyChanged(nameof(CurrencySelectionPreviewText));
    }

    private void SyncSelectedCurrencyFromCode(string currencyCode)
    {
        if (_isSynchronizingCurrencySelections)
        {
            return;
        }

        var matchingCurrency = FindCurrencyOption(currencyCode);

        _isSynchronizingCurrencySelections = true;
        try
        {
            _selectedCurrencyOption = matchingCurrency;
        }
        finally
        {
            _isSynchronizingCurrencySelections = false;
        }

        OnPropertyChanged(nameof(SelectedCurrencyOption));
        OnPropertyChanged(nameof(ActiveCurrencyCodeDisplay));
        OnPropertyChanged(nameof(CurrencySelectionPreviewText));
    }

    private RegionOption? FindRegionOption(string? regionCode)
    {
        var normalized = NormalizeRegionCode(regionCode);
        return RegionOptions.FirstOrDefault(option => string.Equals(option.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private CurrencyOption? FindCurrencyOption(string? currencyCode)
    {
        var normalized = NormalizeCurrencyCode(currencyCode);
        return CurrencyOptions.FirstOrDefault(option => string.Equals(option.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCurrencyCode(string? currencyCode)
    {
        return string.IsNullOrWhiteSpace(currencyCode)
            ? string.Empty
            : currencyCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeRegionCode(string? regionCode)
    {
        return string.IsNullOrWhiteSpace(regionCode)
            ? string.Empty
            : regionCode.Trim().ToUpperInvariant();
    }

}

public sealed class RegionOption
{
    public RegionOption(string code, string englishName, string nativeName, string defaultCurrencyCode, string currencySymbol, string currencyEnglishName, string currencyNativeName)
    {
        Code = code;
        EnglishName = englishName;
        NativeName = nativeName;
        DefaultCurrencyCode = defaultCurrencyCode;
        CurrencySymbol = currencySymbol;
        CurrencyEnglishName = currencyEnglishName;
        CurrencyNativeName = currencyNativeName;
    }

    public string Code { get; }
    public string EnglishName { get; }
    public string NativeName { get; }
    public string DefaultCurrencyCode { get; }
    public string CurrencySymbol { get; }
    public string CurrencyEnglishName { get; }
    public string CurrencyNativeName { get; }
    public string DisplayName => string.Equals(EnglishName, NativeName, StringComparison.CurrentCulture)
        ? EnglishName
        : $"{EnglishName} ({NativeName})";

    public string DefaultCurrencyDisplayName => CurrencyOption.BuildDisplayName(DefaultCurrencyCode, CurrencySymbol, CurrencyEnglishName, CurrencyNativeName);
}

public sealed class CurrencyOption
{
    public CurrencyOption(string code, string symbol, string englishName, string nativeName)
    {
        Code = code;
        Symbol = symbol;
        EnglishName = englishName;
        NativeName = nativeName;
    }

    public string Code { get; }
    public string Symbol { get; }
    public string EnglishName { get; }
    public string NativeName { get; }
    public string LocalizedCode => ResolveLocalizedCode(Code, Symbol, NativeName);
    public string DisplayName => BuildDisplayName(Code, Symbol, EnglishName, NativeName);

    internal static string BuildDisplayName(string code, string symbol, string englishName, string nativeName)
    {
        var localizedCode = ResolveLocalizedCode(code, symbol, nativeName);
        if (string.IsNullOrWhiteSpace(nativeName))
        {
            return string.IsNullOrWhiteSpace(englishName)
                ? localizedCode
                : $"{localizedCode} - {englishName}";
        }

        return $"{localizedCode} - {nativeName}";
    }

    private static string ResolveLocalizedCode(string code, string symbol, string nativeName)
    {
        if (!string.IsNullOrWhiteSpace(symbol) && !string.Equals(symbol, code, StringComparison.OrdinalIgnoreCase))
        {
            return symbol.Trim();
        }

        if (!string.IsNullOrWhiteSpace(nativeName))
        {
            return nativeName.Trim();
        }

        return code;
    }
}


