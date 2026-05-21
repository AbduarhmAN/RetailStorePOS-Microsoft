using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RetailStorePOS.App.Services.Licensing;

/// <summary>
/// Holds the embedded backend signing public key (P-256 / ES256) and verifies
/// ECDSA signatures over canonical JSON, exactly matching what
/// <c>supabase/functions/license-api/utils/crypto.ts</c> produces with
/// <c>crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, ...)</c>.
///
/// The key material is shipped INSIDE the binary, sourced from
/// <c>supabase/public-keys/backend-signing-public.jwk.json</c>. Public material
/// only — no signing capability. The matching private key never leaves the
/// Edge Function environment.
///
/// If the backend ever rotates the signing key:
/// <list type="number">
///   <item>Update <see cref="EmbeddedJwk"/> with the new public JWK.</item>
///   <item>Bump <see cref="KeyId"/> if the <c>kid</c> changed.</item>
///   <item>Ship the new app build and treat in-flight certificates signed by
///         the old key as untrusted.</item>
/// </list>
/// </summary>
internal static class PublicLicenseKey
{
    /// <summary>
    /// Algorithm advertised by the Edge Function in its response. Always
    /// <c>ECDSA-P256-SHA256</c> for protocol version 1; we hard-fail the
    /// verification if the backend returns anything else.
    /// </summary>
    public const string ExpectedAlgorithm = "ECDSA-P256-SHA256";

    /// <summary>
    /// Identifier the Edge Function returns alongside the signature. Used as a
    /// sanity check during verification — if the backend rotates and the kid
    /// no longer matches, we refuse the certificate and force a fresh activation.
    /// </summary>
    public const string KeyId = "backend-signing-key-1";

    /// <summary>
    /// Public JWK in P-256/ES256 form, copied verbatim from
    /// <c>supabase/public-keys/backend-signing-public.jwk.json</c>.
    /// </summary>
    private const string EmbeddedJwk = """
        {
          "kty": "EC",
          "crv": "P-256",
          "alg": "ES256",
          "x": "T4JA_daGd1CN-Kp55si3BLQLf8NJBy-cBDaiDJujxJs",
          "y": "lR2hFOOVn-Ohk-kbRpHT3WCK_bzjT5x25dRtE70Y0kA",
          "key_ops": ["verify"],
          "ext": true
        }
        """;

    /// <summary>
    /// Verifies that <paramref name="signatureBytes"/> is a valid backend signature
    /// over the canonical JSON of <paramref name="payload"/>.
    /// </summary>
    /// <param name="payload">The certificate payload object as returned by the Edge Function.</param>
    /// <param name="signatureBytes">Raw IEEE P1363-formatted ECDSA signature (64 bytes for P-256).</param>
    /// <returns>True iff the signature checks out under the embedded public key.</returns>
    public static bool VerifyPayloadSignature(JsonElement payload, byte[] signatureBytes)
    {
        if (payload.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (signatureBytes is null || signatureBytes.Length == 0)
        {
            return false;
        }

        var canonical = CanonicalJson.Serialize(payload);
        var canonicalBytes = Encoding.UTF8.GetBytes(canonical);

        using var ecdsa = ImportPublicKey();

        // The Edge Function's `crypto.subtle.sign` returns raw r||s (IEEE P1363
        // fixed-field concatenation), 64 bytes for P-256 — NOT DER-encoded. Match
        // that format on the verify side or every signature will look invalid.
        return ecdsa.VerifyData(
            canonicalBytes,
            signatureBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// Convenience overload that decodes <paramref name="signatureBase64"/> and
    /// returns false if the input is not valid Base64 instead of throwing.
    /// </summary>
    public static bool VerifyPayloadSignature(JsonElement payload, string signatureBase64)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64))
        {
            return false;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException)
        {
            return false;
        }

        return VerifyPayloadSignature(payload, signatureBytes);
    }

    private static ECDsa ImportPublicKey()
    {
        // Parse the embedded JWK manually so we do not need a JWK NuGet dependency.
        // P-256 fixed: 32-byte X and Y, no D (public-only).
        using var jwk = JsonDocument.Parse(EmbeddedJwk);
        var root = jwk.RootElement;

        var crv = root.GetProperty("crv").GetString();
        if (!string.Equals(crv, "P-256", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Embedded license signing key has unexpected curve '{crv}'. Expected 'P-256'.");
        }

        var x = Base64UrlDecode(root.GetProperty("x").GetString() ?? string.Empty);
        var y = Base64UrlDecode(root.GetProperty("y").GetString() ?? string.Empty);

        if (x.Length != 32 || y.Length != 32)
        {
            throw new InvalidOperationException(
                "Embedded license signing key has malformed X/Y coordinates (expected 32 bytes each for P-256).");
        }

        var parameters = new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = x,
                Y = y,
            },
        };

        return ECDsa.Create(parameters);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        // Convert Base64URL to standard Base64 then decode. Pad to a multiple of 4.
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
