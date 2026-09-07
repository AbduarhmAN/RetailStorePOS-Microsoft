using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RetailStorePOS.UI.Common.Services;

internal sealed record DevicePublicKeyJwk(
    [property: JsonPropertyName("kty")] string KeyType,
    [property: JsonPropertyName("crv")] string Curve,
    [property: JsonPropertyName("x")] string X,
    [property: JsonPropertyName("y")] string Y);

internal sealed record DevicePublicKeyIdentity(
    DevicePublicKeyJwk PublicKey,
    string Thumbprint,
    bool IsHardwareBacked);

internal sealed record DeviceKeyProof(
    DevicePublicKeyIdentity Identity,
    string Signature,
    string SignatureAlgorithm);

/// <summary>
/// Owns the per-Windows-user device signing key used by licensing protocol v2.
/// The private key remains inside CNG and is created non-exportable. The
/// Microsoft Platform Crypto Provider is preferred so supported machines keep
/// the key in the TPM; the software KSP is the compatibility fallback.
/// </summary>
internal sealed class DeviceKeyService
{
    internal const string ExpectedAlgorithm = "ECDSA-P256-SHA256";
    private const string KeyName = "Nexill.RetailStorePOS.Licensing.DeviceKey.v1";
    private static readonly CngProvider[] Providers =
    {
        CngProvider.MicrosoftPlatformCryptoProvider,
        CngProvider.MicrosoftSoftwareKeyStorageProvider,
    };
    private static readonly object _sync = new();

    public DevicePublicKeyIdentity GetPublicIdentity()
    {
        lock (_sync)
        {
            using var key = OpenOrCreateKey();
            using var signer = new ECDsaCng(key);
            return BuildPublicIdentity(
                signer.ExportParameters(includePrivateParameters: false),
                IsPlatformProvider(key.Provider));
        }
    }

    public DeviceKeyProof Sign(JsonElement payload, string expectedThumbprint)
    {
        lock (_sync)
        {
            using var key = OpenOrCreateKey();
            using var signer = new ECDsaCng(key);
            var identity = BuildPublicIdentity(
                signer.ExportParameters(includePrivateParameters: false),
                IsPlatformProvider(key.Provider));
            if (!string.Equals(identity.Thumbprint, expectedThumbprint, StringComparison.Ordinal))
            {
                throw new CryptographicException("Device identity changed; enrollment recovery is required.");
            }
            var signature = SignCanonicalJson(signer, payload);
            return new DeviceKeyProof(identity, signature, ExpectedAlgorithm);
        }
    }

    public DeviceKeyProof SignServerChallenge(
        JsonElement envelope, DeviceChallengeExpectation expected, DateTimeOffset now)
    {
        var failure = DeviceChallengeProtocol.ValidateServerEnvelope(envelope, expected, now, out var payload);
        if (failure is not null) throw new CryptographicException(failure);
        // Do not touch the device private key before authenticating the challenge.
        return Sign(payload, expected.Thumbprint);
    }

    internal static string SignCanonicalJson(ECDsa signer, JsonElement payload)
    {
        ArgumentNullException.ThrowIfNull(signer);
        var canonical = CanonicalJson.Serialize(payload);
        var signature = signer.SignData(
            Encoding.UTF8.GetBytes(canonical),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return Convert.ToBase64String(signature);
    }

    internal static DevicePublicKeyIdentity BuildPublicIdentity(
        ECParameters parameters,
        bool isHardwareBacked)
    {
        var x = parameters.Q.X;
        var y = parameters.Q.Y;
        if (parameters.Curve.Oid.Value != ECCurve.NamedCurves.nistP256.Oid.Value
            || x is null || y is null || x.Length != 32 || y.Length != 32)
        {
            throw new CryptographicException("Device key is not a valid P-256 public key.");
        }

        var jwk = new DevicePublicKeyJwk(
            KeyType: "EC",
            Curve: "P-256",
            X: ToBase64Url(x),
            Y: ToBase64Url(y));
        return new DevicePublicKeyIdentity(jwk, ComputeThumbprint(jwk), isHardwareBacked);
    }

    internal static string ComputeThumbprint(DevicePublicKeyJwk jwk)
    {
        ArgumentNullException.ThrowIfNull(jwk);
        if (jwk.KeyType != "EC" || jwk.Curve != "P-256")
        {
            throw new CryptographicException("Device public key must use EC P-256.");
        }

        using var publicKey = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = DecodeCoordinate(jwk.X), Y = DecodeCoordinate(jwk.Y) },
        });
        // RFC 7638 requires lexicographically ordered required JWK members.
        var canonicalJwk =
            $"{{\"crv\":\"{jwk.Curve}\",\"kty\":\"{jwk.KeyType}\",\"x\":\"{jwk.X}\",\"y\":\"{jwk.Y}\"}}";
        return ToBase64Url(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJwk)));
    }

    private static CngKey OpenOrCreateKey()
    {
        foreach (var provider in Providers)
        {
            bool exists;
            try
            {
                exists = CngKey.Exists(KeyName, provider);
            }
            catch (CryptographicException)
            {
                continue;
            }
            catch (PlatformNotSupportedException)
            {
                continue;
            }

            // An existing key that cannot be opened is an error, not permission
            // to create a new identity in another provider.
            if (exists)
            {
                return OpenExistingKey(provider);
            }
        }

        CryptographicException? lastFailure = null;
        foreach (var provider in Providers)
        {
            try
            {
                return CngKey.Create(
                    CngAlgorithm.ECDsaP256,
                    KeyName,
                    new CngKeyCreationParameters
                    {
                        Provider = provider,
                        ExportPolicy = CngExportPolicies.None,
                        KeyUsage = CngKeyUsages.Signing,
                        KeyCreationOptions = CngKeyCreationOptions.None,
                        UIPolicy = new CngUIPolicy(CngUIProtectionLevels.None),
                    });
            }
            catch (CryptographicException ex) when (ex.HResult == unchecked((int)0x8009000F))
            {
                // Another process created the same persistent key first.
                return OpenExistingKey(provider);
            }
            catch (CryptographicException ex)
            {
                lastFailure = ex;
            }
            catch (PlatformNotSupportedException ex)
            {
                lastFailure = new CryptographicException("Windows CNG is unavailable.", ex);
            }
        }

        throw new CryptographicException(
            "A persistent device signing key could not be opened or created.",
            lastFailure);
    }

    private static CngKey OpenExistingKey(CngProvider provider)
    {
        var key = CngKey.Open(KeyName, provider);
        try
        {
            if (key.ExportPolicy != CngExportPolicies.None
                || key.AlgorithmGroup != CngAlgorithmGroup.ECDsa)
            {
                throw new CryptographicException("The stored device key has an unexpected policy or algorithm.");
            }

            return key;
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    private static byte[] DecodeCoordinate(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 43
            || value.Any(c => !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                or >= '0' and <= '9' or '-' or '_')))
        {
            throw new CryptographicException("Invalid P-256 coordinate encoding.");
        }

        var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + "=");
        if (ToBase64Url(bytes) != value)
        {
            throw new CryptographicException("Noncanonical P-256 coordinate encoding.");
        }

        return bytes;
    }

    private static bool IsPlatformProvider(CngProvider? provider) =>
        provider is not null
        && string.Equals(
            provider.Provider,
            CngProvider.MicrosoftPlatformCryptoProvider.Provider,
            StringComparison.Ordinal);

    private static string ToBase64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
