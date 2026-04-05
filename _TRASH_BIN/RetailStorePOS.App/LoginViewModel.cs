using System.Collections.ObjectModel;
using System.Windows.Controls;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.App;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly AuthService _authService;
    private string _username = string.Empty;
    private string _pin = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isPinMode = true;
    private bool _isSetupMode;
    private User? _selectedStaff;
    private bool _isUserSelected;

    public LoginViewModel(AuthService authService)
    {
        _authService = authService;
        _isSetupMode = authService.NeedsSetup;

        SwitchToPasswordCommand = new RelayCommand(() => IsPinMode = false);
        SwitchToPinCommand = new RelayCommand(() => IsPinMode = true);
        PinDigitCommand = new RelayCommand<string>(AddPinDigit);
        ClearPinCommand = new RelayCommand(() => Pin = string.Empty);
        BackspacePinCommand = new RelayCommand(BackspacePin);
        LoginPasswordCommand = new RelayCommand<PasswordBox>(LoginWithPassword);
        SelectStaffCommand = new RelayCommand<User>(SelectStaff);
        BackToListCommand = new RelayCommand(BackToList);

        // Load staff who have a PIN
        foreach (var user in _authService.GetActiveStaff())
            Staff.Add(user);
    }

    // ── Mode flags ──────────────────────────────────────────────────────

    public bool IsSetupMode
    {
        get => _isSetupMode;
        private set => SetProperty(ref _isSetupMode, value);
    }

    public bool IsPinMode
    {
        get => _isPinMode;
        set
        {
            if (SetProperty(ref _isPinMode, value))
            {
                OnPropertyChanged(nameof(IsPasswordMode));
                ErrorMessage = string.Empty;
                BackToList(); // reset staff selection when switching tabs
            }
        }
    }

    public bool IsPasswordMode => !_isPinMode;

    // ── Staff selection ─────────────────────────────────────────────────

    public ObservableCollection<User> Staff { get; } = new();

    public User? SelectedStaff
    {
        get => _selectedStaff;
        set => SetProperty(ref _selectedStaff, value);
    }

    public bool IsUserSelected
    {
        get => _isUserSelected;
        set
        {
            if (SetProperty(ref _isUserSelected, value))
                OnPropertyChanged(nameof(IsUserNotSelected));
        }
    }

    public bool IsUserNotSelected => !_isUserSelected;

    // ── Admin login ─────────────────────────────────────────────────────

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    // ── PIN ─────────────────────────────────────────────────────────────

    public string Pin
    {
        get => _pin;
        set
        {
            if (SetProperty(ref _pin, value))
            {
                OnPropertyChanged(nameof(PinDisplay));
                OnPropertyChanged(nameof(IsPinSlot1Filled));
                OnPropertyChanged(nameof(IsPinSlot2Filled));
                OnPropertyChanged(nameof(IsPinSlot3Filled));
                OnPropertyChanged(nameof(IsPinSlot4Filled));
                if (_pin.Length == 4)
                    TryPinLogin();
            }
        }
    }

    public string PinDisplay => new string('●', _pin.Length).PadRight(4, '○');
    public bool IsPinSlot1Filled => _pin.Length >= 1;
    public bool IsPinSlot2Filled => _pin.Length >= 2;
    public bool IsPinSlot3Filled => _pin.Length >= 3;
    public bool IsPinSlot4Filled => _pin.Length >= 4;

    // ── Errors ──────────────────────────────────────────────────────────

    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    // ── Commands ────────────────────────────────────────────────────────

    public RelayCommand SwitchToPasswordCommand { get; }
    public RelayCommand SwitchToPinCommand { get; }
    public RelayCommand<string> PinDigitCommand { get; }
    public RelayCommand ClearPinCommand { get; }
    public RelayCommand BackspacePinCommand { get; }
    public RelayCommand<PasswordBox> LoginPasswordCommand { get; }
    public RelayCommand<User> SelectStaffCommand { get; }
    public RelayCommand BackToListCommand { get; }

    public void ResetState()
    {
        Username = string.Empty;
        ErrorMessage = string.Empty;
        BackToList();
        IsPinMode = true;
    }

    // ── Private methods ─────────────────────────────────────────────────

    private void SelectStaff(User? user)
    {
        if (user == null) return;
        SelectedStaff = user;
        Pin = string.Empty;
        ErrorMessage = string.Empty;
        IsUserSelected = true;
    }

    private void BackToList()
    {
        SelectedStaff = null;
        Pin = string.Empty;
        ErrorMessage = string.Empty;
        IsUserSelected = false;
    }

    private void AddPinDigit(string? digit)
    {
        if (digit != null && _pin.Length < 4)
            Pin += digit;
    }

    private void BackspacePin()
    {
        if (_pin.Length > 0)
            Pin = _pin[..^1];
    }

    private void TryPinLogin()
    {
        if (SelectedStaff == null)
        {
            ErrorMessage = "Select a staff member first.";
            Pin = string.Empty;
            return;
        }

        var result = _authService.LoginWithPin(SelectedStaff.Username, _pin);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Login failed.";
            Pin = string.Empty;
        }
        else
        {
            ErrorMessage = string.Empty;
        }
    }

    private void LoginWithPassword(PasswordBox? passwordBox)
    {
        if (passwordBox == null) return;

        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Username is required.";
            return;
        }

        var password = passwordBox.Password;
        if (string.IsNullOrEmpty(password))
        {
            ErrorMessage = "Password is required.";
            return;
        }

        var result = _authService.LoginWithPassword(Username, password);
        if (!result.Success)
        {
            ErrorMessage = result.ErrorMessage ?? "Login failed.";
        }
        else
        {
            ErrorMessage = string.Empty;
            passwordBox.Clear();
        }
    }
}
