using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RetailStorePOS.Data.Modules.Reporting;

/// <summary>
/// HMAC-SHA256 signing for local snapshot files (dashboard read-models,
/// activation certificates, etc.). The signing key is generated once per
/// install, DPAPI-protected with <see cref="DataProtectionScope.CurrentUser"/>,
/// and stored under <c>{AppData}/secure/snapshot-signing-key.dat</c>.
///
/// Tamper detection: a snapshot file is treated as missing if its signature
/// fails to verify, forcing the producer (background worker) to regenerate
/// from the source of truth (the encrypted DB). This mirrors the lightweight
/// hierarchical verification model documented in
/// <c>documents/Lightweight Hierarchical Verification for RetailStorePOS-Microsoft V2.md</c>.
///
/// This service is intentionally Windows-only (uses DPAPI). Linux/macOS would
/// need a different key-protection strategy; not required for this app.
/// </summary>
public sealed class SnapshotSigningService
{
    private const string SecureFolderName = "secure";
    private const string KeyFileName = "snapshot-signing-key.dat";
    private const string EntropyPurpose = "RetailStorePOS.Data.SnapshotSigning.v1";
    private const int KeyByteLength = 32;

    private static readonly byte[] Entropy = SHA256.HashData(Encoding.UTF8.GetBytes(EntropyPurpose));
    private static readonly object Sync = new();

    private byte[]? _cachedKey;

    /// <summary>
    /// Returns the HMAC-SHA256 signature (raw bytes) of <paramref name="content"/>
    /// using the install-local signing key.
    /// </summary>
    public byte[] Sign(ReadOnlySpan<byte> content)
    {
        var key = GetOrCreateKey();
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(content.ToArray());
    }

    /// <summary>
    /// Verifies <paramref name="signature"/> against <paramref name="content"/>.
    /// Constant-time compare to avoid timing leaks.
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> content, ReadOnlySpan<byte> signature)
    {
        var expected = Sign(content);
        return CryptographicOperations.FixedTimeEquals(expected, signature.ToArray());
    }

    private byte[] GetOrCreateKey()
    {
        lock (Sync)
        {
            if (_cachedKey is not null)
            {
                return _cachedKey;
            }

            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException(
                    "Snapshot signing uses Windows DPAPI and requires Windows.");
            }

            var keyPath = GetKeyPath();
            byte[] key;
            if (File.Exists(keyPath))
            {
                key = ProtectedData.Unprotect(
                    File.ReadAllBytes(keyPath),
                    Entropy,
                    DataProtectionScope.CurrentUser);
                if (key.Length != KeyByteLength)
                {
                    // Corrupt or pre-versioned key file. Regenerate.
                    key = GenerateAndPersist(keyPath);
                }
            }
            else
            {
                key = GenerateAndPersist(keyPath);
            }

            _cachedKey = key;
            return key;
        }
    }

    private static byte[] GenerateAndPersist(string keyPath)
    {
        var key = RandomNumberGenerator.GetBytes(KeyByteLength);
        var protectedKey = ProtectedData.Protect(key, Entropy, DataProtectionScope.CurrentUser);

        var directory = Path.GetDirectoryName(keyPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Atomic write: temp -> fsync -> replace.
        var tempPath = keyPath + ".tmp";
        File.WriteAllBytes(tempPath, protectedKey);
        if (File.Exists(keyPath))
        {
            File.Replace(tempPath, keyPath, destinationBackupFileName: null);
        }
        else
        {
            File.Move(tempPath, keyPath);
        }
        return key;
    }

    private static string GetKeyPath()
    {
        return AppDataPaths.Combine(SecureFolderName, KeyFileName);
    }
}
