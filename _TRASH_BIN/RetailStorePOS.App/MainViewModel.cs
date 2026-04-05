using System.Diagnostics;
using RetailStorePOS.App.Services;

namespace RetailStorePOS.App;

public sealed class MainViewModel : ViewModelBase
{
    public string AppTitle => "RetailStorePOS POS";

    public CheckoutViewModel Checkout { get; }
    public ProductsViewModel Products { get; }
    public SettingsViewModel Settings { get; }
    public UserManagementViewModel UserManagement { get; }
    public ReportsViewModel Reports { get; }
    public HelpViewModel Help { get; }

    public MainViewModel()
    {
        Checkout = new CheckoutViewModel(AppServices.Sales, AppServices.Settings, AppServices.ProductSearch, AppServices.Audit, AppServices.ReceiptPdf, AppServices.LocalPreferences);
        Products = new ProductsViewModel(AppServices.Products, AppServices.TaxCategories, AppServices.ProductImports);
        Settings = new SettingsViewModel(AppServices.Settings, AppServices.TaxCategories, AppServices.LocalPreferences, AppServices.Auth, AppServices.Audit);
        UserManagement = new UserManagementViewModel(AppServices.Users, AppServices.Audit);
        Reports = new ReportsViewModel(AppServices.Sales, AppServices.ReceiptPdf);
        Help = new HelpViewModel();

        AppServices.Auth.LoginStateChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanCheckout));
            OnPropertyChanged(nameof(CanManageProducts));
            OnPropertyChanged(nameof(CanManageSettings));
            OnPropertyChanged(nameof(CanManageUsers));
            OnPropertyChanged(nameof(CanViewReports));
        };

        OpenUpdateLinkCommand = new RelayCommand(ExecuteOpenUpdateLink);
        DismissUpdateCommand = new RelayCommand(ExecuteDismissUpdate);

        // Start update check in the background without blocking the UI
        _ = CheckForUpdatesAsync();
    }

    public bool CanCheckout => AppServices.Auth.CanCheckout;
    public bool CanManageProducts => AppServices.Auth.CanManageProducts;
    public bool CanManageSettings => AppServices.Auth.CanManageSettings;
    public bool CanManageUsers => AppServices.Auth.CanManageUsers;
    public bool CanViewReports => AppServices.Auth.CanViewReports;
    public bool CanViewHelp => true; // Always visible

    // --- Auto-Updater Properties ---
    private bool _isUpdateAvailable;
    public bool IsUpdateAvailable
    {
        get => _isUpdateAvailable;
        set => SetProperty(ref _isUpdateAvailable, value);
    }

    private AppUpdateInfo? _updateInfo;
    public AppUpdateInfo? UpdateInfo
    {
        get => _updateInfo;
        set => SetProperty(ref _updateInfo, value);
    }

    public RelayCommand OpenUpdateLinkCommand { get; }
    public RelayCommand DismissUpdateCommand { get; }

    private async Task CheckForUpdatesAsync()
    {
        var update = await AppServices.Updater.CheckForUpdatesAsync();
        if (update != null)
        {
            UpdateInfo = update;
            IsUpdateAvailable = true;
        }
    }

    private void ExecuteOpenUpdateLink()
    {
        if (UpdateInfo != null && !string.IsNullOrEmpty(UpdateInfo.DownloadUrl))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = UpdateInfo.DownloadUrl,
                    UseShellExecute = true
                });
            }
            catch { } // Ignore if browser fails to launch
        }
    }

    private void ExecuteDismissUpdate()
    {
        IsUpdateAvailable = false;
    }
}
