using System.Collections.ObjectModel;
using System.Collections.Specialized;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;
using Microsoft.UI.Xaml;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class LoginWindowViewModel : ObservableObject
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
    private bool _isAuthenticated;
    private Task? _initializeTask;

    public LoginWindowViewModel(AuthService authService)
    {
        _authService = authService;
        IsSetupMode = authService.NeedsSetup;

        LoginPasswordCommand = new AsyncRelayCommand(LoginWithPassword);
        LoginPinCommand = new AsyncRelayCommand(LoginWithPin);

        Staff.CollectionChanged += Staff_CollectionChanged;

        // Listen for language changes to refresh properties
        Loc.PropertyChanged += (s, e) =>
        {
            RefreshLocalizedProperties();
        };
    }

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(ModeTitle));
        OnPropertyChanged(nameof(ModeSubtitle));
        OnPropertyChanged(nameof(HeroSupportText));
        OnPropertyChanged(nameof(CashierTabText));
        OnPropertyChanged(nameof(AdminTabText));
        OnPropertyChanged(nameof(HeroBrandTitleText));
        OnPropertyChanged(nameof(HeroBrandSubtitleText));
        OnPropertyChanged(nameof(HeroHeadlineText));
        OnPropertyChanged(nameof(HeroBodyText));
        OnPropertyChanged(nameof(WelcomeText));
        OnPropertyChanged(nameof(CashierPanelTitle));
        OnPropertyChanged(nameof(CashierEmptyStateTitle));
        OnPropertyChanged(nameof(CashierEmptyStateBody));
        OnPropertyChanged(nameof(AdminPanelTitle));
        OnPropertyChanged(nameof(SetupCalloutTitle));
        OnPropertyChanged(nameof(UsernameLabel));
        OnPropertyChanged(nameof(UsernameBoxPlaceholder));
        OnPropertyChanged(nameof(PasswordLabel));
        OnPropertyChanged(nameof(AdminPasswordBoxPlaceholder));
        OnPropertyChanged(nameof(OnboardingHintTitle));
        OnPropertyChanged(nameof(OnboardingHintBody));
        OnPropertyChanged(nameof(PinLabel));
        OnPropertyChanged(nameof(PinPasswordBoxPlaceholder));
        OnPropertyChanged(nameof(CashierPinTipTitle));
        OnPropertyChanged(nameof(CashierPinTipSubtitle));
        OnPropertyChanged(nameof(CashierBadgeText));
        OnPropertyChanged(nameof(CashierHelperText));
        OnPropertyChanged(nameof(AdminHelperText));
        OnPropertyChanged(nameof(SetupCalloutText));
        OnPropertyChanged(nameof(LoginButtonContent));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(Generic_Close));
    }


    public ObservableCollection<LoginStaffOption> Staff { get; } = new();

    public bool HasStaff => Staff.Count > 0;

    public AsyncRelayCommand LoginPasswordCommand { get; }

    public AsyncRelayCommand LoginPinCommand { get; }

    public bool IsSetupMode { get; }

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
                OnPropertyChanged(nameof(AdminHelperText));
            }
        }
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
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
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(StatusTitle));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public string SuccessMessage
    {
        get => _successMessage;
        private set => SetProperty(ref _successMessage, value);
    }

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        private set
        {
            if (SetProperty(ref _isAuthenticated, value))
            {
                OnPropertyChanged(nameof(StatusTitle));
            }
        }
    }

    public Task InitializeAsync()
    {
        return _initializeTask ??= InitializeCoreAsync();
    }

    private async Task InitializeCoreAsync()
    {
        var activeStaff = await _authService.GetActiveStaffAsync();

        Staff.Clear();
        foreach (var user in activeStaff)
        {
            Staff.Add(new LoginStaffOption(user));
        }
    }

    private async Task LoginWithPassword()
    {
        try
        {
            ClearFeedback();

            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Username is required.";
                return;
            }

            if (string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Password is required.";
                return;
            }

            var result = await _authService.LoginWithPasswordAsync(Username, Password);
            if (!result.Success)
            {
                ErrorMessage = result.ErrorMessage ?? "Login failed.";
                return;
            }

            LoginRuntime.Telemetry?.TouchCurrentRun("login_password_success");
            CompleteLogin();
        }
        catch (Exception ex)
        {
            IsAuthenticated = false;
            LoginRuntime.ReportException(ex, "WinUiLogin.LoginWithPassword");
            ErrorMessage = "Unable to sign in right now. Please try again.";
        }
    }

    private async Task LoginWithPin()
    {
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

            if (string.IsNullOrWhiteSpace(Pin) || Pin.Length < 4)
            {
                ErrorMessage = "PIN must be 4 digits.";
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
            LoginRuntime.ReportException(ex, "WinUiLogin.LoginWithPin");
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

    private void Staff_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasStaff));
        OnPropertyChanged(nameof(CashierBadgeText));
        OnPropertyChanged(nameof(CashierHelperText));
        OnPropertyChanged(nameof(SetupCalloutText));
    }

    // Dynamic Localization Properties
    public string ModeTitle => GetWithLog(nameof(ModeTitle), "LoginPage_ModeTitle_SignIn");

    public string ModeSubtitle
    {
        get
        {
            var key = SelectedModeIndex == 0 // CashierMode
                ? "LoginPage_ModeSubtitle_Cashier"
                : IsSetupMode ? "LoginPage_ModeSubtitle_Setup" : "LoginPage_ModeSubtitle_Admin";
            return GetWithLog(nameof(ModeSubtitle), key);
        }
    }

    public string HeroSupportText
    {
        get
        {
            var key = SelectedModeIndex == 0 // CashierMode
                ? "LoginPage_HeroSupport_Cashier"
                : IsSetupMode ? "LoginPage_HeroSupport_Setup" : "LoginPage_HeroSupport_Admin";
            return GetWithLog(nameof(HeroSupportText), key);
        }
    }

    public string CashierTabText => GetWithLog(nameof(CashierTabText), "LoginPage_CashierTabText");
    public string AdminTabText => GetWithLog(nameof(AdminTabText), "LoginPage_AdminTabText");

    // Static text bindings replacing x:Uid
    public string HeroBrandTitleText => GetWithLog(nameof(HeroBrandTitleText), "LoginPage_HeroBrandTitleText");
    public string HeroBrandSubtitleText => GetWithLog(nameof(HeroBrandSubtitleText), "LoginPage_HeroBrandSubtitleText.Text");
    public string HeroHeadlineText => GetWithLog(nameof(HeroHeadlineText), "LoginPage_HeroHeadlineText.Text");
    public string HeroBodyText => GetWithLog(nameof(HeroBodyText), "LoginPage_HeroBodyText.Text");
    public string WelcomeText => GetWithLog(nameof(WelcomeText), "LoginPage_WelcomeText.Text");
    public string CashierPanelTitle => GetWithLog(nameof(CashierPanelTitle), "LoginPage_CashierPanelTitle.Text");
    public string CashierEmptyStateTitle => GetWithLog(nameof(CashierEmptyStateTitle), "LoginPage_CashierEmptyState_Title.Text");
    public string CashierEmptyStateBody => GetWithLog(nameof(CashierEmptyStateBody), "LoginPage_CashierEmptyState_Body.Text");
    public string AdminPanelTitle => GetWithLog(nameof(AdminPanelTitle), "LoginPage_AdminPanelTitle.Text");
    public string SetupCalloutTitle => GetWithLog(nameof(SetupCalloutTitle), "LoginPage_SetupCallout_Title.Text");
    public string UsernameLabel => GetWithLog(nameof(UsernameLabel), "LoginPage_UsernameLabel.Text");
    public string UsernameBoxPlaceholder => GetWithLog(nameof(UsernameBoxPlaceholder), "LoginPage_UsernameBox.PlaceholderText");
    public string PasswordLabel => GetWithLog(nameof(PasswordLabel), "LoginPage_PasswordLabel.Text");
    public string AdminPasswordBoxPlaceholder => GetWithLog(nameof(AdminPasswordBoxPlaceholder), "LoginPage_AdminPasswordBox.PlaceholderText");
    public string OnboardingHintTitle => GetWithLog(nameof(OnboardingHintTitle), "LoginPage_OnboardingHint_Title.Text");
    public string OnboardingHintBody => GetWithLog(nameof(OnboardingHintBody), "LoginPage_OnboardingHint_Body.Text");
    public string PinLabel => GetWithLog(nameof(PinLabel), "LoginPage_PinLabel.Text");
    public string PinPasswordBoxPlaceholder => GetWithLog(nameof(PinPasswordBoxPlaceholder), "LoginPage_PinPasswordBox.PlaceholderText");
    public string CashierPinTipTitle => GetWithLog(nameof(CashierPinTipTitle), "LoginPage_CashierPinTip.Title");
    public string CashierPinTipSubtitle => GetWithLog(nameof(CashierPinTipSubtitle), "LoginPage_CashierPinTip.Subtitle");

    public string CashierBadgeText
    {
        get
        {
            var key = HasStaff ? "LoginPage_Badge_Ready" : "LoginPage_Badge_NoProfiles";
            return GetWithLog(nameof(CashierBadgeText), key);
        }
    }

    public string CashierHelperText
    {
        get
        {
            var key = HasStaff ? "LoginPage_Helper_HasStaff" : "LoginPage_Helper_NoStaff";
            return GetWithLog(nameof(CashierHelperText), key);
        }
    }

    public string AdminHelperText
    {
        get
        {
            var key = IsSetupMode ? "LoginPage_AdminHelper_Setup" : "LoginPage_AdminHelper_Admin";
            return GetWithLog(nameof(AdminHelperText), key);
        }
    }

    public string SetupCalloutText
    {
        get
        {
            var key = HasStaff ? "LoginPage_SetupCallout_HasStaff" : "LoginPage_SetupCallout_NoStaff";
            return GetWithLog(nameof(SetupCalloutText), key);
        }
    }

    public string LoginButtonContent => GetWithLog(nameof(LoginButtonContent), "LoginPage_LoginButton_Content");

    public string StatusTitle
    {
        get
        {
            string result = string.Empty;
            if (HasError)
            {
                var key = SelectedModeIndex == 0 // CashierMode
                    ? "LoginPage_Status_CashierFailed"
                    : "LoginPage_Status_AdminFailed";
                result = Loc[key];
            }
            else if (IsAuthenticated)
            {
                var key = SelectedModeIndex == 0 // CashierMode
                    ? "LoginPage_Status_CashierUnlocked"
                    : "LoginPage_Status_AdminSignedIn";
                result = Loc[key];
            }
            return result;
        }
    }

    private string GetWithLog(string propName, string key)
    {
        _ = propName;
        return Loc[key];
    }

    public Visibility SetupCalloutVisibility => IsSetupMode ? Visibility.Visible : Visibility.Collapsed;
}


