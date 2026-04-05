using System.Collections.ObjectModel;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App;

public sealed class UserManagementViewModel : ViewModelBase
{
    private readonly UserRepository _userRepository;
    private readonly Services.AuditLogService _audit;
    private User? _selectedUser;
    private bool _isNewUser;
    private string _statusMessage = string.Empty;
    private bool _isEditUnlocked;
    private bool _showUnlockPrompt;

    // Edit fields
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

    public UserManagementViewModel(UserRepository userRepository, Services.AuditLogService audit)
    {
        _userRepository = userRepository;
        _audit = audit;
        Users = new ObservableCollection<User>();

        NewUserCommand = new RelayCommand(CreateNewUser);
        SaveUserCommand = new RelayCommand<PasswordBox>(SaveUser);
        CancelCommand = new RelayCommand(Cancel);
        DeactivateUserCommand = new RelayCommand(DeactivateUser);
        UnlockEditCommand = new RelayCommand(UnlockEdit);
        ConfirmUnlockEditCommand = new RelayCommand(ConfirmUnlockEdit);
        DismissUnlockPromptCommand = new RelayCommand(DismissUnlockPrompt);

        LoadUsers();

        AppServices.Auth.LoginStateChanged += (_, _) =>
        {
            SelectedUser = null;
            LoadUsers();
            _isNewUser = false;
            ShowUnlockPrompt = false;
            OnPropertyChanged(nameof(HasSelectedUser));
            OnPropertyChanged(nameof(ShowEmptySelectionState));
            OnPropertyChanged(nameof(IsNewUserDraft));
            OnPropertyChanged(nameof(IsUnlockRequired));
            OnPropertyChanged(nameof(EditModeLabel));
        };
    }

    public ObservableCollection<User> Users { get; }

    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value))
            {
                _isNewUser = false;
                ShowUnlockPrompt = false;
                IsEditUnlocked = false; // Lock when switching users
                LoadUserDetails();
                OnPropertyChanged(nameof(HasSelectedUser));
                OnPropertyChanged(nameof(ShowEmptySelectionState));
                OnPropertyChanged(nameof(IsNewUserDraft));
                OnPropertyChanged(nameof(CanDeactivate));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(EditorSubtitle));
                OnPropertyChanged(nameof(AccessLevelLabel));
                OnPropertyChanged(nameof(IsUnlockRequired));
                OnPropertyChanged(nameof(EditModeLabel));
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

                OnPropertyChanged(nameof(IsUnlockRequired));
                OnPropertyChanged(nameof(EditModeLabel));
            }
        }
    }

    public bool HasSelectedUser => _selectedUser != null || _isNewUser;
    public bool ShowEmptySelectionState => !HasSelectedUser;
    public bool IsNewUserDraft => _isNewUser;
    public bool ShowUnlockPrompt
    {
        get => _showUnlockPrompt;
        set => SetProperty(ref _showUnlockPrompt, value);
    }

    public bool CanDeactivate => _selectedUser != null && !_isNewUser && _selectedUser.Id != AppServices.Auth.CurrentUser?.Id;
    public bool IsUnlockRequired => _selectedUser != null && !_isNewUser && !_isEditUnlocked;
    public int TotalUsers => Users.Count;
    public int AdminUsers => Users.Count(user => user.IsAdmin);
    public int StandardUsers => Users.Count(user => !user.IsAdmin);
    public string EditModeLabel => _isNewUser
        ? "Draft"
        : _selectedUser == null
            ? "Preview"
            : _isEditUnlocked
                ? "Editing Unlocked"
                : "Editing Locked";
    public string EditorTitle => _isNewUser
        ? "Create team member"
        : _selectedUser != null
            ? $"Profile: {EditDisplayName}"
            : "Select a team member";
    public string EditorSubtitle => _isNewUser
        ? "Set identity, sign-in credentials, and permissions for a new staff account."
        : _selectedUser != null
            ? "Review identity, login details, and permissions before saving changes."
            : "Choose a user from the roster to view account details.";
    public string AccessLevelLabel => _isNewUser
        ? (EditIsAdmin ? "NEW ADMIN" : "NEW STAFF")
        : (EditIsAdmin ? "ADMINISTRATOR" : "STANDARD ACCESS");

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

    // Edit properties
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
            if (SetProperty(ref _editIsAdmin, value) && value)
            {
                SetAdminPermissions();
            }

            OnPropertyChanged(nameof(AccessLevelLabel));
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

    // Commands
    public RelayCommand NewUserCommand { get; }
    public RelayCommand<PasswordBox> SaveUserCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeactivateUserCommand { get; }
    public RelayCommand UnlockEditCommand { get; }
    public RelayCommand ConfirmUnlockEditCommand { get; }
    public RelayCommand DismissUnlockPromptCommand { get; }

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
        if (AppServices.Auth.CanManageUsers)
        {
            return true;
        }

        StatusMessage = "You do not have permission to manage users.";
        return false;
    }

    private void LoadUsers()
    {
        Users.Clear();
        foreach (var user in _userRepository.GetActive())
        {
            Users.Add(user);
        }

        OnPropertyChanged(nameof(TotalUsers));
        OnPropertyChanged(nameof(AdminUsers));
        OnPropertyChanged(nameof(StandardUsers));
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
        EditPin = string.Empty; // Don't show existing PIN
        if (_selectedUser.IsAdmin)
        {
            SetAdminPermissions();
        }
        else
        {
            EditCanCheckout = _selectedUser.CanCheckout;
            EditCanManageProducts = _selectedUser.CanManageProducts;
            EditCanManageSettings = _selectedUser.CanManageSettings;
            EditCanManageUsers = _selectedUser.IsAdmin;
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


    private void CreateNewUser()
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        _selectedUser = null;
        _isNewUser = true;
        ShowUnlockPrompt = false;
        IsEditUnlocked = true; // Auto-unlock for brand new users
        ClearEditFields();
        OnPropertyChanged(nameof(SelectedUser));
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(ShowEmptySelectionState));
        OnPropertyChanged(nameof(IsNewUserDraft));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSubtitle));
        OnPropertyChanged(nameof(AccessLevelLabel));
        OnPropertyChanged(nameof(IsUnlockRequired));
        OnPropertyChanged(nameof(EditModeLabel));
    }

    private void SaveUser(PasswordBox? passwordBox)
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        EditDisplayName = EditDisplayName.Trim();
        EditUsername = EditUsername.Trim();

        // Validation
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
            // Only validate PIN format - no uniqueness check
            // SECURITY: Uniqueness is enforced at LOGIN time to prevent info disclosure
            // If we reject a PIN here, attacker learns that PIN exists in the database
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
                // Create new user
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

                // Set password for admin
                if (EditIsAdmin && passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                {
                    user.PasswordHash = UserRepository.HashPassword(passwordBox.Password);
                }

                // Set PIN
                if (!string.IsNullOrEmpty(EditPin))
                {
                    user.PinHash = UserRepository.HashPin(EditPin);
                }

                var newId = _userRepository.Create(user);
                userToReselectId = newId;
                _audit.Log("USER_CREATED", $"Created user: {user.Username}, Role: {(user.IsAdmin ? "Admin" : "Cashier")}", AppServices.Auth.CurrentUser?.Id);
                StatusMessage = $"User '{EditDisplayName}' created successfully.";
                _isNewUser = false;
            }
            else if (_selectedUser != null)
            {
                // Update existing user
                _selectedUser.DisplayName = EditDisplayName;
                _selectedUser.Username = EditUsername;
                _selectedUser.IsAdmin = EditIsAdmin;
                _selectedUser.CanCheckout = EditCanCheckout;
                _selectedUser.CanManageProducts = EditCanManageProducts;
                _selectedUser.CanManageSettings = EditCanManageSettings;
                _selectedUser.CanManageUsers = EditIsAdmin;
                _selectedUser.CanViewReports = EditCanViewReports;
                _selectedUser.CanOverridePrice = EditCanOverridePrice;

                // Update password if provided
                if (EditIsAdmin && passwordBox != null && !string.IsNullOrEmpty(passwordBox.Password))
                {
                    _selectedUser.PasswordHash = UserRepository.HashPassword(passwordBox.Password);
                }

                // Update PIN if provided
                if (!string.IsNullOrEmpty(EditPin))
                {
                    _selectedUser.PinHash = UserRepository.HashPin(EditPin);
                }

                _userRepository.Update(_selectedUser);
                _audit.Log("USER_UPDATED", $"Updated user: {_selectedUser.Username}", AppServices.Auth.CurrentUser?.Id);
                StatusMessage = $"User '{EditDisplayName}' updated successfully.";
            }

            var currentUserId = AppServices.Auth.CurrentUser?.Id;
            if (userToReselectId.HasValue && currentUserId == userToReselectId.Value)
            {
                AppServices.Auth.RefreshCurrentUser();
            }

            IsEditUnlocked = false; // Re-lock after saving
            LoadUsers();
            if (userToReselectId.HasValue)
            {
                SelectedUser = Users.FirstOrDefault(user => user.Id == userToReselectId.Value);
            }
            else
            {
                OnPropertyChanged(nameof(HasSelectedUser));
                OnPropertyChanged(nameof(ShowEmptySelectionState));
                OnPropertyChanged(nameof(IsNewUserDraft));
                OnPropertyChanged(nameof(EditorTitle));
                OnPropertyChanged(nameof(EditorSubtitle));
                OnPropertyChanged(nameof(AccessLevelLabel));
                OnPropertyChanged(nameof(IsUnlockRequired));
                OnPropertyChanged(nameof(EditModeLabel));
            }
            passwordBox?.Clear();
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Username is already in use.", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = $"Username '{EditUsername}' is already in use.";
            AppServices.ReportException(ex, "UserManagementViewModel.Save.DuplicateUsername");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            StatusMessage = $"Username '{EditUsername}' is already in use.";
            AppServices.ReportException(ex, "UserManagementViewModel.Save.SqliteConstraint");
        }
        catch (Exception ex)
        {
            StatusMessage = "Unable to save user. Please review the fields and try again.";
            AppServices.ReportException(ex, "UserManagementViewModel.Save");
        }
    }

    private void Cancel()
    {
        _isNewUser = false;
        _selectedUser = null;
        ShowUnlockPrompt = false;
        IsEditUnlocked = false;
        ClearEditFields();
        OnPropertyChanged(nameof(SelectedUser));
        OnPropertyChanged(nameof(HasSelectedUser));
        OnPropertyChanged(nameof(ShowEmptySelectionState));
        OnPropertyChanged(nameof(IsNewUserDraft));
        OnPropertyChanged(nameof(CanDeactivate));
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSubtitle));
        OnPropertyChanged(nameof(AccessLevelLabel));
        OnPropertyChanged(nameof(IsUnlockRequired));
        OnPropertyChanged(nameof(EditModeLabel));
    }

    private void DeactivateUser()
    {
        if (!EnsureCanManageUsers())
        {
            return;
        }

        if (_selectedUser == null || _selectedUser.Id == AppServices.Auth.CurrentUser?.Id)
        {
            StatusMessage = "Cannot deactivate the current user.";
            return;
        }

        var displayName = _selectedUser.DisplayName;
        var username = _selectedUser.Username;
        _userRepository.Deactivate(_selectedUser.Id);
        _audit.Log("USER_DEACTIVATED", $"Deactivated user: {username}", AppServices.Auth.CurrentUser?.Id);
        StatusMessage = $"User '{displayName}' has been deactivated.";

        LoadUsers();
        Cancel();
    }
}
