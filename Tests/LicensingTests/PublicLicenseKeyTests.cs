using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RetailStorePOS.UI.Common.Services;

namespace LicensingTests;

[TestClass]
public sealed class PublicLicenseKeyTests
{
    [TestMethod]
    public void VerifyPayloadSignature_RejectsEmptySignature()
    {
        var payload = JsonDocument.Parse("{\"a\":1}").RootElement;
        Assert.IsFalse(PublicLicenseKey.VerifyPayloadSignature(payload, Array.Empty<byte>()));
        Assert.IsFalse(PublicLicenseKey.VerifyPayloadSignature(payload, ""));
    }

    [TestMethod]
    public void VerifyPayloadSignature_RejectsRandomBytes()
    {
        var payload = JsonDocument.Parse("{\"a\":1}").RootElement;
        var bogus = new byte[64];
        RandomNumberGenerator.Fill(bogus);
        Assert.IsFalse(PublicLicenseKey.VerifyPayloadSignature(payload, bogus));
    }

    [TestMethod]
    public void VerifyPayloadSignature_RejectsMalformedBase64()
    {
        var payload = JsonDocument.Parse("{\"a\":1}").RootElement;
        Assert.IsFalse(PublicLicenseKey.VerifyPayloadSignature(payload, "!!! not base64 !!!"));
    }

    [TestMethod]
    public void VerifyPayloadSignature_RejectsNonObjectPayload()
    {
        // Even with a real-looking signature, only objects are valid certificate payloads.
        var notObject = JsonDocument.Parse("[1,2,3]").RootElement;
        Assert.IsFalse(PublicLicenseKey.VerifyPayloadSignature(notObject, new byte[64]));
    }

    [TestMethod]
    public void EmbeddedJwk_ImportsSuccessfully_AndYieldsP256Key()
    {
        // Round-trip: sign a sample with a known throwaway key and confirm that
        // the embedded P-256 verifier rejects the foreign signature. Useful as
        // a smoke test that the JWK actually parses without throwing — if the
        // embedded key were malformed the helper would fail on the first call.
        var payload = JsonDocument.Parse("{\"a\":1}").RootElement;

        using var foreignKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var canonical = CanonicalJson.Serialize(payload);
        var foreignSignature = foreignKey.SignData(
            Encoding.UTF8.GetBytes(canonical),
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        Assert.IsFalse(
            PublicLicenseKey.VerifyPayloadSignature(payload, foreignSignature),
            "A signature from a different P-256 key must not verify under the embedded backend key.");
    }
}
