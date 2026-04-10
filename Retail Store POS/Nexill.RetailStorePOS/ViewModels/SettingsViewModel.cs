using System.Collections.ObjectModel;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private const decimal MaxQuickCashAmount = 999999.99m;

    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private string _currencyCode = "USD";
    private bool _taxEnabled;
    private bool _isLoading;
    private decimal _taxRatePercent;
    private string _statusMessage = string.Empty;

    // Local Preferences
    private bool _isDarkMode;
    private decimal _quickCash1 = 5m;
    private decimal _quickCash2 = 10m;
    private decimal _quickCash3 = 20m;

    // Security locks for Store Details
    private bool _isStoreNameUnlocked;
    private bool _isStoreAddressUnlocked;

    // Navigation selections
    private bool _isStoreManagementSelected;
    private bool _isMyPreferencesSelected;
    private bool _isTaxConfigSelected;

    public SettingsViewModel()
    {
        SaveCommand = new RelayCommand(SaveSettings, () => CanManageSettings);
        SaveStoreSettingsCommand = new RelayCommand(SaveSettings, () => CanManageSettings);
        ReloadCommand = new RelayCommand(LoadSettings);

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

    public bool IsStoreNameUnlocked
    {
        get => _isStoreNameUnlocked;
        set
        {
            if (SetProperty(ref _isStoreNameUnlocked, value) && !value)
            {
                SaveSettings(); // Auto-save when re-locked
            }
        }
    }

    public bool IsStoreAddressUnlocked
    {
        get => _isStoreAddressUnlocked;
        set
        {
            if (SetProperty(ref _isStoreAddressUnlocked, value) && !value)
            {
                SaveSettings(); // Auto-save when re-locked
            }
        }
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        set { if (SetProperty(ref _currencyCode, value)) SaveSettings(); }
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
            StoreName = LoginRuntime.Settings.GetStoreName() ?? string.Empty;
            StoreAddress = LoginRuntime.Settings.GetStoreAddress() ?? string.Empty;
            CurrencyCode = LoginRuntime.Settings.GetCurrencyCode() ?? "USD";
            var tax = LoginRuntime.Settings.GetTaxSettings();
            TaxEnabled = tax.Enabled;
            TaxRatePercent = tax.RatePercent;
            
            _selectedRoundingStrategy = LoginRuntime.Settings.GetTaxRoundingStrategy();
            _selectedCashRoundingUnit = LoginRuntime.Settings.GetCashRoundingUnit();
            OnPropertyChanged(nameof(SelectedRoundingStrategy));
            OnPropertyChanged(nameof(SelectedCashRoundingUnit));
            
            IsStoreNameUnlocked = false;
            IsStoreAddressUnlocked = false;

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
            LoginRuntime.Settings.SetCurrencyCode(CurrencyCode);
            LoginRuntime.Settings.SetTaxSettings(new TaxSettings
            {
                Enabled = TaxEnabled,
                RatePercent = TaxRatePercent
            });

            IsStoreNameUnlocked = false;
            IsStoreAddressUnlocked = false;

            StatusMessage = $"Settings saved successfully at {DateTime.Now:t}";
        }
        catch (Exception ex)
        {
            StatusMessage = "Failed to save settings.";
            LoginRuntime.ReportException(ex, "SettingsViewModel.SaveSettings");
        }
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

}


