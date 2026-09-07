using System;
using System.Security.Cryptography;
using System.Text;
using RetailStorePOS.Data.Modules.Settings;

namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Generates and stores per-install bootstrap-admin credentials so the binary
/// no longer ships with a publicly-known default password ("1234"). On first
/// run the service creates a cryptographically random password and PIN, stores
/// them as DPAPI-protected blobs under <see cref="SettingsRepository"/>, and
/// surfaces them once via the existing BootstrapAdminHint UI so the operator
/// can capture them. After the operator changes the bootstrap password the
/// stored values are cleared.
///
/// Why DPAPI (CurrentUser) on top of SQLCipher? SQLCipher protects the DB at
/// rest from another process or another Windows user, but the operator runs
/// the app as that user. Adding DPAPI means an attacker who pulls the SQLite
/// file off the disk still cannot recover the bootstrap password without also
/// being able to call <c>ProtectedData.Unprotect</c> as the same user.
/// </summary>
public sealed class BootstrapAdminCredentials
{
    // Settings keys (stored in the local settings table, DPAPI-encrypted blobs).
    internal const string TempPasswordKey = "bootstrap.admin.temp_password";
    internal const string TempPinKey = "bootstrap.admin.temp_pin";

    // 12 character password from a safe (non-confusable) alphabet — enough
    // entropy that brute-force is impractical even if the SQLite file leaks.
    private const int PasswordLength = 12;
    // Cashier PINs are 4 digits across the app, including Settings.
    private const int PinLength = 4;

    // Excludes 0/O/1/I/l to avoid the kind of mistakes operators make when
    // copying values off a screen.
    private const string PasswordAlphabet =
        "ABCDEFGHJKLMNPQRSTUVWXYZ" +
        "abcdefghijkmnopqrstuvwxyz" +
        "23456789" +
        "@#$%&*+-=";

    private readonly SettingsRepository _settings;

    public BootstrapAdminCredentials(SettingsRepository settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Generates fresh random credentials and persists them DPAPI-encrypted.
    /// Always overwrites any prior values; intended to be called only when the
    /// bootstrap admin is being created.
    /// </summary>
    public BootstrapCredentialPair GenerateAndPersist()
    {
        var password = RandomString(PasswordAlphabet, PasswordLength);
        var pin = RandomDigits(PinLength);
        while (pin == "1234")
        {
            pin = RandomDigits(PinLength);
        }

        _settings.SetSetting(TempPasswordKey, EncryptForStorage(password));
        _settings.SetSetting(TempPinKey, EncryptForStorage(pin));

        return new BootstrapCredentialPair(password, pin);
    }

    /// <summary>
    /// Returns the cached bootstrap credentials if any are still on disk. Used
    /// by the login screen hint and by the bootstrap-still-needs-change check.
    /// Returns null when no temp credentials are stored or the DPAPI blob is
    /// unreadable (e.g. user profile changed).
    /// </summary>
    public BootstrapCredentialPair? Load()
    {
        var rawPassword = _settings.GetSetting(TempPasswordKey, string.Empty);
        var rawPin = _settings.GetSetting(TempPinKey, string.Empty);

        var password = TryDecrypt(rawPassword);
        var pin = TryDecrypt(rawPin);

        if (password is null || pin is null)
        {
            return null;
        }

        return new BootstrapCredentialPair(password, pin);
    }

    /// <summary>
    /// Removes the persisted temp credentials. Called once the operator has
    /// chosen a real password through the change-password flow.
    /// </summary>
    public void Clear()
    {
        _settings.SetSetting(TempPasswordKey, string.Empty);
        _settings.SetSetting(TempPinKey, string.Empty);
    }

    private static string EncryptForStorage(string value)
    {
        // DPAPI(CurrentUser) — the same protection scope SecureStorageService
        // already uses for the Supabase secrets.
        var bytes = Encoding.UTF8.GetBytes(value);
        var encrypted = System.Security.Cryptography.ProtectedData.Protect(
            bytes,
            optionalEntropy: null,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    private static string? TryDecrypt(string? encryptedBase64)
    {
        if (string.IsNullOrWhiteSpace(encryptedBase64))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(encryptedBase64);
            var plain = System.Security.Cryptography.ProtectedData.Unprotect(
                bytes,
                optionalEntropy: null,
                System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static string RandomString(string alphabet, int length)
    {
        var buffer = new char[length];
        Span<byte> indexBytes = stackalloc byte[sizeof(uint)];

        for (var i = 0; i < length; i++)
        {
            // Use rejection sampling to avoid modulo bias on the alphabet.
            uint sample;
            do
            {
                RandomNumberGenerator.Fill(indexBytes);
                sample = BitConverter.ToUInt32(indexBytes);
            }
            while (sample >= uint.MaxValue - (uint.MaxValue % (uint)alphabet.Length));

            buffer[i] = alphabet[(int)(sample % (uint)alphabet.Length)];
        }

        return new string(buffer);
    }

    private static string RandomDigits(int length)
    {
        var buffer = new char[length];
        Span<byte> sample = stackalloc byte[1];

        for (var i = 0; i < length; i++)
        {
            // Same rejection-sampling pattern, smaller alphabet (10 digits).
            int digit;
            do
            {
                RandomNumberGenerator.Fill(sample);
            }
            while (sample[0] >= 250); // 250 = 25 * 10, largest multiple of 10 <= 256

            digit = sample[0] % 10;
            buffer[i] = (char)('0' + digit);
        }

        return new string(buffer);
    }
}

/// <summary>
/// Plain text bootstrap credentials. Consumers should display them once and
/// not persist. The DB-stored copies are DPAPI-encrypted via
/// <see cref="BootstrapAdminCredentials"/>.
/// </summary>
public sealed record BootstrapCredentialPair(string Password, string Pin);
