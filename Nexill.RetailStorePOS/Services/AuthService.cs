using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Modules.UsersAuth;

namespace RetailStorePOS.App.Services;

public sealed class AuthService
{
    private static readonly TimeSpan FailedAuthenticationResetWindow = TimeSpan.FromHours(24);
    private readonly UserRepository _userRepository;
    private readonly AuditLogService _audit;
    private bool _needsSetup;
    private User? _currentUser;
    private Session? _currentSession;
    private bool _isLocked;

    public AuthService(UserRepository userRepository, AuditLogService audit)
    {
        _userRepository = userRepository;
        _audit = audit;
        _needsSetup = !_userRepository.UsersExist();
    }

    public User? CurrentUser => _currentUser;
    public bool IsLocked => _isLocked;
    public bool IsLoggedIn => _currentUser != null && !_isLocked;
    public bool NeedsSetup => _needsSetup;

    public event EventHandler? LoginStateChanged;

    public async Task<LoginResult> LoginWithPasswordAsync(string username, string password)
    {
        var result = await Task.Run(() => LoginWithPasswordCore(username, password, notifyStateChanged: false));
        if (result.Success)
        {
            NotifyLoginStateChanged();
        }

        return result;
    }

    public async Task<LoginResult> LoginWithPinAsync(string username, string pin)
    {
        var result = await Task.Run(() => LoginWithPinCore(username, pin, notifyStateChanged: false));
        if (result.Success)
        {
            NotifyLoginStateChanged();
        }

        return result;
    }

    public Task<IReadOnlyList<User>> GetActiveStaffAsync()
    {
        return Task.Run(() => GetActiveStaff());
    }

    public LoginResult LoginWithPassword(string username, string password)
    {
        return LoginWithPasswordCore(username, password, notifyStateChanged: true);
    }

    public LoginResult LoginWithPin(string username, string pin)
    {
        return LoginWithPinCore(username, pin, notifyStateChanged: true);
    }

    private LoginResult LoginWithPasswordCore(string username, string password, bool notifyStateChanged)
    {
        username = username.Trim();
        var lockedSessionValidation = ValidateLockedSessionUser(username);
        if (lockedSessionValidation is not null)
        {
            return lockedSessionValidation;
        }

        var user = _userRepository.GetByUsername(username);
        if (user == null)
        {
            _audit.Log("LOGIN_FAILED", $"Username: {username}, Method: Password, Reason: User not found");
            return new LoginResult(false, "User not found.");
        }

        var lockoutValidation = ValidateAuthenticationLockout(user, "Password", "LOGIN_BLOCKED_LOCKOUT");
        if (lockoutValidation is not null)
        {
            return lockoutValidation;
        }

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (!UserRepository.VerifyPassword(password, user.PasswordHash))
            {
                return HandleFailedAuthentication(
                    user,
                    "Password",
                    "LOGIN_FAILED",
                    "Invalid password.",
                    "Invalid password.");
            }

            TryUpgradeCredentialHash(user, password, CredentialKind.Password, user.PasswordHash);
        }
        else
        {
            // No password is configured. The previous build accepted the PIN
            // here as a "recovery" fallback, which silently lowered admin
            // assurance to the 4-digit PIN keyspace. Refuse the login: the
            // operator must use the dedicated PIN flow if they only have a PIN.
            _audit.Log(
                "LOGIN_FAILED",
                $"User: {username}, Method: Password, Reason: No password configured (use PIN flow)");
            return new LoginResult(false, "This account has no password. Sign in with PIN instead.");
        }

        ClearAuthenticationThrottleState(user, "LOGIN_THROTTLE_RESET_FAILED", "Password");
        SetCurrentUser(user, notifyStateChanged);
        _audit.Log("LOGIN_SUCCESS", $"User: {username}, Method: Password", user.Id);
        return new LoginResult(true, null);
    }

    private LoginResult LoginWithPinCore(string username, string pin, bool notifyStateChanged)
    {
        username = username.Trim();
        var lockedSessionValidation = ValidateLockedSessionUser(username);
        if (lockedSessionValidation is not null)
        {
            return lockedSessionValidation;
        }

        if (string.IsNullOrEmpty(username))
        {
            return new LoginResult(false, "Username is required.");
        }

        if (string.IsNullOrEmpty(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
        {
            return new LoginResult(false, "PIN must be exactly 4 digits.");
        }

        var user = _userRepository.GetByUsername(username);
        if (user == null)
        {
            _audit.Log("LOGIN_FAILED", $"Username: {username}, Method: PIN, Reason: User not found");
            return new LoginResult(false, "Invalid credentials.");
        }

        var lockoutValidation = ValidateAuthenticationLockout(user, "PIN", "LOGIN_BLOCKED_LOCKOUT");
        if (lockoutValidation is not null)
        {
            return lockoutValidation;
        }

        if (!UserRepository.VerifyPin(pin, user.PinHash))
        {
            return HandleFailedAuthentication(
                user,
                "PIN",
                "LOGIN_FAILED",
                "Invalid PIN.",
                "Invalid credentials.");
        }

        TryUpgradeCredentialHash(user, pin, CredentialKind.Pin, user.PinHash);
        ClearAuthenticationThrottleState(user, "LOGIN_THROTTLE_RESET_FAILED", "PIN");
        SetCurrentUser(user, notifyStateChanged);
        _audit.Log("LOGIN_SUCCESS", $"User: {username}, Method: PIN", user.Id);
        return new LoginResult(true, null);
    }

    public void Logout()
    {
        if (_currentUser != null)
        {
            _audit.Log("LOGOUT", $"User: {_currentUser.Username}", _currentUser.Id);
        }
        _isLocked = false;
        _currentUser = null;
        _currentSession = null;
        NotifyLoginStateChanged();
    }

    public void RefreshCurrentUser()
    {
        if (_currentUser == null)
        {
            return;
        }

        var refreshed = _userRepository.GetById(_currentUser.Id);
        if (refreshed == null)
        {
            _isLocked = false;
            _currentUser = null;
            _currentSession = null;
            NotifyLoginStateChanged();
            return;
        }

        _currentUser = refreshed;
        if (_currentSession != null)
        {
            _currentSession.User = refreshed;
            if (!_isLocked)
            {
                _currentSession.LastActivity = DateTime.UtcNow;
            }
        }

        NotifyLoginStateChanged();
    }

    public void Lock()
    {
        if (_currentUser == null || _currentSession == null || _isLocked)
        {
            return;
        }

        _isLocked = true;
        if (_currentUser != null)
        {
            _audit.Log("SESSION_LOCKED", $"User: {_currentUser.Username}", _currentUser.Id);
        }

        NotifyLoginStateChanged();
    }

    public LoginResult UnlockWithPin(string pin, long expectedUserId)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length != 4 || !pin.All(char.IsDigit))
        {
            return new LoginResult(false, "PIN must be exactly 4 digits.");
        }

        var user = _userRepository.GetById(expectedUserId);

        if (user == null)
        {
            _audit.Log("UNLOCK_FAILED", $"TargetUser: {expectedUserId}, Reason: User not found");
            return new LoginResult(false, "Invalid PIN.");
        }

        var lockoutValidation = ValidateAuthenticationLockout(user, "Unlock PIN", "UNLOCK_BLOCKED_LOCKOUT");
        if (lockoutValidation is not null)
        {
            return lockoutValidation;
        }

        if (!UserRepository.VerifyPin(pin, user.PinHash))
        {
            return HandleFailedAuthentication(
                user,
                "Unlock PIN",
                "UNLOCK_FAILED",
                "Invalid PIN.",
                "Invalid PIN.");
        }

        TryUpgradeCredentialHash(user, pin, CredentialKind.Pin, user.PinHash);
        ClearAuthenticationThrottleState(user, "UNLOCK_THROTTLE_RESET_FAILED", "Unlock PIN");
        SetCurrentUser(user);
        _audit.Log("SESSION_UNLOCKED", $"User: {user.Username}", user.Id);
        return new LoginResult(true, null);
    }

    public User CreateFirstAdmin(string username, string displayName, string password, string? pin)
    {
        username = username.Trim();
        displayName = displayName.Trim();

        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            PasswordHash = UserRepository.HashPassword(password),
            PinHash = !string.IsNullOrEmpty(pin) ? UserRepository.HashPin(pin) : null,
            IsAdmin = true,
            CanCheckout = true,
            CanManageProducts = true,
            CanManageSettings = true,
            CanManageUsers = true,
            CanViewReports = true,
            CanOverridePrice = true,
            IsActive = true,
            MustChangePassword = true
        };

        _userRepository.Create(user);
        _needsSetup = false;
        SetCurrentUser(user);
        _audit.Log("USER_CREATED", $"Username: {username}, Role: Admin (First Run)", user.Id);
        return user;
    }

    public User CreateCashier(string username, string displayName, string pin)
    {
        username = username.Trim();
        displayName = displayName.Trim();

        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            PinHash = UserRepository.HashPin(pin),
            IsAdmin = false,
            CanCheckout = true,
            CanManageProducts = false,
            CanManageSettings = false,
            CanManageUsers = false,
            CanViewReports = false,
            IsActive = true
        };
        _userRepository.Create(user);
        _needsSetup = false;
        _audit.Log("USER_CREATED", $"Username: {username}, Role: Cashier", _currentUser?.Id);
        return user;
    }

    public void MarkActivity()
    {
        if (_isLocked || _currentSession == null)
        {
            return;
        }

        _currentSession.LastActivity = DateTime.UtcNow;
    }

    public bool IsSessionIdleTimeoutExceeded(TimeSpan timeout, DateTime utcNow)
    {
        if (_isLocked || _currentSession == null || timeout <= TimeSpan.Zero)
        {
            return false;
        }

        return utcNow - _currentSession.LastActivity >= timeout;
    }

    private void SetCurrentUser(User user, bool notifyStateChanged = true)
    {
        _isLocked = false;
        _currentUser = user;
        _currentSession = new Session
        {
            UserId = user.Id,
            SessionToken = Guid.NewGuid().ToString(),
            StartedAt = DateTime.UtcNow,
            LastActivity = DateTime.UtcNow,
            IsActive = true,
            User = user
        };
        if (notifyStateChanged)
        {
            NotifyLoginStateChanged();
        }
    }

    private void NotifyLoginStateChanged()
    {
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Permission check helpers - IsAdmin grants ALL permissions
    public bool CanCheckout => !_isLocked && (_currentUser?.IsAdmin == true || (_currentUser?.CanCheckout ?? false));
    public bool CanManageProducts => !_isLocked && (_currentUser?.IsAdmin == true || (_currentUser?.CanManageProducts ?? false));
    public bool CanManageSettings => !_isLocked && (_currentUser?.IsAdmin == true || (_currentUser?.CanManageSettings ?? false));
    public bool CanManageUsers => !_isLocked && _currentUser?.IsAdmin == true;
    public bool CanViewReports => !_isLocked && (_currentUser?.IsAdmin == true || (_currentUser?.CanViewReports ?? false));
    public bool CanOverridePrice =>
        !_isLocked && (_currentUser?.IsAdmin == true || (_currentUser?.CanOverridePrice ?? false));

    public IReadOnlyList<User> GetActiveStaff()
    {
        var active = _userRepository.GetActive();

        var cashierUsers = active
            .Where(u => !u.IsAdmin && u.CanCheckout)
            .ToList();

        if (cashierUsers.Count > 0)
        {
            return cashierUsers;
        }

        var pinUsers = active
            .Where(u => !string.IsNullOrWhiteSpace(u.PinHash))
            .ToList();

        if (pinUsers.Count > 0)
        {
            return pinUsers;
        }

        return active;
    }

    private void TryUpgradeCredentialHash(User user, string rawCredential, CredentialKind credentialKind, string? storedHash)
    {
        if (!UserRepository.NeedsPbkdf2Rehash(storedHash))
        {
            return;
        }

        var originalPasswordHash = user.PasswordHash;
        var originalPinHash = user.PinHash;

        try
        {
            if (credentialKind == CredentialKind.Password)
            {
                user.PasswordHash = UserRepository.HashPassword(rawCredential);
            }
            else
            {
                user.PinHash = UserRepository.HashPin(rawCredential);
            }

            _userRepository.Update(user);
            _audit.Log(
                "CREDENTIAL_REHASHED",
                $"User: {user.Username}, Credential: {credentialKind}, Upgrade: PBKDF2-600K",
                user.Id);
        }
        catch (Exception ex)
        {
            user.PasswordHash = originalPasswordHash;
            user.PinHash = originalPinHash;
            _audit.Log(
                "CREDENTIAL_REHASH_FAILED",
                $"User: {user.Username}, Credential: {credentialKind}, Error: {ex.Message}",
                user.Id);
        }
    }

    private LoginResult? ValidateAuthenticationLockout(User user, string method, string auditAction)
    {
        var utcNow = DateTime.UtcNow;
        NormalizeAuthenticationThrottleWindow(user, utcNow);

        if (user.LockedUntilUtc is not DateTime lockedUntilUtc || lockedUntilUtc <= utcNow)
        {
            return null;
        }

        _audit.Log(
            auditAction,
            $"User: {user.Username}, Method: {method}, LockedUntilUtc: {lockedUntilUtc:o}",
            user.Id);

        return new LoginResult(false, BuildLockoutMessage(lockedUntilUtc - utcNow));
    }

    private LoginResult HandleFailedAuthentication(
        User user,
        string method,
        string auditAction,
        string invalidAuditReason,
        string invalidErrorMessage)
    {
        var utcNow = DateTime.UtcNow;
        NormalizeAuthenticationThrottleWindow(user, utcNow);

        var nextFailedCount = user.FailedLoginCount + 1;
        var lockoutDuration = GetIncrementalLockoutDuration(nextFailedCount);
        var lockedUntilUtc = lockoutDuration > TimeSpan.Zero ? utcNow.Add(lockoutDuration) : (DateTime?)null;

        user.FailedLoginCount = nextFailedCount;
        user.LastFailedLoginAtUtc = utcNow;
        user.LockedUntilUtc = lockedUntilUtc;

        try
        {
            _userRepository.UpdateAuthenticationThrottleState(
                user.Id,
                user.FailedLoginCount,
                user.LastFailedLoginAtUtc,
                user.LockedUntilUtc);
        }
        catch (Exception ex)
        {
            _audit.Log(
                "AUTH_THROTTLE_PERSIST_FAILED",
                $"User: {user.Username}, Method: {method}, FailedCount: {nextFailedCount}, Error: {ex.Message}",
                user.Id);
        }

        var auditDetails = $"User: {user.Username}, Method: {method}, Reason: {invalidAuditReason}, FailedCount: {nextFailedCount}";
        if (lockoutDuration > TimeSpan.Zero)
        {
            auditDetails += $", LockoutMinutes: {lockoutDuration.TotalMinutes:0}";
        }

        _audit.Log(auditAction, auditDetails, user.Id);

        if (lockoutDuration > TimeSpan.Zero)
        {
            return new LoginResult(false, BuildLockoutMessage(lockoutDuration));
        }

        return new LoginResult(false, invalidErrorMessage);
    }

    private void ClearAuthenticationThrottleState(User user, string auditFailureAction, string method)
    {
        if (user.FailedLoginCount == 0 &&
            user.LastFailedLoginAtUtc is null &&
            user.LockedUntilUtc is null)
        {
            return;
        }

        user.FailedLoginCount = 0;
        user.LastFailedLoginAtUtc = null;
        user.LockedUntilUtc = null;

        try
        {
            _userRepository.UpdateAuthenticationThrottleState(user.Id, 0, null, null);
        }
        catch (Exception ex)
        {
            _audit.Log(
                auditFailureAction,
                $"User: {user.Username}, Method: {method}, Error: {ex.Message}",
                user.Id);
        }
    }

    private void NormalizeAuthenticationThrottleWindow(User user, DateTime utcNow)
    {
        if (user.FailedLoginCount <= 0 || user.LastFailedLoginAtUtc is not DateTime lastFailedAtUtc)
        {
            return;
        }

        if (utcNow - lastFailedAtUtc < FailedAuthenticationResetWindow)
        {
            return;
        }

        ClearAuthenticationThrottleState(user, "AUTH_THROTTLE_RESET_FAILED", "AutomaticReset");
    }

    private static TimeSpan GetIncrementalLockoutDuration(int failedLoginCount)
    {
        if (failedLoginCount < 5 || failedLoginCount % 5 != 0)
        {
            return TimeSpan.Zero;
        }

        return (failedLoginCount / 5) switch
        {
            1 => TimeSpan.FromMinutes(1),
            2 => TimeSpan.FromMinutes(3),
            3 => TimeSpan.FromMinutes(5),
            4 => TimeSpan.FromMinutes(10),
            _ => TimeSpan.FromMinutes(15)
        };
    }

    private static string BuildLockoutMessage(TimeSpan remaining)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return minutes == 1
            ? "Account temporarily locked. Try again in 1 minute."
            : $"Account temporarily locked. Try again in {minutes} minutes.";
    }

    private enum CredentialKind
    {
        Password,
        Pin
    }

    private LoginResult? ValidateLockedSessionUser(string username)
    {
        if (!_isLocked || _currentUser == null)
        {
            return null;
        }

        if (!string.Equals(_currentUser.Username, username, StringComparison.OrdinalIgnoreCase))
        {
            return new LoginResult(false, "This session is locked. Re-enter the current user's credentials.");
        }

        return null;
    }
}

public record LoginResult(bool Success, string? ErrorMessage);


