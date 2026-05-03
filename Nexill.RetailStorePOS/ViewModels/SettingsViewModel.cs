using System.Collections.ObjectModel;
using System.Globalization;
using RetailStorePOS.Data.Modules.Tax;
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
    private bool _touchModeEnabled;
    private bool _taxEnabled;
    private bool _isLoading;
    private bool _isSynchronizingCurrencySelections;
    private decimal _taxRatePercent;
    private string _statusMessage = string.Empty;
    private bool _isRestartRequiredInfoBarOpen;
    private LanguageOption? _selectedAppLanguage;
    private LanguageOption? _selectedStoreLanguage;
    private PrinterOption? _selectedPrinterOption;

    // Local Preferences
    private bool _isDarkMode;
    private decimal _quickCash1 = 5m;
    private decimal _quickCash2 = 10m;
    private decimal _quickCash3 = 20m;
    private decimal _dailyTarget = 200000m;

    // Navigation selections
    private bool _isStoreManagementSelected;
    private bool _isMyPreferencesSelected;
    private bool _isTaxConfigSelected;

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(() => { SaveSettings(); }, () => CanManageSettings);
        SaveStoreSettingsCommand = new RelayCommand(() => { SaveSettings(); }, () => CanManageSettings);
        ReloadCommand = new AsyncRelayCommand(LoadSettings);
        BuildRegionAndCurrencyOptions();

        _ = LoadSettings();
        LoadTaxData();

        // Default selection based on whether the user can manage store settings
        ResetSectionSelection();

        LoginRuntime.Auth.LoginStateChanged += (_, _) =>
        {
            _ = LoadSettings();
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
    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new();
    public ObservableCollection<PrinterOption> PrinterOptions { get; } = new();

    public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> RoundingStrategies { get; } = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
    {
        new("HALF_UP", LocalizationHelper.GetString("Settings_Rounding_HalfUp")),
        new("HALF_EVEN", LocalizationHelper.GetString("Settings_Rounding_HalfEven")),
        new("ROUND_UP", LocalizationHelper.GetString("Settings_Rounding_RoundUp")),
        new("ROUND_DOWN", LocalizationHelper.GetString("Settings_Rounding_RoundDown"))
    };

    public System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<string, string>> CashRoundingUnits { get; } = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, string>>
    {
        new("0.01", "0.01"),
        new("0.05", "0.05"),
        new("0.10", "0.10"),
        new("0.50", "0.50")
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

    public bool IsTouchModeEnabled
    {
        get => _touchModeEnabled;
        set => SetProperty(ref _touchModeEnabled, value);
    }

    public bool IsDarkMode
    {
        get => _isDarkMode;
        set
        {
            if (SetProperty(ref _isDarkMode, value))
            {
                _ = SaveLocalPreferences();
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

    public decimal DailyTarget
    {
        get => _dailyTarget;
        set
        {
            var normalized = NormalizeQuickCashAmount(value);
            if (SetProperty(ref _dailyTarget, normalized))
            {
                OnPropertyChanged(nameof(DailyTargetValue));
                _ = SaveLocalPreferences();
            }
        }
    }

    public double DailyTargetValue
    {
        get => (double)_dailyTarget;
        set
        {
            var normalized = ConvertToQuickCashAmount(value);
            if (SetProperty(ref _dailyTarget, normalized, nameof(DailyTarget)))
            {
                _ = SaveLocalPreferences();
            }
        }
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
            }
        }
    }

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

    public LanguageOption? SelectedAppLanguage
    {
        get => _selectedAppLanguage;
        set
        {
            if (SetProperty(ref _selectedAppLanguage, value) && value is not null && !_isLoading)
            {
                var currentLang = LocalizationHelper.NormalizeLanguageTag(LoginRuntime.Settings.GetAppLanguage());
                var newLang = LocalizationHelper.NormalizeLanguageTag(value.Tag);
                IsRestartRequiredInfoBarOpen = !string.Equals(currentLang, newLang, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public LanguageOption? SelectedStoreLanguage
    {
        get => _selectedStoreLanguage;
        set => SetProperty(ref _selectedStoreLanguage, value);
    }

    public PrinterOption? SelectedPrinterOption
    {
        get => _selectedPrinterOption;
        set => SetProperty(ref _selectedPrinterOption, value);
    }

    public bool IsRestartRequiredInfoBarOpen
    {
        get => _isRestartRequiredInfoBarOpen;
        set
        {
            if (SetProperty(ref _isRestartRequiredInfoBarOpen, value))
            {
                OnPropertyChanged(nameof(HasPendingAppLanguageRestart));
            }
        }
    }

    public bool HasPendingAppLanguageRestart => _isRestartRequiredInfoBarOpen;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveStoreSettingsCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }

    public bool TrySaveStoreSettings()
    {
        return SaveSettings();
    }

    private async Task LoadSettings()
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
                _dailyTarget = prefs.DailyTarget;
                OnPropertyChanged(nameof(IsDarkMode));
                OnPropertyChanged(nameof(QuickCash1));
                OnPropertyChanged(nameof(QuickCash2));
                OnPropertyChanged(nameof(QuickCash3));
                OnPropertyChanged(nameof(QuickCash1Value));
                OnPropertyChanged(nameof(QuickCash2Value));
                OnPropertyChanged(nameof(QuickCash3Value));
                OnPropertyChanged(nameof(DailyTarget));
                OnPropertyChanged(nameof(DailyTargetValue));

                StatusMessage = LocalizationHelper.GetString("Settings_Status_Loaded");
            });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.DispatcherQueue.TryEnqueue(() => StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Load"));
            LoginRuntime.ReportException(ex, "SettingsViewModel.LoadSettings");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task SaveLocalPreferences()
    {
        try
        {
            // Snapshot properties early on the UI thread before dropping to background 
            var isDark = IsDarkMode;
            var q1 = NormalizeQuickCashAmount(QuickCash1);
            var q2 = NormalizeQuickCashAmount(QuickCash2);
            var q3 = NormalizeQuickCashAmount(QuickCash3);
            var dailyTarget = NormalizeQuickCashAmount(DailyTarget); // Using same normalize

            var prefs = await LoginRuntime.LocalPreferences.LoadPreferencesAsync();
            prefs.IsDarkMode = isDark;
            prefs.QuickCashAmounts = new[] { q1, q2, q3 };
            prefs.DailyTarget = dailyTarget;

            await LoginRuntime.LocalPreferences.SavePreferencesAsync(prefs);

            MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
            {
                StatusMessage = LocalizationHelper.GetString("Settings_Status_PrefsSaved");
            });
        }
        catch (Exception ex)
        {
            MainWindow.Current?.DispatcherQueue.TryEnqueue(() => StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_PrefsSave"));
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
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_TaxLoad");
            LoginRuntime.ReportException(ex, "SettingsViewModel.LoadTaxData");
        }
    }

    private bool SaveSettings()
    {
        if (_isLoading) return false;

        if (!CanManageSettings)
        {
            LoginRuntime.Audit.Log("SECURITY_VIOLATION", "Unauthorized attempt to save store settings.", LoginRuntime.Auth.CurrentUser?.Id);
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Permission");
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrencyCode))
        {
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_CurrencyRequired");
            return false;
        }

        if (TaxRatePercent < 0)
        {
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_TaxRateNegative");
            return false;
        }

        try
        {
            LoginRuntime.Settings.SetStoreName(StoreName ?? string.Empty);
            LoginRuntime.Settings.SetStoreAddress(StoreAddress ?? string.Empty);
            LoginRuntime.Settings.SetTouchModeEnabled(IsTouchModeEnabled);
            LoginRuntime.Settings.SetRegionCode(SelectedRegionOption?.Code ?? RegionInfo.CurrentRegion.TwoLetterISORegionName);
            LoginRuntime.Settings.SetCurrencyCode(CurrencyCode);
            LoginRuntime.Settings.SetPreferredPrinterName(SelectedPrinterOption?.PrinterName);
            LoginRuntime.Settings.SetTaxSettings(new TaxSettings
            {
                Enabled = TaxEnabled,
                RatePercent = TaxRatePercent
            });

            if (SelectedAppLanguage is not null)
                LoginRuntime.Settings.SetAppLanguage(LocalizationHelper.NormalizeLanguageTag(SelectedAppLanguage.Tag));

            if (SelectedStoreLanguage is not null)
                LoginRuntime.Settings.SetStoreLanguage(LocalizationHelper.NormalizeLanguageTag(SelectedStoreLanguage.Tag));

            IsRestartRequiredInfoBarOpen = false;
            StatusMessage = LocalizationHelper.Format("Settings_Status_Saved_Format", DateTime.Now.ToString("t"));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Save");
            LoginRuntime.ReportException(ex, "SettingsViewModel.SaveSettings");
            return false;
        }
    }

    private void LoadStoreManagementSettingsCore()
    {
        StoreName = LoginRuntime.Settings.GetStoreName() ?? string.Empty;
        StoreAddress = LoginRuntime.Settings.GetStoreAddress() ?? string.Empty;
        IsTouchModeEnabled = LoginRuntime.Settings.GetTouchModeEnabled();

        var regionCode = NormalizeRegionCode(LoginRuntime.Settings.GetRegionCode());
        var currencyCode = NormalizeCurrencyCode(LoginRuntime.Settings.GetCurrencyCode());
        ApplyLoadedRegionAndCurrency(regionCode, currencyCode);
        LoadPrinterOptions(LoginRuntime.Settings.GetPreferredPrinterName());

        var tax = LoginRuntime.Settings.GetTaxSettings();
        TaxEnabled = tax.Enabled;
        TaxRatePercent = tax.RatePercent;

        var appLangTag = LocalizationHelper.NormalizeLanguageTag(LoginRuntime.Settings.GetAppLanguage());
        _selectedAppLanguage = LanguageOptions.FirstOrDefault(l => l.Tag == appLangTag) ?? LanguageOptions.FirstOrDefault(l => l.Tag == LocalizationHelper.DefaultLanguage);
        OnPropertyChanged(nameof(SelectedAppLanguage));
        
        var storeLangTag = LocalizationHelper.NormalizeLanguageTag(LoginRuntime.Settings.GetStoreLanguage());
        _selectedStoreLanguage = LanguageOptions.FirstOrDefault(l => l.Tag == storeLangTag) ?? LanguageOptions.FirstOrDefault(l => l.Tag == LocalizationHelper.DefaultLanguage);
        OnPropertyChanged(nameof(SelectedStoreLanguage));

        IsRestartRequiredInfoBarOpen = false;
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
        _ = SaveLocalPreferences();
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
        if (LanguageOptions.Count == 0)
        {
            foreach (var lang in LocalizationHelper.SupportedLanguages)
            {
                if (string.Equals(lang.Tag, "fr-FR", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                LanguageOptions.Add(new LanguageOption(lang.Tag, lang.NativeName, lang.EnglishName, lang.IsRtl));
            }
        }

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
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Restored");
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Restore");
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
    }

    private void LoadPrinterOptions(string? selectedPrinterName)
    {
        var normalizedSelectedPrinterName = NormalizePrinterName(selectedPrinterName);
        var installedPrinters = PrinterSelectionHelper.GetInstalledPrinters();

        PrinterOptions.Clear();
        PrinterOptions.Add(new PrinterOption(null, LocalizationHelper.GetString("StoreManagement_Printer_DefaultOption")));

        foreach (var printerName in installedPrinters)
        {
            PrinterOptions.Add(new PrinterOption(printerName, printerName));
        }

        SelectedPrinterOption = PrinterOptions.FirstOrDefault(option => string.Equals(option.PrinterName, normalizedSelectedPrinterName, StringComparison.OrdinalIgnoreCase))
            ?? PrinterOptions.FirstOrDefault();
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


    private static string? NormalizePrinterName(string? printerName)
    {
        return string.IsNullOrWhiteSpace(printerName)
            ? null
            : printerName.Trim();
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
    public string DisplayName => EnglishName;
    public override string ToString() => DisplayName;

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
    public string DisplayName => string.IsNullOrWhiteSpace(EnglishName) ? Code : $"{Code} - {EnglishName}";
    public override string ToString() => DisplayName;

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

public sealed class LanguageOption
{
    public LanguageOption(string tag, string nativeName, string englishName, bool isRtl)
    {
        Tag = tag;
        NativeName = nativeName;
        EnglishName = englishName;
        IsRtl = isRtl;
    }

    public string Tag { get; }
    public string NativeName { get; }
    public string EnglishName { get; }
    public bool IsRtl { get; }
    public string DisplayName => $"{NativeName} ({EnglishName})";
    public override string ToString() => DisplayName;
}

public sealed class PrinterOption
{
    public PrinterOption(string? printerName, string displayName)
    {
        PrinterName = string.IsNullOrWhiteSpace(printerName) ? null : printerName.Trim();
        DisplayName = displayName;
    }

    public string? PrinterName { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}


