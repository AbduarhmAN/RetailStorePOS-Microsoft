using System.Collections.ObjectModel;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App;

public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsRepository _settingsRepository;
    private readonly TaxCategoryRepository _taxCategoryRepository;
    private readonly ILocalPreferencesService _localPreferences;
    private readonly AuthService _authService;
    private readonly AuditLogService _audit;

    private string _storeName = string.Empty;
    private string _storeAddress = string.Empty;
    private string _currencyCode = "USD";
    private bool _taxEnabled;
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

    // Tax Category Editor fields
    private TaxCategory? _selectedTaxCategory;
    private string _editCategoryName = string.Empty;
    private decimal _editCategoryRate;

    public SettingsViewModel(
        SettingsRepository settingsRepository,
        TaxCategoryRepository taxCategoryRepository,
        ILocalPreferencesService localPreferences,
        AuthService authService,
        AuditLogService auditLogService)
    {
        _settingsRepository = settingsRepository;
        _taxCategoryRepository = taxCategoryRepository;
        _localPreferences = localPreferences;
        _authService = authService;
        _audit = auditLogService;

        SaveCommand = new RelayCommand(SaveSettings);
        SaveStoreSettingsCommand = new RelayCommand(SaveSettings, () => IsAdmin);
        ReloadCommand = new RelayCommand(LoadSettings);

        // Tax Category Commands
        NewCategoryCommand = new RelayCommand(NewCategory);
        SaveCategoryCommand = new RelayCommand(SaveCategory, () => IsAdmin);
        DeleteCategoryCommand = new RelayCommand(DeleteCategory, () => IsAdmin);

        LoadSettings();
        LoadTaxCategories();

        // Default selection based on IsAdmin
        ResetSectionSelection();

        _authService.LoginStateChanged += (_, _) =>
        {
            LoadSettings();
            LoadTaxCategories();
            ResetSectionSelection();
            OnPropertyChanged(nameof(IsAdmin));
            SaveStoreSettingsCommand.RaiseCanExecuteChanged();
            SaveCategoryCommand.RaiseCanExecuteChanged();
            DeleteCategoryCommand.RaiseCanExecuteChanged();
        };
    }

    private void ResetSectionSelection()
    {
        if (IsAdmin)
        {
            IsStoreManagementSelected = true;
            IsMyPreferencesSelected = false;
        }
        else
        {
            IsStoreManagementSelected = false;
            IsMyPreferencesSelected = true;
        }
    }

    public bool IsAdmin => _authService.CurrentUser?.IsAdmin ?? false;

    public bool IsStoreManagementSelected
    {
        get => _isStoreManagementSelected;
        set => SetProperty(ref _isStoreManagementSelected, value);
    }

    public bool IsMyPreferencesSelected
    {
        get => _isMyPreferencesSelected;
        set => SetProperty(ref _isMyPreferencesSelected, value);
    }

    public ObservableCollection<TaxCategory> TaxCategories { get; } = new();

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
        set
        {
            if (SetProperty(ref _quickCash1, value))
            {
                SaveLocalPreferences();
            }
        }
    }

    public decimal QuickCash2
    {
        get => _quickCash2;
        set
        {
            if (SetProperty(ref _quickCash2, value))
            {
                SaveLocalPreferences();
            }
        }
    }

    public decimal QuickCash3
    {
        get => _quickCash3;
        set
        {
            if (SetProperty(ref _quickCash3, value))
            {
                SaveLocalPreferences();
            }
        }
    }
    public bool IsStoreNameUnlocked
    {
        get => _isStoreNameUnlocked;
        set
        {
            if (SetProperty(ref _isStoreNameUnlocked, value) && !value)
            {
                // Auto-save when the user clicks 'Done' and locks the field again.
                SaveSettings();
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
                // Auto-save when the user clicks 'Done' and locks the field again.
                SaveSettings();
            }
        }
    }

    public string CurrencyCode
    {
        get => _currencyCode;
        set
        {
            if (SetProperty(ref _currencyCode, value))
            {
                SaveSettings();
            }
        }
    }

    public bool TaxEnabled
    {
        get => _taxEnabled;
        set
        {
            if (SetProperty(ref _taxEnabled, value))
            {
                SaveSettings();
            }
        }
    }

    public decimal TaxRatePercent
    {
        get => _taxRatePercent;
        set
        {
            if (SetProperty(ref _taxRatePercent, value))
            {
                SaveSettings();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public TaxCategory? SelectedTaxCategory
    {
        get => _selectedTaxCategory;
        set
        {
            if (SetProperty(ref _selectedTaxCategory, value))
            {
                if (value != null)
                {
                    EditCategoryName = value.Name;
                    EditCategoryRate = value.RatePercent;
                }
                else
                {
                    EditCategoryName = string.Empty;
                    EditCategoryRate = 0;
                }
            }
        }
    }

    public string EditCategoryName
    {
        get => _editCategoryName;
        set => SetProperty(ref _editCategoryName, value);
    }

    public decimal EditCategoryRate
    {
        get => _editCategoryRate;
        set => SetProperty(ref _editCategoryRate, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand SaveStoreSettingsCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public RelayCommand NewCategoryCommand { get; }
    public RelayCommand SaveCategoryCommand { get; }
    public RelayCommand DeleteCategoryCommand { get; }



    private async void LoadSettings()
    {
        StoreName = _settingsRepository.GetStoreName();
        StoreAddress = _settingsRepository.GetStoreAddress();
        CurrencyCode = _settingsRepository.GetCurrencyCode();
        var tax = _settingsRepository.GetTaxSettings();
        TaxEnabled = tax.Enabled;
        TaxRatePercent = tax.RatePercent;
        IsStoreNameUnlocked = false;
        IsStoreAddressUnlocked = false;

        // Load Local Preferences
        var prefs = await _localPreferences.LoadPreferencesAsync();
        _isDarkMode = prefs.IsDarkMode;
        if (prefs.QuickCashAmounts != null && prefs.QuickCashAmounts.Length >= 3)
        {
            _quickCash1 = prefs.QuickCashAmounts[0];
            _quickCash2 = prefs.QuickCashAmounts[1];
            _quickCash3 = prefs.QuickCashAmounts[2];
        }
        OnPropertyChanged(nameof(IsDarkMode));
        OnPropertyChanged(nameof(QuickCash1));
        OnPropertyChanged(nameof(QuickCash2));
        OnPropertyChanged(nameof(QuickCash3));

        StatusMessage = "Settings loaded.";
    }

    private async void SaveLocalPreferences()
    {
        var prefs = await _localPreferences.LoadPreferencesAsync();
        prefs.IsDarkMode = IsDarkMode;
        prefs.QuickCashAmounts = new[] { QuickCash1, QuickCash2, QuickCash3 };

        await _localPreferences.SavePreferencesAsync(prefs);
        StatusMessage = "Preferences save automatically.";
    }





    private void LoadTaxCategories()
    {
        TaxCategories.Clear();
        foreach (var category in _taxCategoryRepository.GetAll())
        {
            TaxCategories.Add(category);
        }
    }

    private void SaveSettings()
    {
        if (!IsAdmin)
        {
            _audit.Log("SECURITY_VIOLATION", "Unauthorized attempt to save store settings.", _authService.CurrentUser?.Id);
            StatusMessage = "Permission denied: Admin access required to change store settings.";
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

        _settingsRepository.SetStoreName(StoreName ?? string.Empty);
        _settingsRepository.SetStoreAddress(StoreAddress ?? string.Empty);
        _settingsRepository.SetCurrencyCode(CurrencyCode);
        _settingsRepository.SetTaxSettings(new TaxSettings
        {
            Enabled = TaxEnabled,
            RatePercent = TaxRatePercent
        });

        IsStoreNameUnlocked = false;
        IsStoreAddressUnlocked = false;

        AppServices.RaiseSettingsUpdated();
        StatusMessage = $"Settings saved successfully at {DateTime.Now:t}";
    }

    private void NewCategory()
    {
        SelectedTaxCategory = null;
        EditCategoryName = string.Empty;
        EditCategoryRate = 0;
        StatusMessage = "Enter new tax category details.";
    }

    private void SaveCategory()
    {
        if (!IsAdmin)
        {
            _audit.Log("SECURITY_VIOLATION", "Unauthorized attempt to save tax category.", _authService.CurrentUser?.Id);
            StatusMessage = "Permission denied: Admin access required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditCategoryName))
        {
            StatusMessage = "Tax category name is required.";
            return;
        }

        if (EditCategoryRate < 0)
        {
            StatusMessage = "Tax rate must be zero or greater.";
            return;
        }

        if (SelectedTaxCategory == null)
        {
            // Create new category
            var newCategory = new TaxCategory
            {
                Name = EditCategoryName.Trim(),
                RatePercent = EditCategoryRate,
                IsDefault = false
            };
            _taxCategoryRepository.Create(newCategory);
            StatusMessage = $"Tax category '{newCategory.Name}' created.";
        }
        else
        {
            // Update existing category
            var updated = new TaxCategory
            {
                Id = SelectedTaxCategory.Id,
                Name = EditCategoryName.Trim(),
                RatePercent = EditCategoryRate,
                IsDefault = SelectedTaxCategory.IsDefault
            };
            _taxCategoryRepository.Update(updated);
            StatusMessage = $"Tax category '{updated.Name}' updated.";
        }

        LoadTaxCategories();
        SelectedTaxCategory = null;
    }

    private void DeleteCategory()
    {
        if (!IsAdmin)
        {
            _audit.Log("SECURITY_VIOLATION", "Unauthorized attempt to delete tax category.", _authService.CurrentUser?.Id);
            StatusMessage = "Permission denied: Admin access required.";
            return;
        }

        if (SelectedTaxCategory == null)
        {
            StatusMessage = "Select a tax category to delete.";
            return;
        }

        if (SelectedTaxCategory.IsDefault)
        {
            StatusMessage = "Default tax category cannot be deleted.";
            return;
        }

        var name = SelectedTaxCategory.Name;
        _taxCategoryRepository.Delete(SelectedTaxCategory.Id);

        StatusMessage = $"Tax category '{name}' deleted.";
        LoadTaxCategories();
        SelectedTaxCategory = null;
    }
}

