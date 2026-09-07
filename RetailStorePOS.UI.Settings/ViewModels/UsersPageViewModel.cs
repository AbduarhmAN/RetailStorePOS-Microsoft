using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RetailStorePOS.UI.Common;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Modules.UsersAuth;
using RetailStorePOS.UI.Common;
using RetailStorePOS.UI.Common.Services;

namespace RetailStorePOS.UI.Settings.ViewModels;

public sealed partial class UsersPageViewModel : ObservableObject, IDisposable
{
    public LocalizationService Loc => LocalizationService.Instance;

    private readonly UserRepository _userRepository;
    private readonly AuditLogService _audit;
    private readonly EventHandler _loginStateChangedHandler;
    private readonly PropertyChangedEventHandler _localizationChangedHandler;
    private readonly DispatcherQueue _uiDispatcher;
    private User? _selectedUser;
    private bool _isNewUser;
    private string _statusMessage = string.Empty;
    private bool _isEditUnlocked;
    private bool _showUnlockPrompt;
    private bool _isDisposed;
    private int _reloadUsersVersion;
    private string _adminPasswordValidationMessage = string.Empty;
    private string _pinValidationMessage = string.Empty;

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
        _uiDispatcher = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("UsersPageViewModel requires a UI dispatcher.");
        _userRepository = LoginRuntime.Users;
        _audit = LoginRuntime.Audit;
        _loginStateChangedHandler = OnLoginStateChanged;
        _localizationChangedHandler = OnLocalizationChanged;

        Users = new ObservableCollection<User>();

        NewUserCommand = new RelayCommand(CreateNewUser);
        SaveUserCommand = new RelayCommand<PasswordBox>(SaveUser);
        CancelCommand = new RelayCommand(Cancel);
        DeactivateUserCommand = new RelayCommand(DeactivateUser);
        UnlockEditCommand = new RelayCommand(UnlockEdit);
        ConfirmUnlockEditCommand = new RelayCommand(ConfirmUnlockEdit);
        DismissUnlockPromptCommand = new RelayCommand(DismissUnlockPrompt);

        LoginRuntime.Auth.LoginStateChanged += _loginStateChangedHandler;
        Loc.PropertyChanged += _localizationChangedHandler;
        ReloadUsers();
    }

    private void RefreshLocalizedProperties()
    {
        OnPropertyChanged(nameof(Users_List_Title));
        OnPropertyChanged(nameof(Users_List_Subtitle));
        OnPropertyChanged(nameof(Users_Editor_NewButton));
        OnPropertyChanged(nameof(Users_Editor_UnlockButton));
        OnPropertyChanged(nameof(Users_Editor_ReadOnlyBanner));
        OnPropertyChanged(nameof(Users_Editor_SecurityWarning_Title));
        OnPropertyChanged(nameof(Users_Editor_SecurityWarning_Message));
        OnPropertyChanged(nameof(Users_Section_Identity));
        OnPropertyChanged(nameof(Users_Section_Identity_Subtitle));
        OnPropertyChanged(nameof(Users_Label_DisplayName));
        OnPropertyChanged(nameof(Users_Label_Username));
        OnPropertyChanged(nameof(Users_Section_Security));
        OnPropertyChanged(nameof(Users_Section_Security_Subtitle));
        OnPropertyChanged(nameof(Users_Label_PIN));
        OnPropertyChanged(nameof(Users_Label_AdminPassword));
        OnPropertyChanged(nameof(Users_Label_AdminPasswordHint));
        OnPropertyChanged(nameof(Users_Section_Roles));
        OnPropertyChanged(nameof(Users_Section_Roles_Subtitle));
        OnPropertyChanged(nameof(Users_Role_Admin));
        OnPropertyChanged(nameof(Users_Role_Admin_Description));
        OnPropertyChanged(nameof(Users_Role_Checkout));
        OnPropertyChanged(nameof(Users_Role_Checkout_Description));
        OnPropertyChanged(nameof(Users_Role_Override));
        OnPropertyChanged(nameof(Users_Role_Override_Description));
        OnPropertyChanged(nameof(Users_Role_Reports));
        OnPropertyChanged(nameof(Users_Role_Reports_Description));
        OnPropertyChanged(nameof(Users_Role_Products));
        OnPropertyChanged(nameof(Users_Role_Products_Description));
        OnPropertyChanged(nameof(Users_Role_Settings));
        OnPropertyChanged(nameof(Users_Role_Settings_Description));
        OnPropertyChanged(nameof(Users_Button_Deactivate));
        OnPropertyChanged(nameof(Users_Button_Deactivate_Tip));
        OnPropertyChanged(nameof(Users_Button_Cancel));
        OnPropertyChanged(nameof(Users_Button_Save));
        OnPropertyChanged(nameof(AdminPasswordValidationMessage));
        OnPropertyChanged(nameof(PinValidationMessage));
        OnPropertyChanged(nameof(Users_Empty_Title));
        OnPropertyChanged(nameof(Users_Empty_Subtitle));
        OnPropertyChanged(nameof(Users_Unlock_Title));
        OnPropertyChanged(nameof(Users_Unlock_Subtitle));
        OnPropertyChanged(nameof(Users_Unlock_Reason_Title));
        OnPropertyChanged(nameof(Users_Unlock_Reason_Text));
        OnPropertyChanged(nameof(Users_Unlock_Button_Cancel));
        OnPropertyChanged(nameof(Users_Unlock_Button_Confirm));
        OnPropertyChanged(nameof(Users_Walkthrough_Admin_Title));
        OnPropertyChanged(nameof(Users_Walkthrough_Admin_Subtitle));
        OnPropertyChanged(nameof(Users_Walkthrough_New_Title));
        OnPropertyChanged(nameof(Users_Walkthrough_New_Subtitle));
        OnPropertyChanged(nameof(Users_Walkthrough_Unlock_Title));
        OnPropertyChanged(nameof(Users_Walkthrough_Unlock_Subtitle));
        OnPropertyChanged(nameof(Users_Walkthrough_Save_Title));
        OnPropertyChanged(nameof(Users_Walkthrough_Save_Subtitle));
        OnPropertyChanged(nameof(Generic_Close));
        OnPropertyChanged(nameof(Generic_Next));
        OnPropertyChanged(nameof(Generic_Done));


        // Computed properties that use localized strings
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(EditorSubtitle));
        OnPropertyChanged(nameof(AccessLevelLabel));
        OnPropertyChanged(nameof(EditModeLabel));

        // Refresh the list to update localized strings from converters
        ReloadUsers();
    }

    public ObservableCollection<User> Users { get; }

    public bool CanManageUsers => LoginRuntime.Auth.CurrentUser?.IsAdmin == true;
    public bool IsOnboardingActive =>
        LoginRuntime.Auth.CurrentUser?.Username == "admin" &&
        (!LoginRuntime.IsOnboardingPhaseCleared || LoginRuntime.Auth.CurrentUser?.MustChangePassword == true);
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
                ClearCredentialValidation();

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
        ? LocalizationHelper.GetString("Users_Mode_Draft")
        : _selectedUser == null
            ? LocalizationHelper.GetString("Users_Mode_Preview")
            : IsEditUnlocked
                ? LocalizationHelper.GetString("Users_Mode_Unlocked")
                : LocalizationHelper.GetString("Users_Mode_Locked");

    public string EditorTitle => _isNewUser
        ? LocalizationHelper.GetString("Users_Editor_Title_Create")
        : _selectedUser != null
            ? LocalizationHelper.Format("Users_Editor_Title_Profile", EditDisplayName)
            : LocalizationHelper.GetString("Users_Empty_Title.Text");

    public string EditorSubtitle => _isNewUser
        ? LocalizationHelper.GetString("Users_Editor_Subtitle_Create")
        : _selectedUser != null
            ? LocalizationHelper.GetString("Users_Editor_Subtitle_Review")
            : LocalizationHelper.GetString("Users_Empty_Subtitle.Text");

    public string AccessLevelLabel => _isNewUser
        ? (EditIsAdmin ? LocalizationHelper.GetString("Users_Role_NewAdmin") : LocalizationHelper.GetString("Users_Role_NewStaff"))
        : (EditIsAdmin ? LocalizationHelper.GetString("Users_Role_AdminLabel") : LocalizationHelper.GetString("Users_Role_CashierLabel"));

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

    // Reactive Localization Properties
    public string Users_List_Title => Loc["Users_List_Title.Text"];
    public string Users_List_Subtitle => Loc["Users_List_Subtitle.Text"];
    public string Users_Editor_NewButton => Loc["Users_Editor_NewButton.Label"];
    public string Users_Editor_UnlockButton => Loc["Users_Editor_UnlockButton.Label"];
    public string Users_Editor_ReadOnlyBanner => Loc["Users_Editor_ReadOnlyBanner.Text"];
    public string Users_Editor_SecurityWarning_Title => Loc["Users_Editor_SecurityWarning.Title"];
    public string Users_Editor_SecurityWarning_Message => Loc["Users_Editor_SecurityWarning.Message"];
    public string Users_Section_Identity => Loc["Users_Section_Identity.Text"];
    public string Users_Section_Identity_Subtitle => Loc["Users_Section_Identity_Subtitle.Text"];
    public string Users_Label_DisplayName => Loc["Users_Label_DisplayName.Text"];
    public string Users_Label_Username => Loc["Users_Label_Username.Text"];
    public string Users_Section_Security => Loc["Users_Section_Security.Text"];
    public string Users_Section_Security_Subtitle => Loc["Users_Section_Security_Subtitle.Text"];
    public string Users_Label_PIN => Loc["Users_Label_PIN.Text"];
    public string Users_Label_AdminPassword => Loc["Users_Label_AdminPassword.Text"];
    public string Users_Label_AdminPasswordHint => Loc["Users_Label_AdminPasswordHint.Text"];
    public string Users_Section_Roles => Loc["Users_Section_Roles.Text"];
    public string Users_Section_Roles_Subtitle => Loc["Users_Section_Roles_Subtitle.Text"];
    public string Users_Role_Admin => Loc["Users_Role_Admin.Text"];
    public string Users_Role_Admin_Description => Loc["Users_Role_Admin_Description.Text"];
    public string Users_Role_Checkout => Loc["Users_Role_Checkout.Text"];
    public string Users_Role_Checkout_Description => Loc["Users_Role_Checkout_Description.Text"];
    public string Users_Role_Override => Loc["Users_Role_Override.Text"];
    public string Users_Role_Override_Description => Loc["Users_Role_Override_Description.Text"];
    public string Users_Role_Reports => Loc["Users_Role_Reports.Text"];
    public string Users_Role_Reports_Description => Loc["Users_Role_Reports_Description.Text"];
    public string Users_Role_Products => Loc["Users_Role_Products.Text"];
    public string Users_Role_Products_Description => Loc["Users_Role_Products_Description.Text"];
    public string Users_Role_Settings => Loc["Users_Role_Settings.Text"];
    public string Users_Role_Settings_Description => Loc["Users_Role_Settings_Description.Text"];
    public string Users_Button_Deactivate => Loc["Users_Button_Deactivate.Text"];
    public string Users_Button_Deactivate_Tip => Loc["Users_Button_Deactivate_Tip.Text"];
    public string Users_Button_Cancel => Loc["Users_Button_Cancel.Text"];
    public string Users_Button_Save => Loc["Users_Button_Save.Text"];
    public string Users_Empty_Title => Loc["Users_Empty_Title.Text"];
    public string Users_Empty_Subtitle => Loc["Users_Empty_Subtitle.Text"];
    public string Users_Unlock_Title => Loc["Users_Unlock_Title.Text"];
    public string Users_Unlock_Subtitle => Loc["Users_Unlock_Subtitle.Text"];
    public string Users_Unlock_Reason_Title => Loc["Users_Unlock_Reason_Title.Text"];
    public string Users_Unlock_Reason_Text => Loc["Users_Unlock_Reason_Text.Text"];
    public string Users_Unlock_Button_Cancel => Loc["Users_Unlock_Button_Cancel.Text"];
    public string Users_Unlock_Button_Confirm => Loc["Users_Unlock_Button_Confirm.Text"];
    public string Users_Walkthrough_Admin_Title => Loc["Users_Walkthrough_Admin.Title"];
    public string Users_Walkthrough_Admin_Subtitle => Loc["Users_Walkthrough_Admin.Subtitle"];
    public string Users_Walkthrough_New_Title => Loc["Users_Walkthrough_New.Title"];
    public string Users_Walkthrough_New_Subtitle => Loc["Users_Walkthrough_New.Subtitle"];
    public string Users_Walkthrough_Unlock_Title => Loc["Users_Walkthrough_Unlock.Title"];
    public string Users_Walkthrough_Unlock_Subtitle => Loc["Users_Walkthrough_Unlock.Subtitle"];
    public string Users_Walkthrough_Save_Title => Loc["Users_Walkthrough_Save.Title"];
    public string Users_Walkthrough_Save_Subtitle => Loc["Users_Walkthrough_Save_Subtitle"];
    public string Generic_Close => Loc["Generic_Close"];
    public string Generic_Next => Loc["Generic_Next"];
    public string Generic_Done => Loc["Generic_Done"];

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
        set
        {
            if (SetProperty(ref _editPin, value))
            {
                ClearPinValidation();
            }
        }
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

    public string AdminPasswordValidationMessage
    {
        get => _adminPasswordValidationMessage;
        private set
        {
            if (SetProperty(ref _adminPasswordValidationMessage, value))
            {
                OnPropertyChanged(nameof(AdminPasswordValidationVisibility));
            }
        }
    }

    public string PinValidationMessage
    {
        get => _pinValidationMessage;
        private set
        {
            if (SetProperty(ref _pinValidationMessage, value))
            {
                OnPropertyChanged(nameof(PinValidationVisibility));
            }
        }
    }

    public Visibility AdminPasswordValidationVisibility =>
        string.IsNullOrWhiteSpace(AdminPasswordValidationMessage) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility PinValidationVisibility =>
        string.IsNullOrWhiteSpace(PinValidationMessage) ? Visibility.Collapsed : Visibility.Visible;

    public RelayCommand NewUserCommand { get; }
    public RelayCommand<PasswordBox> SaveUserCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand DeactivateUserCommand { get; }
    public RelayCommand UnlockEditCommand { get; }
    public RelayCommand ConfirmUnlockEditCommand { get; }
    public RelayCommand DismissUnlockPromptCommand { get; }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Interlocked.Increment(ref _reloadUsersVersion);
        LoginRuntime.Auth.LoginStateChanged -= _loginStateChangedHandler;
        Loc.PropertyChanged -= _localizationChangedHandler;
    }

    public void ClearAdminPasswordValidation()
    {
        AdminPasswordValidationMessage = string.Empty;
    }

    public void ClearPinValidation()
    {
        PinValidationMessage = string.Empty;
    }

    private void ClearCredentialValidation()
    {
        ClearAdminPasswordValidation();
        ClearPinValidation();
    }

    private void OnLoginStateChanged(object? sender, EventArgs e)
    {
        SelectedUser = null;
        _isNewUser = false;
        ShowUnlockPrompt = false;
        IsEditUnlocked = false;
        ClearCredentialValidation();
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
        StatusMessage = LocalizationHelper.Format("Users_Status_Unlocked", EditDisplayName);
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

        StatusMessage = LocalizationHelper.GetString("Users_Status_AdminOnly");
        return false;
    }

    private void ReloadUsers(long? reselectUserId = null)
        => _ = ReloadUsersAsync(reselectUserId);

    private async Task ReloadUsersAsync(long? reselectUserId = null)
    {
        var operationVersion = Interlocked.Increment(ref _reloadUsersVersion);
        var targetId = reselectUserId ?? _selectedUser?.Id;
        try
        {
            var users = await Task.Run(() => _userRepository.GetActive().ToList());
            if (IsReloadUsersStale(operationVersion))
            {
                return;
            }

            await RunOnUiThreadAsync(() =>
            {
                if (IsReloadUsersStale(operationVersion))
                {
                    return;
                }

                Users.Clear();
                foreach (var user in users)
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
            });
        }
        catch (Exception ex)
        {
            if (IsReloadUsersStale(operationVersion))
            {
                return;
            }

            LoginRuntime.ReportException(ex, "UsersPageViewModel.ReloadUsers");
        }
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
        ClearCredentialValidation();

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
        ClearCredentialValidation();
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
        ClearCredentialValidation();

        if (string.IsNullOrWhiteSpace(EditDisplayName))
        {
            StatusMessage = LocalizationHelper.GetString("Users_Status_DisplayNameRequired");
            return;
        }

        if (string.IsNullOrWhiteSpace(EditUsername))
        {
            StatusMessage = LocalizationHelper.GetString("Users_Status_UsernameRequired");
            return;
        }

        var enteredPassword = passwordBox?.Password ?? string.Empty;

        if (ValidateBootstrapAdminCredentialChange(enteredPassword))
        {
            StatusMessage = string.Empty;
            return;
        }

        if (_isNewUser)
        {
            if (EditIsAdmin)
            {
                if (string.IsNullOrEmpty(enteredPassword) && string.IsNullOrEmpty(EditPin))
                {
                    StatusMessage = LocalizationHelper.GetString("Users_Status_AdminCredentialsRequired");
                    return;
                }
            }
            else if (string.IsNullOrEmpty(EditPin))
            {
                StatusMessage = LocalizationHelper.GetString("Users_Status_PinRequired");
                return;
            }
        }

        if (_selectedUser != null && EditIsAdmin)
        {
            var hasStoredPassword = !string.IsNullOrEmpty(_selectedUser.PasswordHash);
            var hasStoredPin = !string.IsNullOrEmpty(_selectedUser.PinHash);
            if (!hasStoredPassword && !hasStoredPin && string.IsNullOrEmpty(enteredPassword) && string.IsNullOrEmpty(EditPin))
            {
                StatusMessage = LocalizationHelper.GetString("Users_Status_AdminCredentialsRequired");
                return;
            }
        }

        if (!string.IsNullOrEmpty(EditPin))
        {
            if (EditPin.Length != 4 || !EditPin.All(char.IsDigit))
            {
                StatusMessage = LocalizationHelper.GetString("Users_Status_PinInvalid");
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
            StatusMessage = LocalizationHelper.Format("Users_Status_UsernameInUse", EditUsername);
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
                StatusMessage = LocalizationHelper.Format("Users_Status_Created", EditDisplayName);
                _isNewUser = false;
            }
            else if (_selectedUser != null)
            {
                var completedBootstrapPasswordChange = IsBootstrapAdminCredentialChangeCompleted(
                    _selectedUser,
                    enteredPassword,
                    EditPin);

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

                if (completedBootstrapPasswordChange)
                {
                    _selectedUser.MustChangePassword = false;
                }

                _userRepository.Update(_selectedUser);
                _audit.Log("USER_UPDATED", $"Updated user: {_selectedUser.Username}", LoginRuntime.Auth.CurrentUser?.Id);
                StatusMessage = LocalizationHelper.Format("Users_Status_Updated", EditDisplayName);

                if (completedBootstrapPasswordChange)
                {
                    LoginRuntime.CompleteBootstrapAdminPasswordChange();
                }
            }

            var currentUserId = LoginRuntime.Auth.CurrentUser?.Id;
            if (userToReselectId.HasValue && currentUserId == userToReselectId.Value)
            {
                LoginRuntime.Auth.RefreshCurrentUser();
            }

            IsEditUnlocked = false;
            ReloadUsers(userToReselectId);

            if (passwordBox != null)
            {
                passwordBox.Password = string.Empty;
            }

            OnPropertyChanged(nameof(IsOnboardingActive));
        }
        catch (InvalidOperationException ex) when (string.Equals(ex.Message, "Username is already in use.", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = LocalizationHelper.Format("Users_Status_UsernameInUse", EditUsername);
            LoginRuntime.ReportException(ex, "UsersPageViewModel.Save.DuplicateUsername");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            StatusMessage = LocalizationHelper.Format("Users_Status_UsernameInUse", EditUsername);
            LoginRuntime.ReportException(ex, "UsersPageViewModel.Save.SqliteConstraint");
        }
        catch (Exception ex)
        {
            StatusMessage = LocalizationHelper.GetString("Users_Status_SaveError");
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
            StatusMessage = LocalizationHelper.GetString("Users_Status_CannotDeactivateSelf");
            return;
        }

        var displayName = _selectedUser.DisplayName;
        var username = _selectedUser.Username;
        _userRepository.Deactivate(_selectedUser.Id);
        _audit.Log("USER_DEACTIVATED", $"Deactivated user: {username}", LoginRuntime.Auth.CurrentUser?.Id);
        StatusMessage = LocalizationHelper.Format("Users_Status_Deactivated", displayName);

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
        OnPropertyChanged(nameof(AdminPasswordValidationVisibility));
        OnPropertyChanged(nameof(PinValidationVisibility));
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshLocalizedProperties();
    }

    private bool IsReloadUsersStale(int operationVersion)
        => _isDisposed || operationVersion != Volatile.Read(ref _reloadUsersVersion);

    private Task RunOnUiThreadAsync(Action action)
    {
        if (_uiDispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_uiDispatcher.TryEnqueue(() =>
            {
                try
                {
                    action();
                    tcs.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
        {
            tcs.TrySetCanceled();
        }

        return tcs.Task;
    }

    private bool ValidateBootstrapAdminCredentialChange(string enteredPassword)
    {
        if (!IsBootstrapAdminSetupUser(_selectedUser))
        {
            return false;
        }

        var temporaryCredentials = LoginRuntime.GetBootstrapAdminHintForDisplay();

        if (string.IsNullOrEmpty(enteredPassword))
        {
            AdminPasswordValidationMessage = LocalizationHelper.GetString("Users_Validation_ChangeTemporaryPassword");
        }
        else if (temporaryCredentials is not null &&
                 string.Equals(enteredPassword, temporaryCredentials.Password, StringComparison.Ordinal))
        {
            AdminPasswordValidationMessage = LocalizationHelper.GetString("Users_Validation_ChooseNewPassword");
        }
        else if (temporaryCredentials is null &&
                 UserRepository.VerifyPassword(enteredPassword, _selectedUser?.PasswordHash))
        {
            AdminPasswordValidationMessage = LocalizationHelper.GetString("Users_Validation_ChooseNewPassword");
        }

        if (string.IsNullOrEmpty(EditPin))
        {
            PinValidationMessage = LocalizationHelper.GetString("Users_Validation_ChangeTemporaryPin");
        }
        else if (EditPin.Length != 4 || !EditPin.All(char.IsDigit))
        {
            PinValidationMessage = LocalizationHelper.GetString("Users_Status_PinInvalid");
        }
        else if (temporaryCredentials is not null &&
                 string.Equals(EditPin, temporaryCredentials.Pin, StringComparison.Ordinal))
        {
            PinValidationMessage = LocalizationHelper.GetString("Users_Validation_ChooseNewPin");
        }
        else if (temporaryCredentials is null &&
                 UserRepository.VerifyPin(EditPin, _selectedUser?.PinHash))
        {
            PinValidationMessage = LocalizationHelper.GetString("Users_Validation_ChooseNewPin");
        }

        return AdminPasswordValidationVisibility == Visibility.Visible ||
               PinValidationVisibility == Visibility.Visible;
    }

    private static bool IsBootstrapAdminCredentialChangeCompleted(User selectedUser, string enteredPassword, string enteredPin)
    {
        if (!IsBootstrapAdminSetupUser(selectedUser) ||
            string.IsNullOrEmpty(enteredPassword) ||
            string.IsNullOrEmpty(enteredPin) ||
            enteredPin.Length != 4 ||
            !enteredPin.All(char.IsDigit))
        {
            return false;
        }

        var tempCredentials = LoginRuntime.GetBootstrapAdminHintForDisplay();
        if (tempCredentials is not null)
        {
            return !string.Equals(enteredPassword, tempCredentials.Password, StringComparison.Ordinal) &&
                   !string.Equals(enteredPin, tempCredentials.Pin, StringComparison.Ordinal);
        }

        return !UserRepository.VerifyPassword(enteredPassword, selectedUser.PasswordHash) &&
               !UserRepository.VerifyPin(enteredPin, selectedUser.PinHash);
    }

    private static bool IsBootstrapAdminSetupUser(User? selectedUser)
        => selectedUser is not null &&
           selectedUser.MustChangePassword &&
           string.Equals(selectedUser.Username, "admin", StringComparison.OrdinalIgnoreCase);
}


