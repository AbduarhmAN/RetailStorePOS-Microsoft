using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetailStorePOS.App.Services;
using RetailStorePOS.App.Services.Licensing;

namespace LicensingTests;

/// <summary>
/// Round-trip tests for <see cref="EncryptionEnvelope"/> and the matching
/// decryption logic in the Edge Function. We cannot exercise the Deno code
/// directly from C# tests, so we re-implement the same AES-GCM + RSA-OAEP
/// decryption here using <see cref="System.Security.Cryptography"/>. If both
/// sides drift, this test breaks first.
///
/// The tests seed an ephemeral RSA keypair into <see cref="SecureStorageService"/>
/// for the duration of the test, then clear it on cleanup so subsequent
/// runs start with a fresh state. SecureStorageService is process-static so
/// concurrent tests within this class share a single configuration — the
/// test class is therefore not parallelised at the method level (MSTest's
/// default <see cref="Parallelize"/> attribute lives in MSTestSettings.cs;
/// individual tests can opt out via [DoNotParallelize] if needed but here we
/// rely on each test seeding its own deterministic key inside Setup).
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class EncryptionEnvelopeTests
{
    private const string EphemeralKeyId = "test-encryption-key-roundtrip";

    private RSA _backendKey = null!;
    private string? _previousJwk;
    private string? _previousKeyId;

    [TestInitialize]
    public void Setup()
    {
        // Snapshot any previous SecureStorageService state so the test cleans
        // up after itself — this matters in dev environments where the dev
        // has already seeded their own real keypair.
        _previousJwk = SecureStorageService.GetSecret(PublicEncryptionKey.PublicJwkSecretName);
        _previousKeyId = SecureStorageService.GetSecret(PublicEncryptionKey.KeyIdSecretName);

        _backendKey = RSA.Create(2048);
        var publicJwk = ExportPublicKeyAsJwk(_backendKey, EphemeralKeyId);

        SecureStorageService.StoreSecret(PublicEncryptionKey.PublicJwkSecretName, publicJwk);
        SecureStorageService.StoreSecret(PublicEncryptionKey.KeyIdSecretName, EphemeralKeyId);
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Restore the previous values (or leave empty if there were none).
        SecureStorageService.StoreSecret(
            PublicEncryptionKey.PublicJwkSecretName,
            _previousJwk ?? string.Empty);
        SecureStorageService.StoreSecret(
            PublicEncryptionKey.KeyIdSecretName,
            _previousKeyId ?? string.Empty);
        _backendKey?.Dispose();
    }

    [TestMethod]
    public void IsConfigured_True_AfterSeedingPublicJwk()
    {
        Assert.IsTrue(PublicEncryptionKey.IsConfigured);
        Assert.AreEqual(EphemeralKeyId, PublicEncryptionKey.GetKeyId());
    }

    [TestMethod]
    public void Build_RoundTripDecrypt_ProducesOriginalPlaintext()
    {
        const string plaintext =
            "{\"action\":\"verify_license\",\"installId\":\"INSTALL-T\",\"licenseKey\":\"RSPS-DEMO-1234\"}";

        var envelope = EncryptionEnvelope.Build(plaintext);

        Assert.AreEqual("encrypted_license_verification_envelope", envelope.MessageType);
        Assert.AreEqual(1, envelope.ProtocolVersion);
        Assert.AreEqual(EphemeralKeyId, envelope.KeyId);
        Assert.IsFalse(string.IsNullOrEmpty(envelope.EncryptedContentKey));
        Assert.IsFalse(string.IsNullOrEmpty(envelope.Nonce));
        Assert.IsFalse(string.IsNullOrEmpty(envelope.Ciphertext));
        Assert.IsFalse(string.IsNullOrEmpty(envelope.AuthenticationTag));

        var recovered = DecryptEnvelopeWithBackendKey(envelope, _backendKey);
        Assert.AreEqual(plaintext, recovered);
    }

    [TestMethod]
    public void Build_TwoEnvelopes_HaveDifferentNoncesAndContentKeys()
    {
        const string plaintext = "{\"a\":1}";

        var first = EncryptionEnvelope.Build(plaintext);
        var second = EncryptionEnvelope.Build(plaintext);

        Assert.AreNotEqual(first.Nonce, second.Nonce, "Nonce must be fresh per envelope.");
        Assert.AreNotEqual(first.EncryptedContentKey, second.EncryptedContentKey,
            "AES content key must be fresh per envelope.");
        Assert.AreNotEqual(first.Ciphertext, second.Ciphertext,
            "Same plaintext under different keys + nonces must produce different ciphertexts.");
    }

    [TestMethod]
    public void Build_TamperedCiphertext_FailsAuthenticationOnDecrypt()
    {
        var envelope = EncryptionEnvelope.Build("{\"x\":1}");

        // Flip a single byte of the ciphertext — AES-GCM must reject the tag.
        var bytes = Convert.FromBase64String(envelope.Ciphertext);
        bytes[0] ^= 0x01;
        var tampered = envelope with { Ciphertext = Convert.ToBase64String(bytes) };

        // AesGcm throws AuthenticationTagMismatchException, a subclass of
        // CryptographicException — Assert.Throws catches subclasses too,
        // ThrowsExactly does not.
        Assert.Throws<CryptographicException>(() =>
            DecryptEnvelopeWithBackendKey(tampered, _backendKey));
    }

    [TestMethod]
    public void Build_TamperedTag_FailsAuthenticationOnDecrypt()
    {
        var envelope = EncryptionEnvelope.Build("{\"x\":1}");

        var tagBytes = Convert.FromBase64String(envelope.AuthenticationTag);
        tagBytes[0] ^= 0x01;
        var tampered = envelope with { AuthenticationTag = Convert.ToBase64String(tagBytes) };

        Assert.Throws<CryptographicException>(() =>
            DecryptEnvelopeWithBackendKey(tampered, _backendKey));
    }

    // ------------------------------------------------------------------
    // Test-side decryption — re-implements the Deno side in pure .NET so
    // a contract drift between C# encryptor and TS decryptor breaks here.
    // ------------------------------------------------------------------
    private static string DecryptEnvelopeWithBackendKey(EncryptionEnvelopeDto envelope, RSA backendPrivate)
    {
        var wrappedKey = Convert.FromBase64String(envelope.EncryptedContentKey);
        var nonce = Convert.FromBase64String(envelope.Nonce);
        var ciphertext = Convert.FromBase64String(envelope.Ciphertext);
        var tag = Convert.FromBase64String(envelope.AuthenticationTag);

        var aesKey = backendPrivate.Decrypt(wrappedKey, RSAEncryptionPadding.OaepSHA256);
        Assert.AreEqual(32, aesKey.Length, "Recovered AES key must be 32 bytes (256-bit).");

        var plaintext = new byte[ciphertext.Length];
        using (var aes = new AesGcm(aesKey, tagSizeInBytes: 16))
        {
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }

    private static string ExportPublicKeyAsJwk(RSA rsa, string keyId)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var n = parameters.Modulus ?? throw new InvalidOperationException("modulus missing");
        var e = parameters.Exponent ?? throw new InvalidOperationException("exponent missing");

        var jwk = new
        {
            kty = "RSA",
            alg = "RSA-OAEP-256",
            use = "enc",
            kid = keyId,
            n = Base64UrlEncode(n),
            e = Base64UrlEncode(e),
            key_ops = new[] { "wrapKey", "encrypt" },
            ext = true,
        };

        return JsonSerializer.Serialize(jwk);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
