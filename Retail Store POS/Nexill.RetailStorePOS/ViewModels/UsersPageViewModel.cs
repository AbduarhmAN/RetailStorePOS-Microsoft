using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.App.Services;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;
using RetailStorePOS.WinUiLogin.Common;

namespace RetailStorePOS.WinUiLogin.ViewModels;

public sealed class UsersPageViewModel : ObservableObject, IDisposable
{
    private readonly UserRepository _userRepository;
    private readonly AuditLogService _audit;
    private readonly EventHandler _loginStateChangedHandler;
    private User? _selectedUser;
    private bool _isNewUser;
    private string _statusMessage = string.Empty;
    private bool _isEditUnlocked;
    private bool _showUnlockPrompt;

    private string _editDisplayName = string.Empty;
    private string _editUsername = string.Empty;
    private bool _editIsAdmin;
    private string _editPin = string.Empty;
    private bool _editCanCheckout = true;
    private bool _editCanManageProducts;
    private bool _editCanManageSettings;
    private bool _editCanManageUsers;
    private bool _editCanViewReports;
    private bool _editCanOverridePrice;

    public UsersPageViewModel()
    {
        _userRepository = LoginRuntime.Users;
        _audit = LoginRuntime.Audit;
        _loginStateChangedHandler = OnLoginStateChanged;

        Users = new ObservableCollection<User>();

        NewUserCommand = new RelayCommand(CreateNewUser);
        SaveUserCommand = new RelayCommand<PasswordBox>(SaveUser);
        CancelCommand = new RelayCommand(Cancel);
        DeactivateUserCommand = new RelayCommand(DeactivateUser);
        UnlockEditCommand = new RelayCommand(UnlockEdit);
        ConfirmUnlockEditCommand = new RelayCommand(ConfirmUnlockEdit);
        DismissUnlockPromptCommand = new RelayCommand(DismissUnlockPrompt);

        LoginRuntime.Auth.LoginStateChanged += _loginStateChangedHandler;
        ReloadUsers();
    }

    public ObservableCollection<User> Users { get; }

    public bool CanManageUsers => LoginRuntime.Auth.CurrentUser?.IsAdmin == true;
    public bool IsOnboardingActive => !LoginRuntime.Settings.IsOnboardingPhaseCleared() && LoginRuntime.Auth.CurrentUser?.Username == "admin";
    public Visibility ReadOnlyBannerVisibility => CanManageUsers ? Visibility.Collapsed : Visibility.Visible;

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                _isNewUser = false;
                ShowUnlockPrompt = false;
                IsEditUnlocked = false;
                LoadUserDetails();
                RaiseSelectionProperties();
            }
        }
    }

    public bool IsEditUnlocked
    {
        get => _isEditUnlocked;
        set
        {
            if (SetProperty(ref _isEditUnlocked, value))
            {
                if (value)
                {
                    ShowUnlockPrompt = false;
                }

                OnPropertyChanged(nameof(IsEditLocked));
                OnPropertyChanged(nameof(IsUnlockRequired));
                OnPropertyChanged(nameof(IsUnlockRequiredVisibility));
                OnPropertyChanged(nameof(EditModeLabel));
            }
        }
    }

    public bool IsEditLocked => !IsEditUnlocked;
    public bool HasSelectedUser => _selectedUser != null || _isNewUser;
    public bool ShowEmptySelectionState => !HasSelectedUser;
    public bool IsNewUserDraft => _isNewUser;

    public bool ShowUnlockPrompt
    {
        get => _showUnlockPrompt;
        set
        {
            if (SetProperty(ref _showUnlockPrompt, value))
            {
                OnPropertyChanged(nameof(ShowUnlockPromptVisibility));
            }
        }
    }

    public bool CanDeactivate => _selectedUser != null && !_isNewUser && _selectedUser.Id != LoginRuntime.Auth.CurrentUser?.Id;
    public bool IsUnlockRequired => _selectedUser != null && !_isNewUser && !IsEditUnlocked;
    public Visibility ShowUnlockPromptVisibility => ShowUnlockPrompt ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ShowEmptySelectionStateVisibility => ShowEmptySelectionState ? Visibility.Visible : Visibility.Collapsed;
    public Visibility HasSelectedUserVisibility => HasSelectedUser ? Visibility.Visible : Visibility.Collapsed;
    public Visibility NoSelectionVisibility => HasSelectedUser ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CanDeactivateVisibility => CanDeactivate ? Visibility.Visible : Visibility.Collapsed;
    public Visibility IsUnlockRequiredVisibility => IsUnlockRequired ? Visibility.Visible : Visibility.Collapsed;

    public int TotalUsers => Users.Count;
    public int AdminUsers => Users.Count(user => user.IsAdmin);
    public int StandardUsers => Users.Count(user => !user.IsAdmin);

    public string EditModeLabel => _isNewUser
        ? "Draft"
        : _selectedUser == null
            ? "Preview"
            : IsEditUnlocked
                ? "Editing Unlocked"
                : "Editing Locked";

    public string EditorTitle => _isNewUser
        ? "Create User"
        : _selectedUser != null
            ? $"Profile: {EditDisplayName}"
            : "Select a User";

    public string EditorSubtitle => _isNewUser
        ? "Set identity, sign-in credentials, and permissions for a new staff account."
        : _selectedUser != null
            ? "Review identity, login details, and permissions before saving changes."
            : "Choose a user from the roster to view account details.";

    public string AccessLevelLabel => _isNewUser
        ? (EditIsAdmin ? "NEW ADMIN" : "NEW STAFF")
        : (EditIsAdmin ? "ADMINISTRATOR" : "CASHIER");

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
                OnPropertyChanged(nameof(HasStatusVisibility));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);
    public Visibility HasStatusVisibility => HasStatus ? Visibility.Visible : Visibility.Collapsed;

    public string EditDisplayName
    {
        get => _editDisplayName;
        set
        {
            if (SetProperty(ref _editDisplayName, value))
            {
                OnPropertyChanged(nameof(EditorTitle));
            }
        }
    }

    public string EditUsername
    {
        get => _editUsername;
        set => SetProperty(ref _editUsername, value);
    }

    public bool EditIsAdmin
    {
        get => _editIsAdmin;
        set
        {
            if (SetProperty(ref _editIsAdmin, value))
            {
                if (value)
                {
                    SetAdminPermissions();
                }
                else
                {
                    ClearAdminPermissions();
                }
            }

            OnPropertyChanged(nameof(AccessLevelLabel));
            OnPropertyChanged(nameof(AdminPasswordVisibility));
        }
    }

    public string EditPin
    {
        get => _editPin;
        set => SetProperty(ref _editPin, value);
    }

    public bool EditCanCheckout
    {
        get => _editCanCheckout;
        set => SetProperty(ref _editCanCheckout, value);
    }

    public bool EditCanManageProducts
    {
        get => _editCanManageProducts;
        set => SetProperty(ref _editCanManageProducts, value);
    }

    public bool EditCanManageSettings
    {
        get => _editCanManageSettings;
        set => SetProperty(ref _editCanManageSettings, value);
    }

    public bool EditCanManageUsers
    {
        get => _editCanManageUsers;
        set => SetProperty(ref _editCanManageUsers, value);
    }

    public bool EditCanViewReports
    {
        get => _editCanViewReports;
        set => SetProperty(ref _editCanViewReports, value);
    }

    public bool EditCanOverridePrice
    {
        get => _editCanOverridePrice;
        set => SetProperty(ref _editCanOverridePrice, value);
    }

    public Visibility AdminPasswordVisibility => EditIsAdmin ? Visibility.Visible : Visibility.Collapsed;

    public RelayCommand NewUserCommand { get; }
    public RelayCommand<PasswordBox> SaveUserCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeactivateUserCommand { get; }
    public RelayCommand UnlockEditCommand { get; }
    public RelayCommand ConfirmUnlockEditCommand { get; }
    public RelayCommand DismissUnlockPromptCommand { get; }

    public void Dispose()
    {
        LoginRuntime.Auth.LoginStateChanged -= _loginStateChangedHandler;
    }

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        SelectedUser = null;
        _isNewUser = false;
        ShowUnlockPrompt = false;
        IsEditUnlocked = false;
        ReloadUsers();
        RaiseSelectionProperties();
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(IsOnboardingActive));
        OnPropertyChanged(nameof(ReadOnlyBannerVisibility));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanDeactivateVisibility));
    }

    private void UnlockEdit()
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        if (_selectedUser == null || _isNewUser || IsEditUnlocked)
        {
            return;
        }

        ShowUnlockPrompt = true;
    }

    private void ConfirmUnlockEdit()
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        if (_selectedUser == null || _isNewUser)
        {
            return;
        }

        IsEditUnlocked = true;
        StatusMessage = $"Editing unlocked for '{EditDisplayName}'. Review changes and save when ready.";
    }

    private void DismissUnlockPrompt()
    {
        ShowUnlockPrompt = false;
    }

    private bool EnsureCanManageUsers()
    {
        if (CanManageUsers)
        {
            return true;
        }

        StatusMessage = "Only administrators can manage users.";
        return false;
    }

    private void ReloadUsers(long? reselectUserId = null)
    {
        var targetId = reselectUserId ?? _selectedUser?.Id;

        Users.Clear();
        foreach (var user in _userRepository.GetActive())
        {
            Users.Add(user);
        }

        OnPropertyChanged(nameof(TotalUsers));
        OnPropertyChanged(nameof(AdminUsers));
        OnPropertyChanged(nameof(StandardUsers));
        OnPropertyChanged(nameof(CanManageUsers));
        OnPropertyChanged(nameof(ReadOnlyBannerVisibility));

        if (targetId.HasValue)
        {
            SelectedUser = Users.FirstOrDefault(user => user.Id == targetId.Value);
        }

        RaiseSelectionProperties();
    }

    private void LoadUserDetails()
    {
        if (_selectedUser == null)
        {
            ClearEditFields();
            return;
        }

        EditDisplayName = _selectedUser.DisplayName;
        EditUsername = _selectedUser.Username;
        EditIsAdmin = _selectedUser.IsAdmin;
        EditPin = string.Empty;

        if (_selectedUser.IsAdmin)
        {
            SetAdminPermissions();
        }
        else
        {
            EditCanCheckout = _selectedUser.CanCheckout;
            EditCanManageProducts = _selectedUser.CanManageProducts;
            EditCanManageSettings = _selectedUser.CanManageSettings;
            ClearAdminPermissions();
            EditCanViewReports = _selectedUser.CanViewReports;
            EditCanOverridePrice = _selectedUser.CanOverridePrice;
        }

        StatusMessage = string.Empty;
    }

    private void ClearEditFields()
    {
        EditDisplayName = string.Empty;
        EditUsername = string.Empty;
        EditIsAdmin = false;
        EditPin = string.Empty;
        EditCanCheckout = true;
        EditCanManageProducts = false;
        EditCanManageSettings = false;
        EditCanManageUsers = false;
        EditCanViewReports = false;
        EditCanOverridePrice = false;
        StatusMessage = string.Empty;
    }

    private void SetAdminPermissions()
    {
        EditCanCheckout = true;
        EditCanManageProducts = true;
        EditCanManageSettings = true;
        EditCanManageUsers = true;
        EditCanViewReports = true;
        EditCanOverridePrice = true;
    }

    private void ClearAdminPermissions()
    {
        EditCanManageUsers = false;
    }

    private void CreateNewUser()
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        SelectedUser = null;
        _isNewUser = true;
        ShowUnlockPrompt = false;
        IsEditUnlocked = true;
        ClearEditFields();
        RaiseSelectionProperties();
    }

    private void SaveUser(PasswordBox? passwordBox)
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        EditDisplayName = EditDisplayName.Trim();
        EditUsername = EditUsername.Trim();

        if (string.IsNullOrWhiteSpace(EditDisplayName))
        {
            StatusMessage = "Display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(EditUsername))
        {
            StatusMessage = "Username is required.";
            return;
        }

        var enteredPassword = passwordBox?.Password ?? string.Empty;

        if (_isNewUser)
        {
            if (EditIsAdmin)
            {
                if (string.IsNullOrEmpty(enteredPassword) && string.IsNullOrEmpty(EditPin))
                {
                    StatusMessage = "Admin user requires at least a password or PIN.";
                    return;
                }
            }
            else if (string.IsNullOrEmpty(EditPin))
            {
                StatusMessage = "PIN is required for cashiers.";
                return;
            }
        }

        if (_selectedUser != null && EditIsAdmin)
        {
            var hasStoredPassword = !string.IsNullOrEmpty(_selectedUser.PasswordHash);
            var hasStoredPin = !string.IsNullOrEmpty(_selectedUser.PinHash);
            if (!hasStoredPassword && !hasStoredPin && string.IsNullOrEmpty(enteredPassword) && string.IsNullOrEmpty(EditPin))
            {
                StatusMessage = "Admin user requires at least a password or PIN.";
                return;
            }
        }

        if (!string.IsNullOrEmpty(EditPin))
        {
            if (EditPin.Length != 4 || !EditPin.All(char.IsDigit))
            {
                StatusMessage = "PIN must be exactly 4 digits.";
                return;
            }
        }

        if (EditIsAdmin)
        {
            SetAdminPermissions();
        }
        else
        {
            ClearAdminPermissions();
        }

        if (_userRepository.UsernameExists(EditUsername, _selectedUser?.Id))
        {
            StatusMessage = $"Username '{EditUsername}' is already in use.";
            return;
        }

        try
        {
            long? userToReselectId = _selectedUser?.Id;

            if (_isNewUser)
            {
                var user = new User
                {
                    DisplayName = EditDisplayName,
                    Username = EditUsername,
                    IsAdmin = EditIsAdmin,
                    CanCheckout = EditCanCheckout,
                    CanManageProducts = EditCanManageProducts,
                    CanManageSettings = EditCanManageSettings,
                    CanManageUsers = EditIsAdmin,
                    CanViewReports = EditCanViewReports,
                    CanOverridePrice = EditCanOverridePrice,
                    IsActive = true
                };

                if (EditIsAdmin && passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                {
                    user.PasswordHash = UserRepository.HashPassword(passwordBox.Password);
                }

                if (!string.IsNullOrEmpty(EditPin))
                {
                    user.PinHash = UserRepository.HashPin(EditPin);
                }

                var newId = _userRepository.Create(user);
                userToReselectId = newId;
                _audit.Log("USER_CREATED", $"Created user: {user.Username}, Role: {(user.IsAdmin ? "Admin" : "Cashier")}", LoginRuntime.Auth.CurrentUser?.Id);
                StatusMessage = $"User '{EditDisplayName}' created successfully.";
                _isNewUser = false;
            }
            else if (_selectedUser != null)
            {
                _selectedUser.DisplayName = EditDisplayName;
                _selectedUser.Username = EditUsername;
                _selectedUser.IsAdmin = EditIsAdmin;
                _selectedUser.CanCheckout = EditCanCheckout;
                _selectedUser.CanManageProducts = EditCanManageProducts;
                _selectedUser.CanManageSettings = EditCanManageSettings;
                _selectedUser.CanManageUsers = EditIsAdmin;
                _selectedUser.CanViewReports = EditCanViewReports;
                _selectedUser.CanOverridePrice = EditCanOverridePrice;

                if (EditIsAdmin && passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                {
                    _selectedUser.PasswordHash = UserRepository.HashPassword(passwordBox.Password);
                }

                if (!string.IsNullOrEmpty(EditPin))
                {
                    _selectedUser.PinHash = UserRepository.HashPin(EditPin);
                }

                _userRepository.Update(_selectedUser);
                _audit.Log("USER_UPDATED", $"Updated user: {_selectedUser.Username}", LoginRuntime.Auth.CurrentUser?.Id);
                StatusMessage = $"User '{EditDisplayName}' updated successfully.";

                // If this is the "admin" user changing their password away from "1234", complete the onboarding phase permanently.
                if (_selectedUser.Username == "admin" &&
                    !LoginRuntime.Settings.IsOnboardingPhaseCleared() &&
                    passwordBox != null &&
                    !string.IsNullOrEmpty(passwordBox.Password) &&
                    passwordBox.Password != "1234")
                {
                    LoginRuntime.Settings.ClearOnboardingPhase();
                }
            }

            var currentUserId = LoginRuntime.Auth.CurrentUser?.Id;
            if (userToReselectId.HasValue && currentUserId == userToReselectId.Value)
            {
                LoginRuntime.Auth.RefreshCurrentUser();
            }

            IsEditUnlocked = false;
            ReloadUsers(userToReselectId);
            if (userToReselectId.HasValue)
            {
                SelectedUser = Users.FirstOrDefault(user => user.Id == userToReselectId.Value);
            }

            if (passwordBox != null)
            {
                passwordBox.Password = string.Empty;
            }

            OnPropertyChanged(nameof(IsOnboardingActive));
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Username is already in use.", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = $"Username '{EditUsername}' is already in use.";
            LoginRuntime.ReportException(ex, "UsersPageViewModel.Save.DuplicateUsername");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            StatusMessage = $"Username '{EditUsername}' is already in use.";
            LoginRuntime.ReportException(ex, "UsersPageViewModel.Save.SqliteConstraint");
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to save user. Please review the fields and try again.";
            LoginRuntime.ReportException(ex, "UsersPageViewModel.Save");
        }
    }

    private void Cancel()
    {
        _isNewUser = false;
        SelectedUser = null;
        ShowUnlockPrompt = false;
        IsEditUnlocked = false;
        ClearEditFields();
        RaiseSelectionProperties();
    }

    private void DeactivateUser()
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        if (_selectedUser == null || _selectedUser.Id == LoginRuntime.Auth.CurrentUser?.Id)
        {
            StatusMessage = "Cannot deactivate the current user.";
            return;
        }

        var displayName = _selectedUser.DisplayName;
        var username = _selectedUser.Username;
        _userRepository.Deactivate(_selectedUser.Id);
        _audit.Log("USER_DEACTIVATED", $"Deactivated user: {username}", LoginRuntime.Auth.CurrentUser?.Id);
        StatusMessage = $"User '{displayName}' has been deactivated.";

        ReloadUsers();
        Cancel();
    }

    private void RaiseSelectionProperties()
    {
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(ShowEmptySelectionState));
        OnPropertyChanged(nameof(IsNewUserDraft));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(CanDeactivateVisibility));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSubtitle));
        OnPropertyChanged(nameof(AccessLevelLabel));
        OnPropertyChanged(nameof(IsUnlockRequired));
        OnPropertyChanged(nameof(IsUnlockRequiredVisibility));
        OnPropertyChanged(nameof(EditModeLabel));
        OnPropertyChanged(nameof(HasSelectedUserVisibility));
        OnPropertyChanged(nameof(NoSelectionVisibility));
        OnPropertyChanged(nameof(ShowEmptySelectionStateVisibility));
        OnPropertyChanged(nameof(AdminPasswordVisibility));
    }
}


