using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RetailStorePOS.App.Services.Licensing;

/// <summary>
/// Reasons an activation attempt or local-cache load can fail. Exposed to the
/// UI/ViewModel so the user sees a specific message rather than "something
/// went wrong". Keep <see cref="Activated"/> as the only success value.
/// </summary>
public enum LicenseActivationOutcome
{
    Activated,
    /// <summary>The HTTP call could not reach the backend at all.</summary>
    NetworkFailure,
    /// <summary>The backend returned a non-success body (license_not_found, expired, etc.).</summary>
    BackendRejected,
    /// <summary>The backend response shape was malformed or missing required fields.</summary>
    MalformedResponse,
    /// <summary>The signature on the certificate did not verify under the embedded public key.</summary>
    SignatureInvalid,
    /// <summary>The certificate verified but a field check (installId/productCode/expiry) failed.</summary>
    CertificateRejected,
    /// <summary>The local Supabase URL/key were not configured, so we cannot call the backend.</summary>
    BackendNotConfigured,
    /// <summary>Caller passed an empty or obviously malformed license key.</summary>
    InvalidLicenseKey,
}

/// <summary>
/// Final result of an activation or load attempt. <see cref="Snapshot"/> is non-null
/// only when <see cref="Outcome"/> is <see cref="LicenseActivationOutcome.Activated"/>.
/// <see cref="ErrorCode"/> mirrors the backend's <c>errorCode</c> when present, or a
/// local <c>local_*</c> code when the failure happened on the device.
/// </summary>
public sealed record LicenseActivationResult(
    LicenseActivationOutcome Outcome,
    string? ErrorCode,
    LicenseActivationSnapshot? Snapshot)
{
    public bool IsActivated => Outcome == LicenseActivationOutcome.Activated && Snapshot is not null;

    internal static LicenseActivationResult Success(LicenseActivationSnapshot snapshot)
        => new(LicenseActivationOutcome.Activated, null, snapshot);

    internal static LicenseActivationResult Failure(LicenseActivationOutcome outcome, string? errorCode)
        => new(outcome, errorCode, null);
}

/// <summary>
/// The minimal, validated view of a license activation that the rest of the app
/// needs in order to make feature-access decisions. Built from a verified
/// certificate; does not retain the raw signature blob.
/// </summary>
public sealed record LicenseActivationSnapshot(
    string CertificateId,
    string LicenseId,
    string ActivationId,
    string InstallId,
    string ProductCode,
    string PermissionGroup,
    IReadOnlyList<string> Features,
    string LicenseStatus,
    string ActivationStatus,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset NotBeforeUtc,
    DateTimeOffset ExpiresAtUtc)
{
    /// <summary>True when the certificate's <see cref="ExpiresAtUtc"/> is in the future
    /// AND <see cref="NotBeforeUtc"/> is not in the future relative to <paramref name="now"/>.</summary>
    public bool IsCurrentlyValid(DateTimeOffset now) =>
        ExpiresAtUtc > now &&
        NotBeforeUtc <= now &&
        IsActiveStatus(LicenseStatus) &&
        IsActiveStatus(ActivationStatus);

    public bool HasFeature(string featureCode)
    {
        if (string.IsNullOrWhiteSpace(featureCode) || Features is null)
        {
            return false;
        }

        for (var i = 0; i < Features.Count; i++)
        {
            if (string.Equals(Features[i], featureCode, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsActiveStatus(string status)
        => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "trial", StringComparison.OrdinalIgnoreCase);
}

// ---------- Wire-format DTOs ----------
//
// These mirror the JSON the Edge Function emits. They are kept internal so the
// rest of the app talks to LicenseActivationSnapshot, never the raw response.

internal sealed record LicenseApiEnvelope
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("payload")]
    public LicenseCertificatePayload? Payload { get; init; }

    [JsonPropertyName("backendSignature")]
    public string? BackendSignature { get; init; }

    [JsonPropertyName("backendSignatureAlgorithm")]
    public string? BackendSignatureAlgorithm { get; init; }

    [JsonPropertyName("keyId")]
    public string? KeyId { get; init; }
}

internal sealed record LicenseCertificatePayload
{
    [JsonPropertyName("messageType")]
    public string? MessageType { get; init; }

    [JsonPropertyName("certificateVersion")]
    public int CertificateVersion { get; init; }

    [JsonPropertyName("activationCertificateId")]
    public string? ActivationCertificateId { get; init; }

    [JsonPropertyName("licenseId")]
    public string? LicenseId { get; init; }

    [JsonPropertyName("activationId")]
    public string? ActivationId { get; init; }

    [JsonPropertyName("installId")]
    public string? InstallId { get; init; }

    [JsonPropertyName("productCode")]
    public string? ProductCode { get; init; }

    [JsonPropertyName("permissionGroup")]
    public string? PermissionGroup { get; init; }

    [JsonPropertyName("features")]
    public List<string>? Features { get; init; }

    [JsonPropertyName("licenseStatus")]
    public string? LicenseStatus { get; init; }

    [JsonPropertyName("activationStatus")]
    public string? ActivationStatus { get; init; }

    [JsonPropertyName("issuedAtUtc")]
    public string? IssuedAtUtc { get; init; }

    [JsonPropertyName("notBeforeUtc")]
    public string? NotBeforeUtc { get; init; }

    [JsonPropertyName("expiresAtUtc")]
    public string? ExpiresAtUtc { get; init; }

    [JsonPropertyName("requestNonce")]
    public string? RequestNonce { get; init; }

    [JsonPropertyName("requestSequence")]
    public long? RequestSequence { get; init; }
}

internal sealed record LicenseActivationRequest
{
    [JsonPropertyName("action")]
    public string Action { get; init; } = "verify_license";

    [JsonPropertyName("messageType")]
    public string MessageType { get; init; } = "license_activation_request";

    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = 1;

    [JsonPropertyName("productCode")]
    public string ProductCode { get; init; } = "RETAILSTOREPOS";

    [JsonPropertyName("installId")]
    public string InstallId { get; init; } = string.Empty;

    [JsonPropertyName("licenseKey")]
    public string LicenseKey { get; init; } = string.Empty;

    [JsonPropertyName("devicePublicKeyThumbprint")]
    public string? DevicePublicKeyThumbprint { get; init; }

    [JsonPropertyName("requestNonce")]
    public string RequestNonce { get; init; } = string.Empty;

    [JsonPropertyName("requestSequence")]
    public long RequestSequence { get; init; }

    [JsonPropertyName("requestTimeUtc")]
    public string RequestTimeUtc { get; init; } = string.Empty;
}

/// <summary>Persisted on disk so we can re-verify the signature on startup.</summary>
internal sealed record StoredCertificate
{
    [JsonPropertyName("payload")]
    public LicenseCertificatePayload? Payload { get; init; }

    [JsonPropertyName("backendSignature")]
    public string? BackendSignature { get; init; }

    [JsonPropertyName("backendSignatureAlgorithm")]
    public string? BackendSignatureAlgorithm { get; init; }

    [JsonPropertyName("keyId")]
    public string? KeyId { get; init; }

    [JsonPropertyName("storedAtUtc")]
    public string StoredAtUtc { get; init; } = string.Empty;
}

[JsonSourceGenerationOptions(
    WriteIndented = false,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LicenseApiEnvelope))]
[JsonSerializable(typeof(LicenseCertificatePayload))]
[JsonSerializable(typeof(LicenseActivationRequest))]
[JsonSerializable(typeof(StoredCertificate))]
[JsonSerializable(typeof(EncryptionEnvelopeDto))]
internal sealed partial class LicensingJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Wire format for the encryption envelope — must stay in sync with
/// <c>supabase/functions/license-api/utils/envelope.ts</c>.
///
/// The inner request JSON is encrypted with AES-GCM using a fresh per-request
/// session key. The session key is wrapped with the backend's RSA-OAEP-SHA256
/// public key, identified by <see cref="KeyId"/>. Field names are camelCase
/// for byte-for-byte parity with the backend decryptor.
/// </summary>
internal sealed record EncryptionEnvelopeDto
{
    [JsonPropertyName("messageType")]
    public string MessageType { get; init; } = "encrypted_license_verification_envelope";

    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; init; } = 1;

    [JsonPropertyName("keyId")]
    public string KeyId { get; init; } = string.Empty;

    [JsonPropertyName("encryptedContentKey")]
    public string EncryptedContentKey { get; init; } = string.Empty;

    [JsonPropertyName("nonce")]
    public string Nonce { get; init; } = string.Empty;

    [JsonPropertyName("ciphertext")]
    public string Ciphertext { get; init; } = string.Empty;

    [JsonPropertyName("authenticationTag")]
    public string AuthenticationTag { get; init; } = string.Empty;
}
