using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RetailStorePOS.UI.Common.Services;

internal sealed record DeviceChallengeExpectation(
    string InstallId,
    string Thumbprint,
    string RequestNonce,
    long RequestSequence,
    string Mode);

internal static class DeviceChallengeProtocol
{
    internal const string Audience = "nexill-license-device-v2";
    private const long MaxSafeSequence = 9_007_199_254_740_991L;
    private static readonly string[] PayloadFields =
    {
        "audience", "challengeId", "devicePublicKeyThumbprint", "expiresAtUtc", "installId",
        "issuedAtUtc", "messageType", "mode", "nonce", "productCode", "protocolVersion",
        "requestNonce", "requestSequence",
    };

    internal static string? ValidateServerEnvelope(
        JsonElement envelope, DeviceChallengeExpectation expected, DateTimeOffset now, out JsonElement payload)
    {
        payload = default;
        if (!HasExactFields(envelope, new[] { "success", "payload", "backendSignature", "backendSignatureAlgorithm", "keyId" })
            || envelope.GetProperty("success").ValueKind != JsonValueKind.True
            || ReadString(envelope, "backendSignatureAlgorithm") != PublicLicenseKey.ExpectedAlgorithm
            || ReadString(envelope, "keyId") != PublicLicenseKey.KeyId)
            return "local_device_challenge_envelope_invalid";

        var candidate = envelope.GetProperty("payload");
        var failure = ValidatePayload(candidate, expected, now);
        if (failure is not null) return failure;
        if (!PublicLicenseKey.VerifyPayloadSignature(candidate, ReadString(envelope, "backendSignature") ?? string.Empty))
            return "local_device_challenge_signature_invalid";

        payload = candidate.Clone();
        return null;
    }

    internal static string? ValidatePayload(JsonElement payload, DeviceChallengeExpectation expected, DateTimeOffset now)
    {
        if (!HasExactFields(payload, PayloadFields)) return "local_device_challenge_fields_invalid";
        if (string.IsNullOrWhiteSpace(expected.InstallId) || expected.InstallId.Length > 256
            || expected.InstallId.Trim() != expected.InstallId
            || !Matches(expected.Thumbprint, "^[A-Za-z0-9_-]{43}$")
            || !Matches(expected.RequestNonce, "^[0-9a-f]{32}$")
            || expected.RequestSequence < 1 || expected.RequestSequence > MaxSafeSequence
            || expected.Mode is not ("enroll" or "refresh"))
            return "local_device_challenge_context_invalid";

        if (ReadString(payload, "audience") != Audience
            || ReadString(payload, "messageType") != "license_device_challenge"
            || ReadString(payload, "productCode") != "RETAILSTOREPOS"
            || !ReadInteger(payload, "protocolVersion", out var version) || version != 2)
            return "local_device_challenge_protocol_mismatch";

        if (ReadString(payload, "installId") != expected.InstallId
            || ReadString(payload, "devicePublicKeyThumbprint") != expected.Thumbprint
            || ReadString(payload, "requestNonce") != expected.RequestNonce
            || ReadString(payload, "mode") != expected.Mode)
            return "local_device_challenge_binding_mismatch";

        // A signed server challenge may resynchronize a lost local counter.
        if (!ReadInteger(payload, "requestSequence", out var sequence)
            || sequence < expected.RequestSequence || sequence > MaxSafeSequence)
            return "local_device_challenge_sequence_invalid";

        if (!Matches(ReadString(payload, "challengeId"), "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")
            || !Matches(ReadString(payload, "nonce"), "^[0-9a-f]{64}$"))
            return "local_device_challenge_nonce_invalid";

        if (!ReadTimestamp(payload, "issuedAtUtc", out var issued)
            || !ReadTimestamp(payload, "expiresAtUtc", out var expires)
            || issued - now > TimeSpan.FromSeconds(30) || expires <= now
            || expires <= issued || expires - issued > TimeSpan.FromMinutes(2))
            return "local_device_challenge_expired_or_invalid";

        return null;
    }

    private static bool HasExactFields(JsonElement value, IReadOnlyCollection<string> required)
    {
        if (value.ValueKind != JsonValueKind.Object) return false;
        var remaining = new HashSet<string>(required, StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!remaining.Remove(property.Name)) return false;
        return remaining.Count == 0;
    }

    private static string? ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() : null;

    private static bool ReadInteger(JsonElement value, string name, out long result)
    {
        result = 0;
        return value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt64(out result);
    }

    private static bool ReadTimestamp(JsonElement value, string name, out DateTimeOffset result) =>
        DateTimeOffset.TryParseExact(ReadString(value, name), "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out result);

    private static bool Matches(string? value, string pattern)
    {
        if (value is null) return false;
        var match = Regex.Match(value, pattern, RegexOptions.CultureInvariant);
        return match.Success && match.Length == value.Length;
    }
}
