using System.Collections.ObjectModel;
using System.Collections.Specialized;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class LoginWindowViewModel : ObservableObject
{
    private readonly AuthService _authService;
    private int _selectedModeIndex;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _pin = string.Empty;
    private string _errorMessage = string.Empty;
    private string _successMessage = string.Empty;
    private User? _selectedStaff;
    private bool _isAuthenticated;

    public LoginWindowViewModel(AuthService authService)
    {
        _authService = authService;
        IsSetupMode = authService.NeedsSetup;

        LoginPasswordCommand = new RelayCommand(LoginWithPassword);
        LoginPinCommand = new RelayCommand(LoginWithPin);

        foreach (var user in _authService.GetActiveStaff())
        {
            Staff.Add(new LoginStaffOption(user));
        }

        Staff.CollectionChanged += Staff_CollectionChanged;
    }

    public ObservableCollection<LoginStaffOption> Staff { get; } = new();

    public bool HasStaff => Staff.Count > 0;

    public RelayCommand LoginPasswordCommand { get; }

    public RelayCommand LoginPinCommand { get; }

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
        private set => SetProperty(ref _isAuthenticated, value);
    }

    private async void LoginWithPassword()
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

            CompleteLogin();
        }
        catch (Exception ex)
        {
            IsAuthenticated = false;
            LoginRuntime.ReportException(ex, "WinUiLogin.LoginWithPassword");
            ErrorMessage = "Unable to sign in right now. Please try again.";
        }
    }

    private async void LoginWithPin()
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

    private void ClearFeedback()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        IsAuthenticated = false;
    }

    private void Staff_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasStaff));
    }
}


