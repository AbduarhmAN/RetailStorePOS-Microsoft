using System.Reflection;
using Windows.ApplicationModel;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public LocalizationService Loc => LocalizationService.Instance;

    private string? _currentTag;
    public string? CurrentTag
    {
        get => _currentTag;
        set
        {
            if (_currentTag != value)
            {
                _currentTag = value;
                OnPropertyChanged(nameof(CurrentTag));
                OnPropertyChanged(nameof(ShellContext));
            }
        }
    }

    private bool _isLoginPage;
    public bool IsLoginPage
    {
        get => _isLoginPage;
        set
        {
            if (_isLoginPage != value)
            {
                _isLoginPage = value;
                OnPropertyChanged(nameof(IsLoginPage));
                OnPropertyChanged(nameof(ShellContext));
            }
        }
    }

    private bool _isRegisterActive;
    public bool IsRegisterActive
    {
        get => _isRegisterActive;
        set
        {
            if (_isRegisterActive != value)
            {
                _isRegisterActive = value;
                OnPropertyChanged(nameof(IsRegisterActive));
                OnPropertyChanged(nameof(RegisterStatus));
            }
        }
    }

    public string RegisterStatus
    {
        get
        {
            return IsRegisterActive
                ? Loc["MainWindow_Status_RegisterActive"]
                : Loc["MainWindow_Status_NoActiveRegister"];
        }
    }

    public string ShellContext
    {
        get
        {
            if (IsLoginPage)
            {
                return Loc["MainWindow_Context_SecureSignIn"];
            }

            return CurrentTag switch
            {
                "checkout" => Loc["MainWindow_Context_Checkout"],
                "products" => Loc["MainWindow_Context_Products"],
                "reports" => Loc["MainWindow_Context_Reports"],
                "store" => Loc["MainWindow_Context_StoreSettings"],
                "tax" => Loc["MainWindow_Context_Tax"],
                "users" => Loc["MainWindow_Context_Users"],
                "prefs" => Loc["MainWindow_Context_Preferences"],
                "settings" => Loc["MainWindow_Context_StoreSettings"],
                "about" => Loc["MainWindow_Context_About"],
                _ => Loc["MainWindow_Context_Default"]
            };
        }
    }

    public string BrandTitle => GetAppName();
    public string SplashTitle => GetAppName();
    public string SplashSubtitle => GetWithLog("SplashSubtitle", "MainWindow_Splash_Subtitle");

    private string GetAppName()
    {
        var localizedAppName = Loc["AppDisplayName"];
        // If the resource has a real translation, use it.
        if (!string.IsNullOrEmpty(localizedAppName) && localizedAppName != "AppDisplayName")
        {
            return localizedAppName;
        }

        // 1. Try Assembly Product
        var product = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyProductAttribute>()?
            .Product;
        if (!string.IsNullOrWhiteSpace(product)) return product;

        // 2. Try Package Display Name
        try
        {
            var packageDisplayName = Package.Current.DisplayName;
            if (!string.IsNullOrWhiteSpace(packageDisplayName)) return packageDisplayName;
        }
        catch { }

        // 3. Try Assembly Title
        var title = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyTitleAttribute>()?
            .Title;
        if (!string.IsNullOrWhiteSpace(title)) return title;

        return "Retail Store POS";
    }
    public string SplashStatusTitle => GetWithLog("SplashStatusTitle", "MainWindow_SplashStatusTitle.Text");
    public string SplashStatusDetail => GetWithLog("SplashStatusDetail", "MainWindow_SplashStatusDetail.Text");

    public string MainWindow_Title => Loc["MainWindow_Title"];
    public string MainWindow_CashInOut_Text => Loc["MainWindow_CashInOutMenuItem.Text"];
    public string MainWindow_CloseRegister_Text => Loc["MainWindow_CloseRegisterMenuItem.Text"];
    public string MainWindow_Logout_Text => Loc["MainWindow_LogoutMenuItem.Text"];
    public string MainWindow_Tutorial_Title => Loc["MainWindow_Tutorial.Title"];
    public string MainWindow_Tutorial_Subtitle => Loc["MainWindow_Tutorial.Subtitle"];
    public string MainWindow_Tutorial_CloseButtonContent => Loc["MainWindow_Tutorial.CloseButtonContent"];

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(ShellContext));
        OnPropertyChanged(nameof(BrandTitle));
        OnPropertyChanged(nameof(SplashTitle));
        OnPropertyChanged(nameof(SplashSubtitle));
        OnPropertyChanged(nameof(SplashStatusTitle));
        OnPropertyChanged(nameof(SplashStatusDetail));
        OnPropertyChanged(nameof(RegisterStatus));
        OnPropertyChanged(nameof(MainWindow_Title));
        OnPropertyChanged(nameof(MainWindow_CashInOut_Text));
        OnPropertyChanged(nameof(MainWindow_CloseRegister_Text));
        OnPropertyChanged(nameof(MainWindow_Logout_Text));
        OnPropertyChanged(nameof(MainWindow_Tutorial_Title));
        OnPropertyChanged(nameof(MainWindow_Tutorial_Subtitle));
        OnPropertyChanged(nameof(MainWindow_Tutorial_CloseButtonContent));
    }

    private string GetWithLog(string propName, string key)
    {
        _ = propName;
        return Loc[key];
    }

    public MainWindowViewModel()
    {
        // Listen for language changes to refresh properties
        Loc.PropertyChanged += (s, e) =>
        {
            RefreshLocalizedProperties();
        };
    }
}
