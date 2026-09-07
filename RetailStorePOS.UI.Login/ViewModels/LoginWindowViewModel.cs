using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Services;
using RetailStorePOS.UI.Common.Models;
using RetailStorePOS.Data.Models;
using Microsoft.UI.Xaml;

namespace RetailStorePOS.UI.Login.ViewModels;

public sealed partial class LoginWindowViewModel : ObservableObject
{
    private readonly AuthService _authService;
    public LocalizationService Loc => LocalizationService.Instance;

    private int _selectedModeIndex;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _pin = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private User? _selectedStaff;
    private BootstrapAdminHint? _bootstrapAdminHint;
    private bool _isAuthenticated;
    private Task? _initializeTask;

    public LoginWindowViewModel(AuthService authService)
    {
        _authService = authService;
        IsSetupMode = authService.NeedsSetup;

        LoginPasswordCommand = new AsyncRelayCommand(LoginWithPassword);
        LoginPinCommand = new AsyncRelayCommand(LoginWithPin);

        // Listen for language changes to refresh properties
        Loc.PropertyChanged += (s, e) =>
        {
            // Any property change on the LocalizationService (like CurrentLanguage or Item[])
            // indicates a language switch. Refresh all local string properties.
            OnPropertyChanged(string.Empty); // Refresh all bindings
        };

        if (IsSetupMode)
        {
            SelectedModeIndex = 1;
        }

        _initializeTask = InitializeCoreAsync();
    }

    public Task InitializeAsync() => _initializeTask ?? Task.CompletedTask;

    public void SetBootstrapAdminHint(BootstrapAdminHint hint)
    {
        _bootstrapAdminHint = hint;
        OnPropertyChanged(nameof(OnboardingHintBody));
        OnPropertyChanged(nameof(CashierPinTipSubtitle));
    }

    public bool IsSetupMode { get; }

    // ObservableCollection<T> projects to the WinRT IBindableObservableVector that
    // ItemsControl.ItemsSource requires. A raw object[] does NOT project and throws
    // ArgumentException (E_INVALIDARG) inside the x:Bind Update() pass, which aborts
    // the whole binding update and leaves every other bound field blank.
    private readonly ObservableCollection<object> _staff = new();
    public ObservableCollection<object> Staff => _staff;

    public bool HasStaff => _staff.Count > 0;

    public AsyncRelayCommand LoginPasswordCommand { get; }
    public AsyncRelayCommand LoginPinCommand { get; }

    public int SelectedModeIndex
    {
        get => _selectedModeIndex;
        set
        {
            if (SetProperty(ref _selectedModeIndex, value))
            {
                ClearFeedback();
                Password = string.Empty;
                Pin = string.Empty;
                SelectedStaff = null;
                OnPropertyChanged(nameof(ModeSubtitle));
                OnPropertyChanged(nameof(HeroSupportText));
            }
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                ClearFeedback();
            }
        }
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string Pin
    {
        get => _pin;
        set => SetProperty(ref _pin, value);
    }

    public User? SelectedStaff
    {
        get => _selectedStaff;
        set => SetProperty(ref _selectedStaff, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public string SuccessMessage
    {
        get => _successMessage;
        set
        {
            if (SetProperty(ref _successMessage, value))
            {
                OnPropertyChanged(nameof(HasSuccess));
            }
        }
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set => SetProperty(ref _isAuthenticated, value);
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);



    private async Task InitializeCoreAsync()
    {
        var activeStaff = await _authService.GetActiveStaffAsync();

        _staff.Clear();
        for (int i = 0; i < activeStaff.Count; i++)
        {
            _staff.Add(new LoginStaffOption(activeStaff[i]));
        }
        OnPropertyChanged(nameof(Staff));
        UpdateStaffDependentProperties();
    }

    private async Task LoginWithPassword()
    {
        if (IsAuthenticated) return;

        try
        {
            ClearFeedback();

            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            var result = await _authService.LoginWithPasswordAsync(Username, Password);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Login failed.";
                Password = string.Empty;
                return;
            }

            LoginRuntime.Telemetry?.TouchCurrentRun("login_password_success");
            CompleteLogin();
        }
        catch (Exception ex)
        {
            IsAuthenticated = false;
            Password = string.Empty;
            RetailStorePOS.UI.Common.LoginRuntime.ReportException(ex, "WinUiLogin.LoginWithPassword");
            ErrorMessage = "Unable to sign in right now. Please try again.";
        }
    }

    private async Task LoginWithPin()
    {
        if (IsAuthenticated) return;

        try
        {
            ClearFeedback();

            if (SelectedStaff is null)
            {
                ErrorMessage = "Choose a cashier profile first.";
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedStaff.PinHash))
            {
                ErrorMessage = $"{SelectedStaff.DisplayName} does not have a PIN configured.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Pin) || Pin.Length != 4 || !Pin.All(char.IsDigit))
            {
                ErrorMessage = "PIN must be exactly 4 digits.";
                return;
            }

            var result = await _authService.LoginWithPinAsync(SelectedStaff.Username, Pin);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Login failed.";
                Pin = string.Empty;
                return;
            }

            LoginRuntime.Telemetry?.TouchCurrentRun("login_pin_success");
            CompleteLogin();
        }
        catch (Exception ex)
        {
            IsAuthenticated = false;
            Pin = string.Empty;
            RetailStorePOS.UI.Common.LoginRuntime.ReportException(ex, "WinUiLogin.LoginWithPin");
            ErrorMessage = "Unable to sign in right now. Please try again.";
        }
    }

    private void CompleteLogin()
    {
        IsAuthenticated = true;
        SuccessMessage = _authService.CurrentUser is { } user
            ? $"Signed in as {user.DisplayName}."
            : "Signed in successfully.";
    }

    public string Generic_Close => Loc["Generic_Close"];

    private void ClearFeedback()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        IsAuthenticated = false;
    }

    private void UpdateStaffDependentProperties()
    {
        OnPropertyChanged(nameof(HasStaff));
        OnPropertyChanged(nameof(CashierBadgeText));
        OnPropertyChanged(nameof(CashierHelperText));
        OnPropertyChanged(nameof(SetupCalloutText));
    }

    // Dynamic Localization Properties
    public string ModeTitle => GetWithLog(nameof(ModeTitle), "LoginPage_ModeTitle_SignIn", "Sign In");

    public string ModeSubtitle
    {
        get
        {
            var key = SelectedModeIndex == 0 // CashierMode
                ? "LoginPage_ModeSubtitle_Cashier"
                : IsSetupMode ? "LoginPage_ModeSubtitle_Setup" : "LoginPage_ModeSubtitle_Admin";
            return GetWithLog(nameof(ModeSubtitle), key, "Access your workspace");
        }
    }

    public string HeroSupportText
    {
        get
        {
            var key = SelectedModeIndex == 0 // CashierMode
                ? "LoginPage_HeroSupport_Cashier"
                : IsSetupMode ? "LoginPage_HeroSupport_Setup" : "LoginPage_HeroSupport_Admin";
            return GetWithLog(nameof(HeroSupportText), key, "Support");
        }
    }

    public string CashierTabText => GetWithLog(nameof(CashierTabText), "LoginPage_CashierTabText", "Cashier");
    public string AdminTabText => GetWithLog(nameof(AdminTabText), "LoginPage_AdminTabText", "Admin");

    // Static text bindings replacing x:Uid
    public string StatusTitle => HasError ? Loc["Generic_Error"] : Loc["Generic_Success"];
    public string AdminHelperText => GetWithLog(nameof(AdminHelperText), "LoginPage_AdminHelperText.Text", "Sign in with your admin username and password.");
    public Visibility SetupCalloutVisibility => IsSetupMode ? Visibility.Visible : Visibility.Collapsed;
    public string LoginButtonContent => GetWithLog(nameof(LoginButtonContent), "LoginPage_LoginButton.Content", "Sign In");

    public string HeroBrandTitleText => GetWithLog(nameof(HeroBrandTitleText), "LoginPage_HeroBrandTitleText", "Retail Store");
    public string HeroBrandSubtitleText => GetWithLog(nameof(HeroBrandSubtitleText), "LoginPage_HeroBrandSubtitleText.Text", "Point of Sale");
    public string HeroHeadlineText => GetWithLog(nameof(HeroHeadlineText), "LoginPage_HeroHeadlineText.Text", "Retail login workspace");
    public string HeroBodyText => GetWithLog(nameof(HeroBodyText), "LoginPage_HeroBodyText.Text", "Fast cashier access. Secure admin control.");
    public string WelcomeText => GetWithLog(nameof(WelcomeText), "LoginPage_WelcomeText.Text", "Welcome back");
    public string CashierPanelTitle => GetWithLog(nameof(CashierPanelTitle), "LoginPage_CashierPanelTitle.Text", "Select your profile");
    public string CashierEmptyStateTitle => GetWithLog(nameof(CashierEmptyStateTitle), "LoginPage_CashierEmptyState_Title.Text", "No cashier profiles found");
    public string CashierEmptyStateBody => GetWithLog(nameof(CashierEmptyStateBody), "LoginPage_CashierEmptyState_Body.Text", "An admin must create cashier accounts first.");
    public string AdminPanelTitle => GetWithLog(nameof(AdminPanelTitle), "LoginPage_AdminPanelTitle.Text", "Admin Sign In");
    public string SetupCalloutTitle => GetWithLog(nameof(SetupCalloutTitle), "LoginPage_SetupCallout_Title.Text", "First Time Setup");
    public string UsernameLabel => GetWithLog(nameof(UsernameLabel), "LoginPage_UsernameLabel.Text", "Username");
    public string UsernameBoxPlaceholder => GetWithLog(nameof(UsernameBoxPlaceholder), "LoginPage_UsernameBox.PlaceholderText", "Enter your username");
    public string PasswordLabel => GetWithLog(nameof(PasswordLabel), "LoginPage_PasswordLabel.Text", "Password");
    public string AdminPasswordBoxPlaceholder => GetWithLog(nameof(AdminPasswordBoxPlaceholder), "LoginPage_AdminPasswordBox.PlaceholderText", "Enter your password");
    public string OnboardingHintTitle => GetWithLog(nameof(OnboardingHintTitle), "LoginPage_OnboardingHint_Title.Text", "Getting Started");
    public string OnboardingHintBody
    {
        get
        {
            var hint = GetBootstrapAdminHintForDisplay();
            if (hint is null)
            {
                return GetWithLog(nameof(OnboardingHintBody), "LoginPage_OnboardingHint_Body.Text");
            }

            return string.Format(
                Loc["LoginPage_OnboardingHint_Body_WithCredentials"],
                hint.Username,
                hint.Password,
                hint.Pin);
        }
    }
    public string PinLabel => GetWithLog(nameof(PinLabel), "LoginPage_PinLabel.Text", "PIN");
    public string PinPasswordBoxPlaceholder => GetWithLog(nameof(PinPasswordBoxPlaceholder), "LoginPage_PinPasswordBox.PlaceholderText", "Enter your PIN");
    public string CashierPinTipTitle => GetWithLog(nameof(CashierPinTipTitle), "LoginPage_CashierPinTip.Title", "Quick Login");

    public string CashierPinTipSubtitle
    {
        get
        {
            var hint = GetBootstrapAdminHintForDisplay();
            if (hint is not null)
            {
                return string.Format(Loc["LoginPage_CashierPinTip_Subtitle_WithPin"], hint.Pin);
            }
            return GetWithLog(nameof(CashierPinTipSubtitle), "LoginPage_CashierPinTip.Subtitle", "Use your PIN for faster access");
        }
    }

    public string SetupCalloutText
    {
        get
        {
            var key = HasStaff ? "LoginPage_SetupCallout_HasStaff" : "LoginPage_SetupCallout_NoStaff";
            return GetWithLog(nameof(SetupCalloutText), key, "Welcome! Set up your first profile.");
        }
    }

    public string CashierBadgeText
    {
        get
        {
            var key = HasStaff ? "LoginPage_Badge_Ready" : "LoginPage_Badge_NoProfiles";
            return GetWithLog(nameof(CashierBadgeText), key, HasStaff ? "Ready" : "No Profiles");
        }
    }

    public string CashierHelperText
    {
        get
        {
            var key = HasStaff ? "LoginPage_Helper_HasStaff" : "LoginPage_Helper_NoStaff";
            return GetWithLog(nameof(CashierHelperText), key, "Select your profile to begin.");
        }
    }

    private BootstrapAdminHint? GetBootstrapAdminHintForDisplay()
    {
        return _bootstrapAdminHint;
    }

    private string GetWithLog(string propName, string key, string fallback = "")
    {
        var val = Loc[key];
        if (string.IsNullOrEmpty(val) || val == key)
        {
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }
        return val;
    }
}
