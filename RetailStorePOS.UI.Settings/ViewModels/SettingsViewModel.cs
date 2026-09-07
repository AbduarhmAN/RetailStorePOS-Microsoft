using System.Collections.ObjectModel;
using System.Globalization;
using System.ComponentModel;
using System.Threading;
using Microsoft.UI.Dispatching;
using RetailStorePOS.Data.Modules.Tax;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Services.Printing;

namespace RetailStorePOS.UI.Settings.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const decimal MaxQuickCashAmount = 999999.99m;
    private const int MinimumSessionIdleTimeoutMinutes = 1;
    private const int MaximumSessionIdleTimeoutMinutes = 480;

    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    public LocalizationService Loc => LocalizationService.Instance;
    private string _currencyCode = "USD";
    private RegionOption? _selectedRegionOption;
    private CurrencyOption? _selectedCurrencyOption;
    private bool _touchModeEnabled;
    private bool _taxEnabled;
    private bool _isLoading;
    private bool _isTaxDataLoading;
    private bool _isSynchronizingCurrencySelections;
    private bool _hasLoadedSettingsData;
    private bool _hasLoadedTaxData;
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
    private int _sessionIdleTimeoutMinutes = 30;

    // Navigation selections
    private bool _isStoreManagementSelected;
    private bool _isMyPreferencesSelected;
    private bool _isTaxConfigSelected;
    private readonly DispatcherQueue _uiDispatcher;
    private readonly EventHandler _loginStateChangedHandler;
    private readonly PropertyChangedEventHandler _localizationChangedHandler;
    private bool _isDisposed;
    private int _settingsLoadVersion;
    private int _taxLoadVersion;
    private int _restoreLoadVersion;

    public SettingsViewModel()
    {
        _uiDispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("SettingsViewModel requires a UI dispatcher.");
        _loginStateChangedHandler = OnLoginStateChanged;
        _localizationChangedHandler = OnLocalizationChanged;
        SaveCommand = new RelayCommand(() => { SaveSettings(); }, () => CanManageSettings);
        SaveStoreSettingsCommand = new RelayCommand(() => { SaveSettings(); }, () => CanManageSettings);
        ReloadCommand = new AsyncRelayCommand(ReloadSettingsAsync);
        BuildRegionAndCurrencyOptions();

        // Default selection based on whether the user can manage store settings
        ResetSectionSelection();

        LoginRuntime.Auth.LoginStateChanged += _loginStateChangedHandler;
        Loc.PropertyChanged += _localizationChangedHandler;
    }

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(MyPreferences_Header_Title));
        OnPropertyChanged(nameof(MyPreferences_Header_Subtitle));
        OnPropertyChanged(nameof(MyPreferences_QuickCash_Title));
        OnPropertyChanged(nameof(MyPreferences_QuickCash_Subtitle));
        OnPropertyChanged(nameof(MyPreferences_QuickCash_Value1));
        OnPropertyChanged(nameof(MyPreferences_QuickCash_Value2));
        OnPropertyChanged(nameof(MyPreferences_QuickCash_Value3));
        OnPropertyChanged(nameof(MyPreferences_SessionSecurity_Title));
        OnPropertyChanged(nameof(MyPreferences_SessionSecurity_Subtitle));
        OnPropertyChanged(nameof(MyPreferences_SessionTimeout_Label));
        OnPropertyChanged(nameof(MyPreferences_SessionTimeout_Helper));
        
        OnPropertyChanged(nameof(StoreManagement_Header_Title));
        OnPropertyChanged(nameof(StoreManagement_Header_Subtitle));
        OnPropertyChanged(nameof(StoreManagement_SaveButton));
        OnPropertyChanged(nameof(StoreManagement_StoreIdentity_Title));
        OnPropertyChanged(nameof(StoreManagement_StoreIdentity_Subtitle));
        OnPropertyChanged(nameof(StoreManagement_StoreName_Label));
        OnPropertyChanged(nameof(StoreManagement_StoreAddress_Label));
        OnPropertyChanged(nameof(StoreManagement_CheckoutDefaults_Title));
        OnPropertyChanged(nameof(StoreManagement_Region_Label));
        OnPropertyChanged(nameof(StoreManagement_Currency_Label));
        OnPropertyChanged(nameof(StoreManagement_PrintingSettings_Title));
        OnPropertyChanged(nameof(StoreManagement_PrintingSettings_Subtitle));
        OnPropertyChanged(nameof(StoreManagement_Printer_Label));
        OnPropertyChanged(nameof(StoreManagement_LanguageRegion_Title));
        OnPropertyChanged(nameof(StoreManagement_AppLanguage_Label));
        OnPropertyChanged(nameof(StoreManagement_DashboardSettings_Title));
        OnPropertyChanged(nameof(StoreManagement_DashboardSettings_Subtitle));
        OnPropertyChanged(nameof(StoreManagement_DailyTarget_Label));

        OnPropertyChanged(nameof(TaxConfiguration_Header_Title));
        OnPropertyChanged(nameof(TaxConfiguration_Header_Subtitle));
        OnPropertyChanged(nameof(TaxConfiguration_SaveButton));
        OnPropertyChanged(nameof(TaxConfiguration_TaxEngine_Title));
        OnPropertyChanged(nameof(TaxConfiguration_TaxEngine_Subtitle));
        OnPropertyChanged(nameof(TaxConfiguration_EnableTax_Label));
        OnPropertyChanged(nameof(TaxConfiguration_DefaultRate_Label));
        OnPropertyChanged(nameof(TaxConfiguration_Rounding_Title));
        OnPropertyChanged(nameof(TaxConfiguration_Rounding_Subtitle));
        OnPropertyChanged(nameof(TaxConfiguration_RoundingStrategy_Label));
        OnPropertyChanged(nameof(TaxConfiguration_RoundingStrategy_Tip));
        OnPropertyChanged(nameof(TaxConfiguration_CashRounding_Label));
        OnPropertyChanged(nameof(TaxConfiguration_CashRounding_Tip));
        OnPropertyChanged(nameof(TaxConfiguration_Authorities_Title));
        OnPropertyChanged(nameof(TaxConfiguration_Authorities_Subtitle));
        OnPropertyChanged(nameof(TaxConfiguration_AddAuthority_Button));
        OnPropertyChanged(nameof(TaxConfiguration_EditAuthority_Button));
        OnPropertyChanged(nameof(TaxConfiguration_DeleteAuthority_Button));
        OnPropertyChanged(nameof(TaxConfiguration_Rules_Title));
        OnPropertyChanged(nameof(TaxConfiguration_Rules_Subtitle));
        OnPropertyChanged(nameof(TaxConfiguration_AddRule_Button));
        OnPropertyChanged(nameof(TaxConfiguration_EditRule_Button));
        OnPropertyChanged(nameof(TaxConfiguration_DeleteRule_Button));
        OnPropertyChanged(nameof(TaxConfiguration_Groups_Title));
        OnPropertyChanged(nameof(TaxConfiguration_Groups_Subtitle));
        OnPropertyChanged(nameof(TaxConfiguration_AddGroup_Button));
        OnPropertyChanged(nameof(TaxConfiguration_DefaultBadge));
        OnPropertyChanged(nameof(TaxConfiguration_EditGroup_Button));
        OnPropertyChanged(nameof(TaxConfiguration_DeleteGroup_Button));

        OnPropertyChanged(nameof(RoundingStrategies));
        OnPropertyChanged(nameof(StatusMessage));

        OnPropertyChanged(nameof(SettingsPage_Nav_Checkout));
        OnPropertyChanged(nameof(SettingsPage_Nav_Products));
        OnPropertyChanged(nameof(SettingsPage_Nav_Reports));
        OnPropertyChanged(nameof(SettingsPage_Nav_Settings));
        OnPropertyChanged(nameof(SettingsPage_Nav_Store));
        OnPropertyChanged(nameof(SettingsPage_Nav_Tax));
        OnPropertyChanged(nameof(SettingsPage_Nav_Users));
        OnPropertyChanged(nameof(SettingsPage_Nav_Prefs));
        OnPropertyChanged(nameof(SettingsPage_Nav_About));
        OnPropertyChanged(nameof(SettingsPage_Nav_SignOut));
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Interlocked.Increment(ref _settingsLoadVersion);
        Interlocked.Increment(ref _taxLoadVersion);
        Interlocked.Increment(ref _restoreLoadVersion);
        LoginRuntime.Auth.LoginStateChanged -= _loginStateChangedHandler;
        Loc.PropertyChanged -= _localizationChangedHandler;
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

    public string SettingsPage_Nav_Checkout => Loc["SettingsPage_Nav_Checkout.Content"];
    public string SettingsPage_Nav_Products => Loc["SettingsPage_Nav_Products.Content"];
    public string SettingsPage_Nav_Reports => Loc["SettingsPage_Nav_Reports.Content"];
    public string SettingsPage_Nav_Settings => Loc["SettingsPage_Nav_Settings.Content"];
    public string SettingsPage_Nav_Users => Loc["SettingsPage_Nav_Users.Content"];
    public string SettingsPage_Nav_Store => Loc["SettingsPage_Nav_Store.Content"];
    public string SettingsPage_Nav_Tax => Loc["SettingsPage_Nav_Tax.Content"];
    public string SettingsPage_Nav_Prefs => Loc["SettingsPage_Nav_Prefs.Content"];
    public string SettingsPage_Nav_About => Loc["SettingsPage_Nav_About.Content"];
    public string SettingsPage_Nav_SignOut => Loc["SettingsPage_Nav_SignOut.Content"];

    public string StoreManagement_FreshStart_Title => Loc["StoreManagement_FreshStart_Title.Text"];
    public string StoreManagement_FreshStart_Subtitle => Loc["StoreManagement_FreshStart_Subtitle.Text"];
    public string StoreManagement_ResetButton => Loc["StoreManagement_ResetButton.Content"];

    private System.Collections.Generic.IReadOnlyList<ComboBoxOption>? _roundingStrategies;
    public System.Collections.Generic.IReadOnlyList<ComboBoxOption> RoundingStrategies =>
        _roundingStrategies ??= new System.Collections.Generic.List<ComboBoxOption>
        {
            new("HALF_UP", LocalizationHelper.GetString("Settings_Rounding_HalfUp")),
            new("HALF_EVEN", LocalizationHelper.GetString("Settings_Rounding_HalfEven")),
            new("ROUND_UP", LocalizationHelper.GetString("Settings_Rounding_RoundUp")),
            new("ROUND_DOWN", LocalizationHelper.GetString("Settings_Rounding_RoundDown"))
        };

    public System.Collections.Generic.IReadOnlyList<ComboBoxOption> CashRoundingUnits { get; } = new System.Collections.Generic.List<ComboBoxOption>
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

    public int SessionIdleTimeoutMinutes
    {
        get => _sessionIdleTimeoutMinutes;
        set
        {
            var normalized = NormalizeSessionIdleTimeoutMinutes(value);
            if (SetProperty(ref _sessionIdleTimeoutMinutes, normalized))
            {
                OnPropertyChanged(nameof(SessionIdleTimeoutMinutesValue));
                _ = SaveSessionSecurityPreferencesAsync(normalized);
            }
        }
    }

    public double SessionIdleTimeoutMinutesValue
    {
        get => _sessionIdleTimeoutMinutes;
        set
        {
            var normalized = NormalizeSessionIdleTimeoutMinutes(ConvertToSessionIdleTimeoutMinutes(value));
            if (SetProperty(ref _sessionIdleTimeoutMinutes, normalized, nameof(SessionIdleTimeoutMinutes)))
            {
                OnPropertyChanged(nameof(SessionIdleTimeoutMinutesValue));
                _ = SaveSessionSecurityPreferencesAsync(normalized);
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

    // Reactive Localization Properties for MyPreferences
    public string MyPreferences_Header_Title => GetWithLog(nameof(MyPreferences_Header_Title), "MyPreferences_Header_Title.Text");
    public string MyPreferences_Header_Subtitle => GetWithLog(nameof(MyPreferences_Header_Subtitle), "MyPreferences_Header_Subtitle.Text");
    public string MyPreferences_QuickCash_Title => GetWithLog(nameof(MyPreferences_QuickCash_Title), "MyPreferences_QuickCash_Title.Text");
    public string MyPreferences_QuickCash_Subtitle => GetWithLog(nameof(MyPreferences_QuickCash_Subtitle), "MyPreferences_QuickCash_Subtitle.Text");
    public string MyPreferences_QuickCash_Value1 => GetWithLog(nameof(MyPreferences_QuickCash_Value1), "MyPreferences_QuickCash_Value1.Header");
    public string MyPreferences_QuickCash_Value2 => GetWithLog(nameof(MyPreferences_QuickCash_Value2), "MyPreferences_QuickCash_Value2.Header");
    public string MyPreferences_QuickCash_Value3 => GetWithLog(nameof(MyPreferences_QuickCash_Value3), "MyPreferences_QuickCash_Value3.Header");
    public string MyPreferences_SessionSecurity_Title => GetWithLog(nameof(MyPreferences_SessionSecurity_Title), "MyPreferences_SessionSecurity_Title.Text");
    public string MyPreferences_SessionSecurity_Subtitle => GetWithLog(nameof(MyPreferences_SessionSecurity_Subtitle), "MyPreferences_SessionSecurity_Subtitle.Text");
    public string MyPreferences_SessionTimeout_Label => GetWithLog(nameof(MyPreferences_SessionTimeout_Label), "MyPreferences_SessionTimeout_Label.Text");
    public string MyPreferences_SessionTimeout_Helper => GetWithLog(nameof(MyPreferences_SessionTimeout_Helper), "MyPreferences_SessionTimeout_Helper.Text");

    // Reactive Localization Properties for StoreManagement
    public string StoreManagement_Header_Title => GetWithLog(nameof(StoreManagement_Header_Title), "StoreManagement_Header_Title.Text");
    public string StoreManagement_Header_Subtitle => GetWithLog(nameof(StoreManagement_Header_Subtitle), "StoreManagement_Header_Subtitle.Text");
    public string StoreManagement_SaveButton => GetWithLog(nameof(StoreManagement_SaveButton), "StoreManagement_SaveButton.Content");
    public string StoreManagement_StoreIdentity_Title => GetWithLog(nameof(StoreManagement_StoreIdentity_Title), "StoreManagement_StoreIdentity_Title.Text");
    public string StoreManagement_StoreIdentity_Subtitle => GetWithLog(nameof(StoreManagement_StoreIdentity_Subtitle), "StoreManagement_StoreIdentity_Subtitle.Text");
    public string StoreManagement_StoreName_Label => GetWithLog(nameof(StoreManagement_StoreName_Label), "StoreManagement_StoreName_Label.Text");
    public string StoreManagement_StoreAddress_Label => GetWithLog(nameof(StoreManagement_StoreAddress_Label), "StoreManagement_StoreAddress_Label.Text");
    public string StoreManagement_CheckoutDefaults_Title => GetWithLog(nameof(StoreManagement_CheckoutDefaults_Title), "StoreManagement_CheckoutDefaults_Title.Text");
    public string StoreManagement_Region_Label => GetWithLog(nameof(StoreManagement_Region_Label), "StoreManagement_Region_Label.Text");
    public string StoreManagement_Currency_Label => GetWithLog(nameof(StoreManagement_Currency_Label), "StoreManagement_Currency_Label.Text");
    public string StoreManagement_PrintingSettings_Title => GetWithLog(nameof(StoreManagement_PrintingSettings_Title), "StoreManagement_PrintingSettings_Title.Text");
    public string StoreManagement_PrintingSettings_Subtitle => GetWithLog(nameof(StoreManagement_PrintingSettings_Subtitle), "StoreManagement_PrintingSettings_Subtitle.Text");
    public string StoreManagement_Printer_Label => GetWithLog(nameof(StoreManagement_Printer_Label), "StoreManagement_Printer_Label.Text");
    public string StoreManagement_LanguageRegion_Title => GetWithLog(nameof(StoreManagement_LanguageRegion_Title), "StoreManagement_LanguageRegion_Title.Text");
    public string StoreManagement_AppLanguage_Label => GetWithLog(nameof(StoreManagement_AppLanguage_Label), "StoreManagement_AppLanguage_Label.Text");
    public string StoreManagement_DashboardSettings_Title => GetWithLog(nameof(StoreManagement_DashboardSettings_Title), "StoreManagement_DashboardSettings_Title.Text");
    public string StoreManagement_DashboardSettings_Subtitle => GetWithLog(nameof(StoreManagement_DashboardSettings_Subtitle), "StoreManagement_DashboardSettings_Subtitle.Text");
    public string StoreManagement_DailyTarget_Label => GetWithLog(nameof(StoreManagement_DailyTarget_Label), "StoreManagement_DailyTarget_Label.Text");

    // Reactive Localization Properties for TaxConfiguration
    public string TaxConfiguration_Header_Title => GetWithLog(nameof(TaxConfiguration_Header_Title), "TaxConfiguration_Header_Title.Text");
    public string TaxConfiguration_Header_Subtitle => GetWithLog(nameof(TaxConfiguration_Header_Subtitle), "TaxConfiguration_Header_Subtitle.Text");
    public string TaxConfiguration_SaveButton => GetWithLog(nameof(TaxConfiguration_SaveButton), "TaxConfiguration_SaveButton.Content");
    public string TaxConfiguration_TaxEngine_Title => GetWithLog(nameof(TaxConfiguration_TaxEngine_Title), "TaxConfiguration_TaxEngine_Title.Text");
    public string TaxConfiguration_TaxEngine_Subtitle => GetWithLog(nameof(TaxConfiguration_TaxEngine_Subtitle), "TaxConfiguration_TaxEngine_Subtitle.Text");
    public string TaxConfiguration_EnableTax_Label => GetWithLog(nameof(TaxConfiguration_EnableTax_Label), "TaxConfiguration_EnableTax_Label.Content");
    public string TaxConfiguration_DefaultRate_Label => GetWithLog(nameof(TaxConfiguration_DefaultRate_Label), "TaxConfiguration_DefaultRate_Label.Text");
    public string TaxConfiguration_Rounding_Title => GetWithLog(nameof(TaxConfiguration_Rounding_Title), "TaxConfiguration_Rounding_Title.Text");
    public string TaxConfiguration_Rounding_Subtitle => GetWithLog(nameof(TaxConfiguration_Rounding_Subtitle), "TaxConfiguration_Rounding_Subtitle.Text");
    public string TaxConfiguration_RoundingStrategy_Label => GetWithLog(nameof(TaxConfiguration_RoundingStrategy_Label), "TaxConfiguration_RoundingStrategy_Label.Text");
    public string TaxConfiguration_RoundingStrategy_Tip => GetWithLog(nameof(TaxConfiguration_RoundingStrategy_Tip), "TaxConfiguration_RoundingStrategy_Tip.Text");
    public string TaxConfiguration_CashRounding_Label => GetWithLog(nameof(TaxConfiguration_CashRounding_Label), "TaxConfiguration_CashRounding_Label.Text");
    public string TaxConfiguration_CashRounding_Tip => GetWithLog(nameof(TaxConfiguration_CashRounding_Tip), "TaxConfiguration_CashRounding_Tip.Text");
    public string TaxConfiguration_Authorities_Title => GetWithLog(nameof(TaxConfiguration_Authorities_Title), "TaxConfiguration_Authorities_Title.Text");
    public string TaxConfiguration_Authorities_Subtitle => GetWithLog(nameof(TaxConfiguration_Authorities_Subtitle), "TaxConfiguration_Authorities_Subtitle.Text");
    public string TaxConfiguration_AddAuthority_Button => GetWithLog(nameof(TaxConfiguration_AddAuthority_Button), "TaxConfiguration_AddAuthority_Button.Content");
    public string TaxConfiguration_EditAuthority_Button => GetWithLog(nameof(TaxConfiguration_EditAuthority_Button), "TaxConfiguration_EditAuthority_Button.Content");
    public string TaxConfiguration_DeleteAuthority_Button => GetWithLog(nameof(TaxConfiguration_DeleteAuthority_Button), "TaxConfiguration_DeleteAuthority_Button.Content");
    public string TaxConfiguration_Rules_Title => GetWithLog(nameof(TaxConfiguration_Rules_Title), "TaxConfiguration_Rules_Title.Text");
    public string TaxConfiguration_Rules_Subtitle => GetWithLog(nameof(TaxConfiguration_Rules_Subtitle), "TaxConfiguration_Rules_Subtitle.Text");
    public string TaxConfiguration_AddRule_Button => GetWithLog(nameof(TaxConfiguration_AddRule_Button), "TaxConfiguration_AddRule_Button.Content");
    public string TaxConfiguration_EditRule_Button => GetWithLog(nameof(TaxConfiguration_EditRule_Button), "TaxConfiguration_EditRule_Button.Content");
    public string TaxConfiguration_DeleteRule_Button => GetWithLog(nameof(TaxConfiguration_DeleteRule_Button), "TaxConfiguration_DeleteRule_Button.Content");
    public string TaxConfiguration_Groups_Title => GetWithLog(nameof(TaxConfiguration_Groups_Title), "TaxConfiguration_Groups_Title.Text");
    public string TaxConfiguration_Groups_Subtitle => GetWithLog(nameof(TaxConfiguration_Groups_Subtitle), "TaxConfiguration_Groups_Subtitle.Text");
    public string TaxConfiguration_AddGroup_Button => GetWithLog(nameof(TaxConfiguration_AddGroup_Button), "TaxConfiguration_AddGroup_Button.Content");
    public string TaxConfiguration_DefaultBadge => GetWithLog(nameof(TaxConfiguration_DefaultBadge), "TaxConfiguration_DefaultBadge.Text");
    public string TaxConfiguration_EditGroup_Button => GetWithLog(nameof(TaxConfiguration_EditGroup_Button), "TaxConfiguration_EditGroup_Button.Content");
    public string TaxConfiguration_DeleteGroup_Button => GetWithLog(nameof(TaxConfiguration_DeleteGroup_Button), "TaxConfiguration_DeleteGroup_Button.Content");

    private string GetWithLog(string propName, string key)
    {
        _ = propName;
        return Loc[key];
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
                OnPropertyChanged(nameof(StatusVisibility));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(_statusMessage);

    public Microsoft.UI.Xaml.Visibility StatusVisibility =>
        HasStatusMessage ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveStoreSettingsCommand { get; }
    public AsyncRelayCommand ReloadCommand { get; }

    public bool TrySaveStoreSettings()
    {
        return SaveSettings();
    }

    public void EnsureDataForSection(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag) || _isDisposed)
        {
            return;
        }

        switch (tag.Trim().ToLowerInvariant())
        {
            case "store":
            case "prefs":
                EnsureSettingsLoaded();
                break;
            case "tax":
                EnsureSettingsLoaded();
                EnsureTaxDataLoaded();
                break;
        }
    }

    private void EnsureSettingsLoaded()
    {
        if (_hasLoadedSettingsData || _isDisposed)
        {
            return;
        }

        _ = LoadSettingsAsync();
    }

    private void EnsureTaxDataLoaded()
    {
        if (_hasLoadedTaxData || _isDisposed)
        {
            return;
        }

        _ = LoadTaxDataAsync();
    }

    private Task ReloadSettingsAsync()
        => LoadSettingsAsync(forceReload: true);

    private async Task LoadSettingsAsync(bool forceReload = false)
    {
        if (_isDisposed || _isLoading || (!forceReload && _hasLoadedSettingsData))
        {
            return;
        }

        var operationVersion = Interlocked.Increment(ref _settingsLoadVersion);
        _isLoading = true;
        try
        {
            var snapshotTask = Task.Run(BuildStoreManagementSettingsSnapshot);
            var prefsTask = LoginRuntime.LocalPreferences.LoadPreferencesAsync();

            var snapshot = await snapshotTask;
            var prefs = await prefsTask;

            if (IsSettingsLoadStale(operationVersion))
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                if (IsSettingsLoadStale(operationVersion))
                {
                    return;
                }

                ApplyStoreManagementSettingsSnapshot(snapshot);

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
                _hasLoadedSettingsData = true;
            });
        }
        catch (Exception ex)
        {
            if (!IsSettingsLoadStale(operationVersion))
            {
                await RunOnUiThreadAsync(() =>
                {
                    if (!IsSettingsLoadStale(operationVersion))
                    {
                        StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Load");
                    }
                });
            }

            LoginRuntime.ReportException(ex, "SettingsViewModel.LoadSettings");
        }
        finally
        {
            if (!IsSettingsLoadStale(operationVersion))
            {
                await RunOnUiThreadAsync(() => _isLoading = false);
            }
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

            await RunOnUiThreadAsync(() =>
            {
                StatusMessage = LocalizationHelper.GetString("Settings_Status_PrefsSaved");
            });
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_PrefsSave"));
            LoginRuntime.ReportException(ex, "SettingsViewModel.SaveLocalPreferences");
        }
    }

    private async Task SaveSessionSecurityPreferencesAsync(int sessionIdleTimeoutMinutes)
    {
        try
        {
            await Task.Run(() => LoginRuntime.Settings.SetSessionIdleTimeoutMinutes(sessionIdleTimeoutMinutes));

            await RunOnUiThreadAsync(() =>
            {
                StatusMessage = LocalizationHelper.GetString("Settings_Status_PrefsSaved");
            });
        }
        catch (Exception ex)
        {
            await RunOnUiThreadAsync(() => StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_PrefsSave"));
            LoginRuntime.ReportException(ex, "SettingsViewModel.SaveSessionSecurityPreferences");
        }
    }

    public void LoadTaxData()
        => _ = LoadTaxDataAsync(forceReload: true);

    private async Task LoadTaxDataAsync(bool forceReload = false)
    {
        if (_isDisposed || _isTaxDataLoading || (!forceReload && _hasLoadedTaxData))
        {
            return;
        }

        var operationVersion = Interlocked.Increment(ref _taxLoadVersion);
        _isTaxDataLoading = true;
        try
        {
            var snapshot = await Task.Run(() => new TaxDataSnapshot(
                LoginRuntime.TaxRules.GetAll().ToList(),
                LoginRuntime.TaxGroups.GetAllWithRules().ToList(),
                LoginRuntime.TaxAuthorities.GetAll().ToList()));

            if (IsTaxLoadStale(operationVersion))
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                if (IsTaxLoadStale(operationVersion))
                {
                    return;
                }

                TaxRules.Clear();
                foreach (var rule in snapshot.Rules)
                {
                    TaxRules.Add(rule);
                }

                TaxGroups.Clear();
                foreach (var group in snapshot.Groups)
                {
                    TaxGroups.Add(group);
                }

                TaxAuthorities.Clear();
                foreach (var auth in snapshot.Authorities)
                {
                    TaxAuthorities.Add(auth);
                }

                _hasLoadedTaxData = true;
            });
        }
        catch (Exception ex)
        {
            if (!IsTaxLoadStale(operationVersion))
            {
                await RunOnUiThreadAsync(() =>
                {
                    if (!IsTaxLoadStale(operationVersion))
                    {
                        StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_TaxLoad");
                    }
                });
            }

            LoginRuntime.ReportException(ex, "SettingsViewModel.LoadTaxData");
        }
        finally
        {
            if (!IsTaxLoadStale(operationVersion))
            {
                _isTaxDataLoading = false;
            }
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

            CurrencyDisplayHelper.InvalidateConfiguredState();
            _hasLoadedSettingsData = true;
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

    private static int ConvertToSessionIdleTimeoutMinutes(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 30;
        }

        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
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

    private static int NormalizeSessionIdleTimeoutMinutes(int value)
    {
        return Math.Clamp(value, MinimumSessionIdleTimeoutMinutes, MaximumSessionIdleTimeoutMinutes);
    }

    private void BuildRegionAndCurrencyOptions()
    {
        if (LanguageOptions.Count == 0)
        {
            foreach (var lang in LocalizationHelper.SupportedLanguages)
            {
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
        => _ = RestoreStoreManagementDraftAsync();

    private async Task RestoreStoreManagementDraftAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        var operationVersion = Interlocked.Increment(ref _restoreLoadVersion);
        var wasLoading = _isLoading;
        _isLoading = true;

        try
        {
            var snapshot = await Task.Run(BuildStoreManagementSettingsSnapshot);

            if (IsRestoreLoadStale(operationVersion))
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                if (IsRestoreLoadStale(operationVersion))
                {
                    return;
                }

                ApplyStoreManagementSettingsSnapshot(snapshot);
                StatusMessage = LocalizationHelper.GetString("Settings_Status_Restored");
            });
        }
        catch (Exception ex)
        {
            if (!IsRestoreLoadStale(operationVersion))
            {
                await RunOnUiThreadAsync(() =>
                {
                    if (!IsRestoreLoadStale(operationVersion))
                    {
                        StatusMessage = LocalizationHelper.GetString("Settings_Status_Error_Restore");
                    }
                });
            }

            LoginRuntime.ReportException(ex, "SettingsViewModel.RestoreStoreManagementDraft");
        }
        finally
        {
            if (!IsRestoreLoadStale(operationVersion))
            {
                await RunOnUiThreadAsync(() => _isLoading = wasLoading);
            }
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

    private StoreManagementSettingsSnapshot BuildStoreManagementSettingsSnapshot()
    {
        var regionCode = NormalizeRegionCode(LoginRuntime.Settings.GetRegionCode());
        var currencyCode = NormalizeCurrencyCode(LoginRuntime.Settings.GetCurrencyCode());
        var installedPrinters = PrinterSelectionHelper.GetInstalledPrinters().ToList();
        var tax = LoginRuntime.Settings.GetTaxSettings();

        return new StoreManagementSettingsSnapshot(
            LoginRuntime.Settings.GetStoreName() ?? string.Empty,
            LoginRuntime.Settings.GetStoreAddress() ?? string.Empty,
            LoginRuntime.Settings.GetTouchModeEnabled(),
            regionCode,
            currencyCode,
            NormalizePrinterName(LoginRuntime.Settings.GetPreferredPrinterName()),
            installedPrinters,
            tax.Enabled,
            tax.RatePercent,
            LocalizationHelper.NormalizeLanguageTag(LoginRuntime.Settings.GetAppLanguage()),
            LocalizationHelper.NormalizeLanguageTag(LoginRuntime.Settings.GetStoreLanguage()),
            LoginRuntime.Settings.GetTaxRoundingStrategy(),
            LoginRuntime.Settings.GetCashRoundingUnit(),
            LoginRuntime.Settings.GetSessionIdleTimeoutMinutes());
    }

    private void ApplyStoreManagementSettingsSnapshot(StoreManagementSettingsSnapshot snapshot)
    {
        StoreName = snapshot.StoreName;
        StoreAddress = snapshot.StoreAddress;
        IsTouchModeEnabled = snapshot.IsTouchModeEnabled;
        ApplyLoadedRegionAndCurrency(snapshot.RegionCode, snapshot.CurrencyCode);
        ApplyPrinterOptions(snapshot.InstalledPrinters, snapshot.SelectedPrinterName);

        TaxEnabled = snapshot.TaxEnabled;
        TaxRatePercent = snapshot.TaxRatePercent;

        _selectedRoundingStrategy = snapshot.RoundingStrategy;
        _selectedCashRoundingUnit = snapshot.CashRoundingUnit;
        OnPropertyChanged(nameof(SelectedRoundingStrategy));
        OnPropertyChanged(nameof(SelectedCashRoundingUnit));
        _sessionIdleTimeoutMinutes = snapshot.SessionIdleTimeoutMinutes;
        OnPropertyChanged(nameof(SessionIdleTimeoutMinutes));
        OnPropertyChanged(nameof(SessionIdleTimeoutMinutesValue));

        _selectedAppLanguage = LanguageOptions.FirstOrDefault(l => l.Tag == snapshot.AppLanguageTag)
            ?? LanguageOptions.FirstOrDefault(l => l.Tag == LocalizationHelper.DefaultLanguage);
        OnPropertyChanged(nameof(SelectedAppLanguage));

        _selectedStoreLanguage = LanguageOptions.FirstOrDefault(l => l.Tag == snapshot.StoreLanguageTag)
            ?? LanguageOptions.FirstOrDefault(l => l.Tag == LocalizationHelper.DefaultLanguage);
        OnPropertyChanged(nameof(SelectedStoreLanguage));

        IsRestartRequiredInfoBarOpen = false;
    }

    private void ApplyPrinterOptions(IReadOnlyList<string> installedPrinters, string? selectedPrinterName)
    {
        var normalizedSelectedPrinterName = NormalizePrinterName(selectedPrinterName);

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

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        var shouldReloadSettings = _hasLoadedSettingsData;
        var shouldReloadTax = _hasLoadedTaxData;

        _hasLoadedSettingsData = false;
        _hasLoadedTaxData = false;
        CurrencyDisplayHelper.InvalidateConfiguredState();

        if (shouldReloadSettings)
        {
            _ = LoadSettingsAsync(forceReload: true);
        }

        if (shouldReloadTax)
        {
            _ = LoadTaxDataAsync(forceReload: true);
        }

        ResetSectionSelection();
        OnPropertyChanged(nameof(CanManageSettings));
        SaveStoreSettingsCommand.RaiseCanExecuteChanged();
        RefreshLocalizedProperties();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshLocalizedProperties();
    }

    private bool IsSettingsLoadStale(int operationVersion)
        => _isDisposed || operationVersion != Volatile.Read(ref _settingsLoadVersion);

    private bool IsTaxLoadStale(int operationVersion)
        => _isDisposed || operationVersion != Volatile.Read(ref _taxLoadVersion);

    private bool IsRestoreLoadStale(int operationVersion)
        => _isDisposed || operationVersion != Volatile.Read(ref _restoreLoadVersion);

    private Task RunOnUiThreadAsync(Action action)
    {
        if (_uiDispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_uiDispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetCanceled();
        }

        return tcs.Task;
    }

    private sealed record StoreManagementSettingsSnapshot(
        string StoreName,
        string StoreAddress,
        bool IsTouchModeEnabled,
        string RegionCode,
        string CurrencyCode,
        string? SelectedPrinterName,
        IReadOnlyList<string> InstalledPrinters,
        bool TaxEnabled,
        decimal TaxRatePercent,
        string AppLanguageTag,
        string StoreLanguageTag,
        string RoundingStrategy,
        string CashRoundingUnit,
        int SessionIdleTimeoutMinutes);

    private sealed record TaxDataSnapshot(
        IReadOnlyList<TaxRule> Rules,
        IReadOnlyList<TaxGroup> Groups,
        IReadOnlyList<TaxAuthority> Authorities);

}

public sealed partial class RegionOption
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

public sealed partial class CurrencyOption
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

public sealed partial class LanguageOption
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

public sealed partial class PrinterOption
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
public sealed partial class ComboBoxOption
{
    public ComboBoxOption(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; }
    public string Value { get; }
    public override string ToString() => Value;
}
