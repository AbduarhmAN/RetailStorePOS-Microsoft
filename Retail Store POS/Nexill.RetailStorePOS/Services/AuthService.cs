using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Repositories;

namespace RetailStorePOS.App.Services;

public sealed class AuthService
{
    private readonly UserRepository _userRepository;
    private readonly AuditLogService _audit;
    private User? _currentUser;
    private Session? _currentSession;

public AuthService(UserRepository userRepository, AuditLogService audit)
    {
        _userRepository = userRepository;
        _audit = audit;
    }

    public User? CurrentUser => _currentUser;
    public bool IsLoggedIn => _currentUser != null;
    public bool NeedsSetup => !_userRepository.UsersExist();

    public event EventHandler? LoginStateChanged;
    public Task<LoginResult> LoginWithPasswordAsync(string username, string password)
    {
        return Task.FromResult(LoginWithPassword(username, password));
    }
    public Task<LoginResult> LoginWithPinAsync(string username, string pin)
    {
        return Task.FromResult(LoginWithPin(username, pin));
    }

    public LoginResult LoginWithPassword(string username, string password)
    {
        username = username.Trim();
        var user = _userRepository.GetByUsername(username);
        if (user == null)
        {
            _audit.Log("LOGIN_FAILED", $"Username: {username}, Method: Password, Reason: User not found");
            return new LoginResult(false, "User not found.");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (!UserRepository.VerifyPassword(password, user.PasswordHash))
            {
                _audit.Log("LOGIN_FAILED", $"User: {username}, Method: Password, Reason: Invalid password");
                return new LoginResult(false, "Invalid password.");
            }
        }
        else if (!string.IsNullOrEmpty(user.PinHash))
        {
            // Recovery path: allow password box login using PIN value
            // for admin users created without a stored password.
            if (!UserRepository.VerifyPin(password, user.PinHash))
            {
                _audit.Log("LOGIN_FAILED", $"User: {username}, Method: Password(PIN), Reason: Invalid password");
                return new LoginResult(false, "Invalid password.");
            }
        }
        else
        {
            _audit.Log("LOGIN_FAILED", $"User: {username}, Method: Password, Reason: No credentials configured");
            return new LoginResult(false, "User has no login credentials configured.");
        }

        SetCurrentUser(user);
        _audit.Log("LOGIN_SUCCESS", $"User: {username}, Method: Password", user.Id);
        return new LoginResult(true, null);
    }

    public LoginResult LoginWithPin(string username, string pin)
    {
        username = username.Trim();
        if (string.IsNullOrEmpty(username))
        {
            return new LoginResult(false, "Username is required.");
        }

        if (string.IsNullOrEmpty(pin) || pin.Length < 4)
        {
            return new LoginResult(false, "PIN must be at least 4 digits.");
        }

        var user = _userRepository.GetByUsername(username);
        if (user == null)
        {
            _audit.Log("LOGIN_FAILED", $"Username: {username}, Method: PIN, Reason: User not found");
            return new LoginResult(false, "Invalid credentials.");
        }

        if (!UserRepository.VerifyPin(pin, user.PinHash))
        {
            _audit.Log("LOGIN_FAILED", $"User: {username}, Method: PIN, Reason: Invalid PIN");
            return new LoginResult(false, "Invalid credentials.");
        }

        SetCurrentUser(user);
        _audit.Log("LOGIN_SUCCESS", $"User: {username}, Method: PIN", user.Id);
        return new LoginResult(true, null);
    }

    public void Logout()
    {
        if (_currentUser != null)
        {
            _audit.Log("LOGOUT", $"User: {_currentUser.Username}", _currentUser.Id);
        }
        _currentUser = null;
        _currentSession = null;
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
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
            _currentUser = null;
            _currentSession = null;
            LoginStateChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _currentUser = refreshed;
        if (_currentSession != null)
        {
            _currentSession.User = refreshed;
            _currentSession.LastActivity = DateTime.UtcNow;
        }

        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Lock()
    {
        if (_currentUser != null)
        {
            _audit.Log("SESSION_LOCKED", $"User: {_currentUser.Username}", _currentUser.Id);
        }
        // Keep user reference but mark as locked for unlock flow
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    public LoginResult UnlockWithPin(string pin, long expectedUserId)
    {
        if (string.IsNullOrEmpty(pin) || pin.Length < 4)
        {
            return new LoginResult(false, "PIN must be at least 4 digits.");
        }

        var user = _userRepository.GetById(expectedUserId);

        if (user == null || !UserRepository.VerifyPin(pin, user.PinHash))
        {
            _audit.Log("UNLOCK_FAILED", $"TargetUser: {expectedUserId}, Reason: Invalid PIN");
            return new LoginResult(false, "Invalid PIN.");
        }

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
            IsActive = true
        };

        _userRepository.Create(user);
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
        _audit.Log("USER_CREATED", $"Username: {username}, Role: Cashier", _currentUser?.Id);
        return user;
    }

    private void SetCurrentUser(User user)
    {
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
        LoginStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // Permission check helpers - IsAdmin grants ALL permissions
    public bool CanCheckout => _currentUser?.IsAdmin == true || (_currentUser?.CanCheckout ?? false);
    public bool CanManageProducts => _currentUser?.IsAdmin == true || (_currentUser?.CanManageProducts ?? false);
    public bool CanManageSettings => _currentUser?.IsAdmin == true || (_currentUser?.CanManageSettings ?? false);
    public bool CanManageUsers => _currentUser?.IsAdmin == true;
    public bool CanViewReports => _currentUser?.IsAdmin == true || (_currentUser?.CanViewReports ?? false);
    public bool CanOverridePrice =>
        _currentUser?.IsAdmin == true || (_currentUser?.CanOverridePrice ?? false);

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
}

public record LoginResult(bool Success, string? ErrorMessage);


