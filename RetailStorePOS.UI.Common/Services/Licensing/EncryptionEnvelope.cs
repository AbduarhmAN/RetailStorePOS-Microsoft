using System;
using System.Security.Cryptography;
using System.Text;

namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Builds an <see cref="EncryptionEnvelopeDto"/> for the license-api request,
/// matching the contract in <c>supabase/functions/license-api/utils/envelope.ts</c>.
///
/// Construction:
/// <list type="number">
///   <item>Generate a fresh 256-bit AES key and a 96-bit nonce. The nonce is
///         RFC-recommended size for AES-GCM and the key is unique per request,
///         so nonce reuse is structurally impossible.</item>
///   <item>Encrypt the UTF-8 bytes of the inner JSON with AES-GCM. The 128-bit
///         authentication tag is kept separate on the wire (matches the .NET
///         <c>AesGcm</c> shape; the Edge Function joins them before passing to
///         <c>crypto.subtle.decrypt</c>).</item>
///   <item>Wrap the AES key with the backend RSA-OAEP-SHA256 public key from
///         <see cref="PublicEncryptionKey"/>.</item>
///   <item>Base64-encode each binary field and emit the envelope.</item>
/// </list>
///
/// All sensitive byte arrays (the AES key and the plaintext) are zeroed before
/// the method returns so a debugger or memory dump shortly after the call
/// cannot recover them.
/// </summary>
internal static class EncryptionEnvelope
{
    private const int AesKeySizeBytes = 32;   // 256-bit key
    private const int NonceSizeBytes = 12;    // 96-bit AES-GCM nonce per NIST SP 800-38D
    private const int TagSizeBytes = 16;      // 128-bit AES-GCM authentication tag

    /// <summary>
    /// Encrypts <paramref name="plaintextJson"/> into a fresh envelope using
    /// the backend public key currently configured via
    /// <see cref="PublicEncryptionKey"/>. Throws when no public key is
    /// configured (call sites should check
    /// <see cref="PublicEncryptionKey.IsConfigured"/> first and fall back to
    /// plain JSON).
    /// </summary>
    public static EncryptionEnvelopeDto Build(string plaintextJson)
    {
        if (plaintextJson is null) throw new ArgumentNullException(nameof(plaintextJson));

        var aesKey = new byte[AesKeySizeBytes];
        var nonce = new byte[NonceSizeBytes];
        byte[]? plaintextBytes = null;
        byte[] ciphertext;
        var tag = new byte[TagSizeBytes];
        byte[] wrappedKey;

        try
        {
            RandomNumberGenerator.Fill(aesKey);
            RandomNumberGenerator.Fill(nonce);

            plaintextBytes = Encoding.UTF8.GetBytes(plaintextJson);
            ciphertext = new byte[plaintextBytes.Length];

            using (var aes = new AesGcm(aesKey, TagSizeBytes))
            {
                aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);
            }

            wrappedKey = PublicEncryptionKey.WrapContentKey(aesKey);
        }
        finally
        {
            // Best-effort wipe of sensitive material. The plaintext is also
            // wiped because it contains the license key in cleartext.
            CryptographicOperations.ZeroMemory(aesKey);
            if (plaintextBytes is not null)
            {
                CryptographicOperations.ZeroMemory(plaintextBytes);
            }
        }

        return new EncryptionEnvelopeDto
        {
            KeyId = PublicEncryptionKey.GetKeyId(),
            EncryptedContentKey = Convert.ToBase64String(wrappedKey),
            Nonce = Convert.ToBase64String(nonce),
            Ciphertext = Convert.ToBase64String(ciphertext),
            AuthenticationTag = Convert.ToBase64String(tag),
        };
    }
}
