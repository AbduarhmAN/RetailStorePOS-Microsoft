using System;
using System.Security.Cryptography;
using System.Text.Json;
namespace RetailStorePOS.UI.Common.Services;

/// <summary>
/// Loads the backend RSA-OAEP-SHA256 public key the app uses to wrap the
/// per-request AES content key inside an <see cref="EncryptionEnvelopeDto"/>.
///
/// Key material is sourced from the same DPAPI-protected secrets vault that
/// already holds the Supabase URL/anon key (<see cref="SecureStorageService"/>).
/// Two secrets are read:
/// <list type="bullet">
///   <item><c>BackendEncryptionPublicJwk</c> — the JSON Web Key (JWK) string
///         containing the public key. RSA-OAEP, RSA-2048 minimum, SHA-256
///         hash. When missing or empty the encryption envelope is disabled
///         and <see cref="LicenseValidationService"/> posts plain JSON.</item>
///   <item><c>BackendEncryptionKeyId</c> — the <c>kid</c> value the Edge
///         Function expects. Defaults to <c>backend-encryption-key-1</c>.</item>
/// </list>
///
/// This deliberately does NOT bake the public key into the binary the way
/// <see cref="PublicLicenseKey"/> does for the signing key. The signing key
/// is a verify-only public credential — embedding it in the binary is fine
/// because it can only be used to check signatures the operator already
/// trusts. Encryption keys, by contrast, are operator-managed assets that
/// roll independently of app builds and need to be installable into a
/// running install without a redeploy.
///
/// Operator workflow:
///   1. Generate an RSA-2048+ keypair (e.g. <c>openssl genrsa -out priv.pem 2048</c>).
///   2. Export the public part as JWK (<c>openssl rsa -in priv.pem -pubout</c>
///      then convert to JWK with kty=RSA, alg=RSA-OAEP-256, kid=backend-encryption-key-1).
///   3. On every install, write the JWK string into
///      <see cref="SecureStorageService"/> under key
///      <see cref="PublicJwkSecretName"/>.
///   4. Upload the matching PKCS8 PEM (or JWK) to Supabase as the
///      <c>BACKEND_DECRYPTION_PRIVATE_KEY</c> secret.
///
/// Until step 3 happens on a given install, <see cref="IsConfigured"/> stays
/// false and the app continues to send plain JSON. The Edge Function accepts
/// both forms during the rollout.
/// </summary>
internal static class PublicEncryptionKey
{
    public const string PublicJwkSecretName = "BackendEncryptionPublicJwk";
    public const string KeyIdSecretName = "BackendEncryptionKeyId";
    public const string DefaultKeyId = "backend-encryption-key-1";

    /// <summary>
    /// True when an RSA-OAEP public JWK is configured on this install.
    /// <see cref="LicenseValidationService"/> consults this once per request
    /// to decide whether to wrap the request body in an envelope.
    /// </summary>
    public static bool IsConfigured => !string.IsNullOrWhiteSpace(LoadJwkText());

    /// <summary>
    /// Returns the configured key id (the <c>keyId</c> field that goes on the
    /// wire), defaulting to <see cref="DefaultKeyId"/> when no override is set.
    /// </summary>
    public static string GetKeyId()
    {
        var configured = SecureStorageService.GetSecret(KeyIdSecretName);
        return string.IsNullOrWhiteSpace(configured) ? DefaultKeyId : configured.Trim();
    }

    /// <summary>
    /// Wraps the supplied 32-byte AES key with the backend's RSA-OAEP-SHA256
    /// public key. Throws when no public key is configured or the configured
    /// JWK cannot be parsed.
    /// </summary>
    public static byte[] WrapContentKey(byte[] aesKey)
    {
        if (aesKey is null) throw new ArgumentNullException(nameof(aesKey));
        if (aesKey.Length != 32)
        {
            throw new ArgumentException(
                "AES content key must be 32 bytes (256 bits).",
                nameof(aesKey));
        }

        using var rsa = ImportPublicKey();
        return rsa.Encrypt(aesKey, RSAEncryptionPadding.OaepSHA256);
    }

    private static RSA ImportPublicKey()
    {
        var jwkText = LoadJwkText();
        if (string.IsNullOrWhiteSpace(jwkText))
        {
            throw new InvalidOperationException(
                "Backend encryption public key is not configured. " +
                $"Seed it via SecureStorageService under '{PublicJwkSecretName}' " +
                "before enabling envelope encryption.");
        }

        using var jwk = JsonDocument.Parse(jwkText);
        var root = jwk.RootElement;

        if (root.GetProperty("kty").GetString() != "RSA")
        {
            throw new InvalidOperationException(
                "Backend encryption JWK has unexpected key type. Expected 'RSA'.");
        }

        var nBytes = Base64UrlDecode(root.GetProperty("n").GetString() ?? string.Empty);
        var eBytes = Base64UrlDecode(root.GetProperty("e").GetString() ?? string.Empty);

        if (nBytes.Length < 256 /* 2048 bits */)
        {
            throw new InvalidOperationException(
                "Backend encryption modulus is shorter than 2048 bits — refusing to use weak key.");
        }

        var parameters = new RSAParameters
        {
            Modulus = nBytes,
            Exponent = eBytes,
        };

        var rsa = RSA.Create();
        try
        {
            rsa.ImportParameters(parameters);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
        return rsa;
    }

    private static string? LoadJwkText()
    {
        try
        {
            return SecureStorageService.GetSecret(PublicJwkSecretName);
        }
        catch
        {
            // Treat secure-storage failures the same as "not configured" so
            // a corrupt vault never crashes the licensing path.
            return null;
        }
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
