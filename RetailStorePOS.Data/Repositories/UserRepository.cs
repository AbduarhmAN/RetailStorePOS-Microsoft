using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Models;

namespace RetailStorePOS.Data.Repositories;

public sealed class UserRepository
{
    private readonly SqliteConnectionFactory _factory;

    public UserRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public List<User> GetAll()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, username, display_name, password_hash, pin_hash, is_admin,
       can_checkout, can_manage_products, can_manage_settings, can_manage_users,
       can_view_reports, can_override_price, is_active, created_at, updated_at
FROM users
ORDER BY is_admin DESC, display_name;";

        using var reader = command.ExecuteReader();
        var results = new List<User>();
        while (reader.Read())
        {
            results.Add(MapUser(reader));
        }
        return results;
    }

    public List<User> GetActive()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, username, display_name, password_hash, pin_hash, is_admin,
       can_checkout, can_manage_products, can_manage_settings, can_manage_users,
       can_view_reports, can_override_price, is_active, created_at, updated_at
FROM users
WHERE is_active = 1
ORDER BY is_admin DESC, display_name;";

        using var reader = command.ExecuteReader();
        var results = new List<User>();
        while (reader.Read())
        {
            results.Add(MapUser(reader));
        }
        return results;
    }

    public User? GetById(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, username, display_name, password_hash, pin_hash, is_admin,
       can_checkout, can_manage_products, can_manage_settings, can_manage_users,
       can_view_reports, can_override_price, is_active, created_at, updated_at
FROM users WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public User? GetByUsername(string username)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, username, display_name, password_hash, pin_hash, is_admin,
       can_checkout, can_manage_products, can_manage_settings, can_manage_users,
       can_view_reports, can_override_price, is_active, created_at, updated_at
FROM users
WHERE lower(trim(username)) = lower(trim(@username))
  AND is_active = 1;";
        command.Parameters.AddWithValue("@username", username);

        using var reader = command.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public bool UsernameExists(string username, long? excludeUserId = null)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT COUNT(*)
FROM users
WHERE lower(trim(username)) = lower(trim(@username))
  AND (@exclude_user_id IS NULL OR id <> @exclude_user_id);";
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@exclude_user_id", excludeUserId.HasValue ? excludeUserId.Value : DBNull.Value);
        var count = Convert.ToInt64(command.ExecuteScalar());
        return count > 0;
    }

    public User? GetByPin(string pinHash)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT id, username, display_name, password_hash, pin_hash, is_admin,
       can_checkout, can_manage_products, can_manage_settings, can_manage_users,
       can_view_reports, can_override_price, is_active, created_at, updated_at
FROM users WHERE pin_hash = @pin_hash AND is_active = 1;";
        command.Parameters.AddWithValue("@pin_hash", pinHash);

        using var reader = command.ExecuteReader();
        User? foundUser = null;
        while (reader.Read())
        {
            if (foundUser != null)
            {
                // SECURITY: Multiple users share this PIN - deny login to prevent unauthorized access
                return null;
            }
            foundUser = MapUser(reader);
        }
        return foundUser;
    }

    public bool UsersExist()
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM users;";
        var count = Convert.ToInt64(command.ExecuteScalar());
        return count > 0;
    }

    public bool IsPinTaken(string rawPin, long? excludeUserId = null)
    {
        // With salted hashes, we can't search by hash. We must verify against all active users.
        // This is O(N) but N is small (<100 users).
        var activeUsers = GetActive();
        foreach (var user in activeUsers)
        {
            if (excludeUserId.HasValue && user.Id == excludeUserId.Value) continue;
            if (string.IsNullOrEmpty(user.PinHash)) continue;

            if (VerifyPin(rawPin, user.PinHash))
            {
                return true;
            }
        }
        return false;
    }

    public long Create(User user)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();

        user.Username = user.Username.Trim();
        user.DisplayName = user.DisplayName.Trim();

        if (UsernameExists(user.Username))
        {
            throw new InvalidOperationException("Username is already in use.");
        }

        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = @"
INSERT INTO users (username, display_name, password_hash, pin_hash, is_admin,
                   can_checkout, can_manage_products, can_manage_settings, can_manage_users,
                   can_view_reports, can_override_price, is_active, created_at, updated_at)
VALUES (@username, @display_name, @password_hash, @pin_hash, @is_admin,
        @can_checkout, @can_manage_products, @can_manage_settings, @can_manage_users,
        @can_view_reports, @can_override_price, @is_active, @created_at, @updated_at);SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@display_name", user.DisplayName);
        command.Parameters.AddWithValue("@password_hash", (object?)user.PasswordHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@pin_hash", (object?)user.PinHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@is_admin", user.IsAdmin ? 1 : 0);
        command.Parameters.AddWithValue("@can_checkout", user.CanCheckout ? 1 : 0);
        command.Parameters.AddWithValue("@can_manage_products", user.CanManageProducts ? 1 : 0);
        command.Parameters.AddWithValue("@can_manage_settings", user.CanManageSettings ? 1 : 0);
        command.Parameters.AddWithValue("@can_manage_users", user.CanManageUsers ? 1 : 0);
        command.Parameters.AddWithValue("@can_view_reports", user.CanViewReports ? 1 : 0);
        command.Parameters.AddWithValue("@can_override_price", user.CanOverridePrice ? 1 : 0);
        command.Parameters.AddWithValue("@is_active", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@created_at", now);
        command.Parameters.AddWithValue("@updated_at", now);

        try
        {
            var result = command.ExecuteScalar();
            user.Id = Convert.ToInt64(result);
            return user.Id;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Username is already in use.", ex);
        }
    }

    public void Update(User user)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();

        user.Username = user.Username.Trim();
        user.DisplayName = user.DisplayName.Trim();

        if (UsernameExists(user.Username, user.Id))
        {
            throw new InvalidOperationException("Username is already in use.");
        }

        var now = DateTime.UtcNow.ToString("o");
        command.CommandText = @"
UPDATE users SET
    username = @username,
    display_name = @display_name,
    password_hash = @password_hash,
    pin_hash = @pin_hash,
    is_admin = @is_admin,
    can_checkout = @can_checkout,
    can_manage_products = @can_manage_products,
    can_manage_settings = @can_manage_settings,
    can_manage_users = @can_manage_users,
    can_view_reports = @can_view_reports,
    can_override_price = @can_override_price,
    is_active = @is_active,
    updated_at = @updated_at
WHERE id = @id;";

        command.Parameters.AddWithValue("@id", user.Id);
        command.Parameters.AddWithValue("@username", user.Username);
        command.Parameters.AddWithValue("@display_name", user.DisplayName);
        command.Parameters.AddWithValue("@password_hash", (object?)user.PasswordHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@pin_hash", (object?)user.PinHash ?? DBNull.Value);
        command.Parameters.AddWithValue("@is_admin", user.IsAdmin ? 1 : 0);
        command.Parameters.AddWithValue("@can_checkout", user.CanCheckout ? 1 : 0);
        command.Parameters.AddWithValue("@can_manage_products", user.CanManageProducts ? 1 : 0);
        command.Parameters.AddWithValue("@can_manage_settings", user.CanManageSettings ? 1 : 0);
        command.Parameters.AddWithValue("@can_manage_users", user.CanManageUsers ? 1 : 0);
        command.Parameters.AddWithValue("@can_view_reports", user.CanViewReports ? 1 : 0);
        command.Parameters.AddWithValue("@can_override_price", user.CanOverridePrice ? 1 : 0);
        command.Parameters.AddWithValue("@is_active", user.IsActive ? 1 : 0);
        command.Parameters.AddWithValue("@updated_at", now);

        try
        {
            command.ExecuteNonQuery();
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Username is already in use.", ex);
        }
    }

    public void Deactivate(long id)
    {
        using var connection = _factory.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE users SET is_active = 0, updated_at = @updated_at WHERE id = @id;";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@updated_at", DateTime.UtcNow.ToString("o"));
        command.ExecuteNonQuery();
    }

    public static string HashPassword(string password)
    {
        return HashWithPbkdf2(password);
    }

    public static string HashPin(string pin)
    {
        return HashWithPbkdf2(pin);
    }

    public static bool VerifyPassword(string password, string? storedHash)
    {
        if (string.IsNullOrEmpty(storedHash)) return false;

        if (storedHash.StartsWith("v2:"))
        {
            return VerifyPbkdf2(password, storedHash);
        }

        // Legacy SHA-256 fallback
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        var legacyHash = Convert.ToBase64String(bytes);
        return legacyHash == storedHash;
    }

    public static bool VerifyPin(string pin, string? storedHash)
    {
        // Re-use password verification logic since format is identical
        return VerifyPassword(pin, storedHash);
    }

    private static string HashWithPbkdf2(string input)
    {
        const int iterations = 10000;
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(input),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return $"v2:{iterations}:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPbkdf2(string input, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 4) return false;

        if (!int.TryParse(parts[1], out int iterations)) return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expectedHash = Convert.FromBase64String(parts[3]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(input),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static User MapUser(SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt64(0),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2),
            PasswordHash = reader.IsDBNull(3) ? null : reader.GetString(3),
            PinHash = reader.IsDBNull(4) ? null : reader.GetString(4),
            IsAdmin = reader.GetInt32(5) == 1,
            CanCheckout = reader.GetInt32(6) == 1,
            CanManageProducts = reader.GetInt32(7) == 1,
            CanManageSettings = reader.GetInt32(8) == 1,
            CanManageUsers = reader.GetInt32(9) == 1,
            CanViewReports = reader.GetInt32(10) == 1,
            CanOverridePrice = reader.GetInt32(11) == 1,
            IsActive = reader.GetInt32(12) == 1,
            CreatedAt = DateTime.Parse(reader.GetString(13)),
            UpdatedAt = DateTime.Parse(reader.GetString(14))
        };
    }
}
