using System.Security.Cryptography;
using System.Text.Json;
using RetailStorePOS.UI.Common.Services;

namespace LicensingTests;

[TestClass]
public sealed class DeviceKeyServiceTests
{
    [TestMethod]
    public void BuildPublicIdentity_ProducesP256JwkAndRfc7638Thumbprint()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var identity = DeviceKeyService.BuildPublicIdentity(
            signer.ExportParameters(includePrivateParameters: false),
            isHardwareBacked: false);

        Assert.AreEqual("EC", identity.PublicKey.KeyType);
        Assert.AreEqual("P-256", identity.PublicKey.Curve);
        Assert.AreEqual(32, FromBase64Url(identity.PublicKey.X).Length);
        Assert.AreEqual(32, FromBase64Url(identity.PublicKey.Y).Length);
        Assert.AreEqual(32, FromBase64Url(identity.Thumbprint).Length);
        Assert.IsFalse(identity.IsHardwareBacked);
    }

    [TestMethod]
    public void SignCanonicalJson_ProducesVerifiableP1363Signature()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var document = JsonDocument.Parse("""
            {"z":2,"a":{"y":true,"x":"value"}}
            """);

        var signature = Convert.FromBase64String(
            DeviceKeyService.SignCanonicalJson(signer, document.RootElement));
        var canonical = CanonicalJson.Serialize(document.RootElement);

        Assert.AreEqual(64, signature.Length);
        Assert.IsTrue(signer.VerifyData(
            System.Text.Encoding.UTF8.GetBytes(canonical),
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    [TestMethod]
    public void ComputeThumbprint_MatchesFixedP256GeneratorVector()
    {
        var jwk = new DevicePublicKeyJwk(
            "EC",
            "P-256",
            "axfR8uEsQkf4vOblY6RA8ncDfYEt6zOg9KE5RdiYwpY",
            "T-NC4v4af5uO5-tKfA-eFivOM1drMV7Oy7ZAaDe_UfU");

        Assert.AreEqual(
            "xx0BcA-wMohw8atYDJOe6peGModklG2wRHBlXHMvl0M",
            DeviceKeyService.ComputeThumbprint(jwk));
    }

    [TestMethod]
    public void ComputeThumbprint_RejectsMalformedOrWrongCurveKey()
    {
        var jwk = new DevicePublicKeyJwk("EC", "P-256", "invalid", "invalid");
        Assert.ThrowsExactly<CryptographicException>(() => DeviceKeyService.ComputeThumbprint(jwk));
        Assert.ThrowsExactly<CryptographicException>(() => DeviceKeyService.ComputeThumbprint(
            jwk with { Curve = "P-384" }));
    }

    [TestMethod]
    public void SignCanonicalJson_RejectsTamperedPayloadWithOriginalSignature()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var original = JsonDocument.Parse("{\"requestNonce\":\"original\"}");
        using var tampered = JsonDocument.Parse("{\"requestNonce\":\"replacement\"}");
        var signature = Convert.FromBase64String(
            DeviceKeyService.SignCanonicalJson(signer, original.RootElement));

        Assert.IsFalse(signer.VerifyData(
            System.Text.Encoding.UTF8.GetBytes(CanonicalJson.Serialize(tampered.RootElement)),
            signature,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    }

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded += new string('=', (4 - padded.Length % 4) % 4);
        return Convert.FromBase64String(padded);
    }
}
