using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetailStorePOS.Data;

namespace RetailStorePOS.App.Services;

/// <summary>
/// Provides Windows DPAPI-based encryption for secrets stored on disk.
/// Data encrypted with DPAPI can ONLY be decrypted by the same Windows user
/// on the same machine. If the hard drive is stolen, the data is unreadable.
/// </summary>
public static class SecureStorageService
{
    private static readonly string SecureFolder;
    private static readonly string SecretsPath;

    static SecureStorageService()
    {
        SecureFolder = AppDataPaths.Combine("secure");
        Directory.CreateDirectory(SecureFolder);
        SecretsPath = Path.Combine(SecureFolder, "credentials.dat");
    }

    /// <summary>
    /// Encrypts a string value using Windows DPAPI and returns a Base64-encoded blob.
    /// </summary>
    public static string Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(
            plainBytes,
            null,
            DataProtectionScope.CurrentUser
        );
        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Decrypts a DPAPI-encrypted Base64 string back to plaintext.
    /// </summary>
    public static string Decrypt(string encryptedBase64)
    {
        var encryptedBytes = Convert.FromBase64String(encryptedBase64);
        var plainBytes = ProtectedData.Unprotect(
            encryptedBytes,
            null,
            DataProtectionScope.CurrentUser
        );
        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Stores a key-value secret encrypted on disk.
    /// </summary>
    public static void StoreSecret(string key, string value)
    {
        var secrets = LoadAllSecrets();
        secrets[key] = Encrypt(value);
        var json = JsonSerializer.Serialize(secrets, StartupJsonContext.Default.StringDictionary);
        var secretsDirectory = Path.GetDirectoryName(SecretsPath);
        if (!string.IsNullOrWhiteSpace(secretsDirectory))
        {
            Directory.CreateDirectory(secretsDirectory);
        }

        File.WriteAllText(SecretsPath, json);
    }

    /// <summary>
    /// Retrieves a decrypted secret by key. Returns null if not found.
    /// </summary>
    public static string? GetSecret(string key)
    {
        var secrets = LoadAllSecrets();
        if (secrets.TryGetValue(key, out var encryptedValue))
        {
            try
            {
                return Decrypt(encryptedValue);
            }
            catch
            {
                return null; // Corrupt or wrong user
            }
        }
        return null;
    }

    /// <summary>
    /// Checks whether the secure credentials store has been initialized.
    /// </summary>
    public static bool HasSecrets()
    {
        return File.Exists(SecretsPath);
    }

    /// <summary>
    /// Seeds the initial secrets into the encrypted vault.
    /// This should be called once during first-run setup.
    /// </summary>
    public static void SeedIfNeeded(string supabaseUrl, string supabaseKey)
    {
        // If the secrets exist AND can be successfully decrypted, do not seed.
        if (HasSecrets() && !string.IsNullOrEmpty(GetSecret("SupabaseUrl")))
        {
            return;
        }

        StoreSecret("SupabaseUrl", supabaseUrl);
        StoreSecret("SupabaseKey", supabaseKey);
    }

    private static Dictionary<string, string> LoadAllSecrets()
    {
        TryMigrateLegacySecretsFile();

        if (!File.Exists(SecretsPath))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var json = File.ReadAllText(SecretsPath);
            return JsonSerializer.Deserialize(json, StartupJsonContext.Default.StringDictionary)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static void TryMigrateLegacySecretsFile()
    {
        if (File.Exists(SecretsPath))
        {
            return;
        }

        var legacyPath = Path.Combine(AppDataPaths.CombineLegacy("secure"), "credentials.dat");
        if (!File.Exists(legacyPath))
        {
            return;
        }

        Directory.CreateDirectory(SecureFolder);
        File.Copy(legacyPath, SecretsPath, overwrite: false);
    }
}
