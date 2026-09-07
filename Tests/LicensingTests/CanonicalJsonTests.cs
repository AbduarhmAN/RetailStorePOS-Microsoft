using System.Text.Json;
using RetailStorePOS.UI.Common.Services;

namespace LicensingTests;

/// <summary>
/// Locks down the byte-for-byte contract between
/// <see cref="CanonicalJson"/> and the Edge Function's <c>canonicalizeJson</c>
/// in <c>supabase/functions/license-api/utils/crypto.ts</c>. If either
/// implementation drifts the certificate signature stops verifying, so these
/// tests are deliberately strict: every expected string is the exact output the
/// JS implementation would emit for the same input.
/// </summary>
[TestClass]
public sealed class CanonicalJsonTests
{
    [TestMethod]
    public void Primitive_Null_IsEmittedAsLiteralNull()
    {
        var element = JsonDocument.Parse("null").RootElement;
        Assert.AreEqual("null", CanonicalJson.Serialize(element));
    }

    [TestMethod]
    public void Primitive_Boolean_IsEmittedLowercase()
    {
        Assert.AreEqual("true", CanonicalJson.Serialize(JsonDocument.Parse("true").RootElement));
        Assert.AreEqual("false", CanonicalJson.Serialize(JsonDocument.Parse("false").RootElement));
    }

    [TestMethod]
    public void Primitive_Number_PreservesTextualForm()
    {
        Assert.AreEqual("0", CanonicalJson.Serialize(JsonDocument.Parse("0").RootElement));
        Assert.AreEqual("42", CanonicalJson.Serialize(JsonDocument.Parse("42").RootElement));
        Assert.AreEqual("3.14", CanonicalJson.Serialize(JsonDocument.Parse("3.14").RootElement));
        Assert.AreEqual("-7", CanonicalJson.Serialize(JsonDocument.Parse("-7").RootElement));
    }

    [TestMethod]
    public void Primitive_String_UsesJsonEscapes()
    {
        var element = JsonDocument.Parse("\"hello\"").RootElement;
        Assert.AreEqual("\"hello\"", CanonicalJson.Serialize(element));
    }

    [TestMethod]
    public void String_EscapingMatchesJavaScriptForUnicodeAndHtmlSensitiveText()
    {
        const string value = "\u062a\u0631\u062e\u064a\u0635 + <premium> & 'store' / \u2028\u2029\ud83d\ude00";
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { value }));

        Assert.AreEqual("{\"value\":\"" + value + "\"}", CanonicalJson.Serialize(doc.RootElement));
    }

    [TestMethod]
    public void String_UsesShortControlEscapesAndLowercaseUnicodeEscapes()
    {
        const string value = "\"\\\b\f\n\r\t\u0000\u001a";
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { value }));

        Assert.AreEqual("{\"value\":\"\\\"\\\\\\b\\f\\n\\r\\t\\u0000\\u001a\"}",
            CanonicalJson.Serialize(doc.RootElement));
    }

    [TestMethod]
    public void Object_Keys_AreSortedOrdinal()
    {
        var element = JsonDocument.Parse("{\"b\":1,\"a\":2}").RootElement;
        Assert.AreEqual("{\"a\":2,\"b\":1}", CanonicalJson.Serialize(element));
    }

    [TestMethod]
    public void Object_NullValues_AreKept()
    {
        var element = JsonDocument.Parse("{\"a\":null,\"b\":1}").RootElement;
        Assert.AreEqual("{\"a\":null,\"b\":1}", CanonicalJson.Serialize(element));
    }

    [TestMethod]
    public void Array_PreservesOrder_AndHasNoWhitespace()
    {
        var element = JsonDocument.Parse("[3,1,2]").RootElement;
        Assert.AreEqual("[3,1,2]", CanonicalJson.Serialize(element));
    }

    [TestMethod]
    public void Nested_Object_IsRecursivelyCanonicalized()
    {
        var element = JsonDocument.Parse(
            "{\"outer\":{\"z\":1,\"a\":[2,1]},\"alpha\":true}").RootElement;

        Assert.AreEqual(
            "{\"alpha\":true,\"outer\":{\"a\":[2,1],\"z\":1}}",
            CanonicalJson.Serialize(element));
    }

    /// <summary>
    /// Shape-equivalent of the certificate payload the Edge Function signs.
    /// Field set, types, and values are aligned with
    /// <c>handlers/verify-license.ts:handleVerifyLicense</c> so a future regression
    /// shows up as a failure here, not as a mysterious signature mismatch in
    /// production.
    /// </summary>
    [TestMethod]
    public void RealisticCertificatePayload_MatchesExpectedCanonicalOutput()
    {
        // Keys deliberately out of alphabetical order in the input so sorting
        // actually does something during canonicalization.
        var input = """
            {
              "messageType": "license_activation_certificate",
              "certificateVersion": 1,
              "activationCertificateId": "11111111-2222-3333-4444-555555555555",
              "licenseId": "lic-001",
              "activationId": "act-001",
              "installId": "INSTALL_TEST",
              "productCode": "RETAILSTOREPOS",
              "permissionGroup": "PREMIUM",
              "features": ["AdvancedReports", "AdvancedReports.Basket"],
              "licenseStatus": "active",
              "activationStatus": "active",
              "issuedAtUtc": "2026-05-19T10:00:00.000Z",
              "notBeforeUtc": "2026-05-19T10:00:00.000Z",
              "expiresAtUtc": "2026-06-19T10:00:00.000Z",
              "requestNonce": null,
              "requestSequence": 7
            }
            """;

        const string expected =
            "{\"activationCertificateId\":\"11111111-2222-3333-4444-555555555555\"," +
            "\"activationId\":\"act-001\"," +
            "\"activationStatus\":\"active\"," +
            "\"certificateVersion\":1," +
            "\"expiresAtUtc\":\"2026-06-19T10:00:00.000Z\"," +
            "\"features\":[\"AdvancedReports\",\"AdvancedReports.Basket\"]," +
            "\"installId\":\"INSTALL_TEST\"," +
            "\"issuedAtUtc\":\"2026-05-19T10:00:00.000Z\"," +
            "\"licenseId\":\"lic-001\"," +
            "\"licenseStatus\":\"active\"," +
            "\"messageType\":\"license_activation_certificate\"," +
            "\"notBeforeUtc\":\"2026-05-19T10:00:00.000Z\"," +
            "\"permissionGroup\":\"PREMIUM\"," +
            "\"productCode\":\"RETAILSTOREPOS\"," +
            "\"requestNonce\":null," +
            "\"requestSequence\":7}";

        using var doc = JsonDocument.Parse(input);
        Assert.AreEqual(expected, CanonicalJson.Serialize(doc.RootElement));
    }
}
