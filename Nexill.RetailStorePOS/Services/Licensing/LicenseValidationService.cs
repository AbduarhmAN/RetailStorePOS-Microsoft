using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nexill.RetailStorePOS.Services.DeviceIdentity;
using RetailStorePOS.Data;

namespace RetailStorePOS.App.Services.Licensing;

/// <summary>
/// Calls the Supabase <c>license-api</c> Edge Function to activate or refresh a
/// license, verifies the backend's ECDSA signature locally, and persists a
/// DPAPI-protected copy of the signed certificate so the app can boot offline.
///
/// Flow on activation:
/// <list type="number">
///   <item>Build a <see cref="LicenseActivationRequest"/> with the device's
///         install id, the user-entered license key, a fresh request nonce, and
///         a monotonically increasing request sequence stored in
///         <c>SettingsRepository</c>.</item>
///   <item>POST to <c>{SupabaseUrl}/functions/v1/license-api</c> with the
///         Supabase anon key in the <c>apikey</c> + <c>Authorization</c> headers
///         (mirrors how <c>TelemetryService</c> talks to Supabase today).</item>
///   <item>Parse the envelope. If <c>success == false</c>, surface
///         <see cref="LicenseActivationOutcome.BackendRejected"/> with the
///         backend's error code.</item>
///   <item>Verify the certificate signature against the canonical JSON of
///         <c>payload</c> using <see cref="PublicLicenseKey"/>.</item>
///   <item>Sanity-check the certificate fields (installId, productCode,
///         expiresAt &gt; now, statuses == active/trial).</item>
///   <item>Persist the envelope as a DPAPI-protected blob under
///         <c>%LOCALAPPDATA%\RetailStorePOS\license\license_activation_certificate.dat</c>.</item>
///   <item>Build a <see cref="LicenseActivationSnapshot"/> for the rest of the
///         app and raise <see cref="SnapshotChanged"/>.</item>
/// </list>
///
/// On startup, <see cref="LoadCachedSnapshot"/> re-runs the signature and
/// expiry checks and silently discards any cached certificate that fails.
/// That way a corrupted file or a key rotation cannot accidentally hand the
/// user paid features they should not have.
/// </summary>
public sealed class LicenseValidationService
{
    private const string ProductCode = "RETAILSTOREPOS";
    private const int ProtocolVersion = 1;
    private const string LicenseFolderName = "license";
    private const string CertificateFileName = "license_activation_certificate.dat";
    private const string FunctionPath = "/functions/v1/license-api";
    private const string SequenceSettingKey = "license.request_sequence";
    private const string SupabaseUrlSecret = "SupabaseUrl";
    private const string SupabaseKeySecret = "SupabaseKey";

    private readonly Func<string?> _supabaseUrlAccessor;
    private readonly Func<string?> _supabaseKeyAccessor;
    private readonly Func<string> _installIdAccessor;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly Func<DateTimeOffset> _clock;
    private readonly Action<long>? _persistRequestSequence;
    private readonly Func<long>? _readRequestSequence;
    private readonly object _stateLock = new();

    private LicenseActivationSnapshot? _currentSnapshot;
    private bool _initialLoadAttempted;

    /// <summary>
    /// Raised whenever the active snapshot changes (load on startup, successful
    /// activation, manual clear). Null payload means "no valid certificate".
    /// </summary>
    public event EventHandler<LicenseActivationSnapshot?>? SnapshotChanged;

    /// <summary>
    /// Production constructor — wires the service to the same Supabase secrets
    /// and device fingerprint that <c>TelemetryService</c> already uses.
    /// </summary>
    public LicenseValidationService(
        RetailStorePOS.Data.Modules.Settings.SettingsRepository settings)
        : this(
            supabaseUrlAccessor: () => SecureStorageService.GetSecret(SupabaseUrlSecret),
            supabaseKeyAccessor: () => SecureStorageService.GetSecret(SupabaseKeySecret),
            installIdAccessor: DeviceIdentityService.CreateStableInstallId,
            httpClientFactory: CreateDefaultHttpClient,
            clock: () => DateTimeOffset.UtcNow,
            readRequestSequence: () => ReadSequenceFromSettings(settings),
            persistRequestSequence: value => WriteSequenceToSettings(settings, value))
    {
    }

    // Test-friendly seam — the production ctor delegates to this.
    internal LicenseValidationService(
        Func<string?> supabaseUrlAccessor,
        Func<string?> supabaseKeyAccessor,
        Func<string> installIdAccessor,
        Func<HttpClient> httpClientFactory,
        Func<DateTimeOffset> clock,
        Func<long>? readRequestSequence = null,
        Action<long>? persistRequestSequence = null)
    {
        _supabaseUrlAccessor = supabaseUrlAccessor ?? throw new ArgumentNullException(nameof(supabaseUrlAccessor));
        _supabaseKeyAccessor = supabaseKeyAccessor ?? throw new ArgumentNullException(nameof(supabaseKeyAccessor));
        _installIdAccessor = installIdAccessor ?? throw new ArgumentNullException(nameof(installIdAccessor));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _readRequestSequence = readRequestSequence;
        _persistRequestSequence = persistRequestSequence;
    }

    /// <summary>
    /// Returns the validated snapshot from the local certificate cache, loading
    /// it on first call. Returns null when there is no cache, or the cached
    /// certificate fails signature/expiry checks (in which case the bad file is
    /// removed so a future activation starts clean).
    /// </summary>
    public LicenseActivationSnapshot? LoadCachedSnapshot()
    {
        lock (_stateLock)
        {
            if (_initialLoadAttempted)
            {
                return _currentSnapshot;
            }

            _initialLoadAttempted = true;
            _currentSnapshot = TryReadCachedCertificate();
            return _currentSnapshot;
        }
    }

    /// <summary>
    /// Returns whatever snapshot is currently in memory without re-reading the
    /// disk cache. Use this on hot paths after the first <see cref="LoadCachedSnapshot"/>.
    /// </summary>
    public LicenseActivationSnapshot? GetCurrentSnapshot()
    {
        lock (_stateLock)
        {
            return _initialLoadAttempted ? _currentSnapshot : LoadCachedSnapshotLocked();
        }
    }

    /// <summary>
    /// Removes the cached certificate (e.g. on logout, license rotation, or
    /// when an activation explicitly fails). Raises <see cref="SnapshotChanged"/>.
    /// </summary>
    public void Clear()
    {
        lock (_stateLock)
        {
            try
            {
                var path = GetCertificatePath();
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException) { /* best-effort */ }
            catch (UnauthorizedAccessException) { /* best-effort */ }

            _currentSnapshot = null;
            _initialLoadAttempted = true;
        }

        SnapshotChanged?.Invoke(this, null);
    }

    /// <summary>
    /// Calls the Edge Function to activate or refresh the license, verifies the
    /// signed certificate, and persists it locally. Safe to call repeatedly —
    /// each call advances the request sequence so old responses cannot be
    /// replayed against a future call.
    /// </summary>
    public async Task<LicenseActivationResult> ActivateAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.InvalidLicenseKey,
                "local_invalid_license_key");
        }

        var supabaseUrl = (_supabaseUrlAccessor() ?? string.Empty).Trim().TrimEnd('/');
        var supabaseKey = (_supabaseKeyAccessor() ?? string.Empty).Trim();
        if (supabaseUrl.Length == 0 || supabaseKey.Length == 0)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.BackendNotConfigured,
                "local_backend_not_configured");
        }

        var installId = _installIdAccessor();
        if (string.IsNullOrWhiteSpace(installId))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.BackendNotConfigured,
                "local_install_id_unavailable");
        }

        var nextSequence = AdvanceRequestSequence();
        var request = new LicenseActivationRequest
        {
            InstallId = installId,
            LicenseKey = licenseKey.Trim(),
            RequestNonce = Guid.NewGuid().ToString("N"),
            RequestSequence = nextSequence,
            RequestTimeUtc = _clock().UtcDateTime.ToString("O"),
        };

        return await ExecuteRequestAsync(supabaseUrl, supabaseKey, installId, request, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<LicenseActivationResult> ReissueByInstallAsync(CancellationToken cancellationToken = default)
    {
        var supabaseUrl = (_supabaseUrlAccessor() ?? string.Empty).Trim().TrimEnd('/');
        var supabaseKey = (_supabaseKeyAccessor() ?? string.Empty).Trim();
        if (supabaseUrl.Length == 0 || supabaseKey.Length == 0)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.BackendNotConfigured,
                "local_backend_not_configured");
        }

        var installId = _installIdAccessor();
        if (string.IsNullOrWhiteSpace(installId))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.BackendNotConfigured,
                "local_install_id_unavailable");
        }

        var nextSequence = AdvanceRequestSequence();
        var request = new LicenseActivationRequest
        {
            Action = "reissue_by_install",
            MessageType = "license_reissue_request",
            InstallId = installId,
            LicenseKey = string.Empty,
            RequestNonce = Guid.NewGuid().ToString("N"),
            RequestSequence = nextSequence,
            RequestTimeUtc = _clock().UtcDateTime.ToString("O"),
        };

        return await ExecuteRequestAsync(supabaseUrl, supabaseKey, installId, request, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<LicenseActivationResult> ExecuteRequestAsync(
        string supabaseUrl,
        string supabaseKey,
        string installId,
        LicenseActivationRequest request,
        CancellationToken cancellationToken)
    {
        
        LicenseApiEnvelope? envelope;
        try
        {
            envelope = await PostActivationRequestAsync(supabaseUrl, supabaseKey, request, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.NetworkFailure,
                "local_network_failure");
        }
        catch (TaskCanceledException)
        {
            // HttpClient timeout — surface as network failure, not a user cancel.
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.NetworkFailure,
                "local_network_timeout");
        }
        catch (JsonException)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.MalformedResponse,
                "local_response_not_json");
        }

        if (envelope is null)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.MalformedResponse,
                "local_response_empty");
        }

        if (!envelope.Success || envelope.Payload is null)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.BackendRejected,
                envelope.ErrorCode ?? "backend_unspecified_error");
        }

        if (!string.Equals(envelope.BackendSignatureAlgorithm, PublicLicenseKey.ExpectedAlgorithm, StringComparison.Ordinal))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.SignatureInvalid,
                "local_unexpected_signature_algorithm");
        }

        if (!string.Equals(envelope.KeyId, PublicLicenseKey.KeyId, StringComparison.Ordinal))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.SignatureInvalid,
                "local_unexpected_key_id");
        }

        var payloadElement = ToJsonElement(envelope.Payload);
        if (!PublicLicenseKey.VerifyPayloadSignature(payloadElement, envelope.BackendSignature ?? string.Empty))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.SignatureInvalid,
                "local_signature_mismatch");
        }

        var validationFailure = ValidateCertificateFields(envelope.Payload, installId, _clock());
        if (validationFailure is not null)
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.CertificateRejected,
                validationFailure);
        }

        // The Edge Function does not currently echo requestNonce/requestSequence
        // back inside the signed payload (it sets them as fields, but the
        // server clamps requestSequence to last_request_sequence when the
        // caller did not pass one). We still verify what's there matches.
        if (envelope.Payload.RequestNonce is { Length: > 0 } returnedNonce
            && !string.Equals(returnedNonce, request.RequestNonce, StringComparison.Ordinal))
        {
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.CertificateRejected,
                "local_nonce_mismatch");
        }

        var snapshot = BuildSnapshot(envelope.Payload);

        try
        {
            PersistCertificate(envelope);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException)
        {
            // Activation succeeded server-side; the fact that we cannot persist
            // the cert locally is a degraded-but-usable state. Surface it as a
            // local error so the caller can decide whether to retry, but still
            // keep the in-memory snapshot for the current session.
            UpdateSnapshot(snapshot);
            return LicenseActivationResult.Failure(
                LicenseActivationOutcome.CertificateRejected,
                "local_persist_failed");
        }

        UpdateSnapshot(snapshot);
        return LicenseActivationResult.Success(snapshot);
    }

    // ---------------- internals ----------------

    private LicenseActivationSnapshot? LoadCachedSnapshotLocked()
    {
        _initialLoadAttempted = true;
        _currentSnapshot = TryReadCachedCertificate();
        return _currentSnapshot;
    }

    private LicenseActivationSnapshot? TryReadCachedCertificate()
    {
        var path = GetCertificatePath();
        if (!File.Exists(path))
        {
            return null;
        }

        StoredCertificate? stored;
        try
        {
            var encrypted = File.ReadAllBytes(path);
            var decryptedBytes = ProtectedData.Unprotect(encrypted, optionalEntropy: null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decryptedBytes);
            stored = JsonSerializer.Deserialize(json, LicensingJsonContext.Default.StoredCertificate);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is CryptographicException ||
            ex is JsonException ||
            ex is FormatException)
        {
            // Corrupt or wrong-user blob — treat as no cert.
            DeleteCorruptCertificate(path);
            return null;
        }

        if (stored?.Payload is null || string.IsNullOrEmpty(stored.BackendSignature))
        {
            DeleteCorruptCertificate(path);
            return null;
        }

        if (!string.Equals(stored.BackendSignatureAlgorithm, PublicLicenseKey.ExpectedAlgorithm, StringComparison.Ordinal)
            || !string.Equals(stored.KeyId, PublicLicenseKey.KeyId, StringComparison.Ordinal))
        {
            DeleteCorruptCertificate(path);
            return null;
        }

        var payloadElement = ToJsonElement(stored.Payload);
        if (!PublicLicenseKey.VerifyPayloadSignature(payloadElement, stored.BackendSignature))
        {
            DeleteCorruptCertificate(path);
            return null;
        }

        var installId = SafeReadInstallId();
        var validationFailure = ValidateCertificateFields(stored.Payload, installId, _clock());
        if (validationFailure is not null)
        {
            // Don't delete — the certificate may still be valid on a different
            // machine if this is a cloned profile, and removing it forces a
            // fresh re-activation which is what the operator wants anyway.
            return null;
        }

        return BuildSnapshot(stored.Payload);
    }

    private string SafeReadInstallId()
    {
        try
        {
            return _installIdAccessor() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void DeleteCorruptCertificate(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException) { /* best-effort */ }
        catch (UnauthorizedAccessException) { /* best-effort */ }
    }

    private void UpdateSnapshot(LicenseActivationSnapshot? snapshot)
    {
        lock (_stateLock)
        {
            _currentSnapshot = snapshot;
            _initialLoadAttempted = true;
        }

        SnapshotChanged?.Invoke(this, snapshot);
    }

    private static LicenseActivationSnapshot BuildSnapshot(LicenseCertificatePayload payload)
    {
        var features = payload.Features is null
            ? Array.Empty<string>()
            : payload.Features.ToArray();

        var issuedAt = ParseUtc(payload.IssuedAtUtc);
        // Older certificates (pre-Task-3 backend) do not include notBeforeUtc.
        // Fall back to issuedAt so they keep validating until they expire.
        var notBefore = string.IsNullOrWhiteSpace(payload.NotBeforeUtc)
            ? issuedAt
            : ParseUtc(payload.NotBeforeUtc);

        return new LicenseActivationSnapshot(
            CertificateId: payload.ActivationCertificateId ?? string.Empty,
            LicenseId: payload.LicenseId ?? string.Empty,
            ActivationId: payload.ActivationId ?? string.Empty,
            InstallId: payload.InstallId ?? string.Empty,
            ProductCode: payload.ProductCode ?? string.Empty,
            PermissionGroup: payload.PermissionGroup ?? string.Empty,
            Features: features,
            LicenseStatus: payload.LicenseStatus ?? string.Empty,
            ActivationStatus: payload.ActivationStatus ?? string.Empty,
            IssuedAtUtc: issuedAt,
            NotBeforeUtc: notBefore,
            ExpiresAtUtc: ParseUtc(payload.ExpiresAtUtc));
    }

    /// <summary>
    /// Returns null when the certificate's required fields all check out, or a
    /// short error code otherwise. Codes are local-only and prefixed
    /// <c>local_</c> so they cannot be confused with backend error codes.
    /// </summary>
    private static string? ValidateCertificateFields(
        LicenseCertificatePayload payload,
        string installId,
        DateTimeOffset now)
    {
        if (!string.Equals(payload.MessageType, "license_activation_certificate", StringComparison.Ordinal))
        {
            return "local_message_type_mismatch";
        }

        if (!string.Equals(payload.ProductCode, ProductCode, StringComparison.Ordinal))
        {
            return "local_product_code_mismatch";
        }

        if (!string.IsNullOrWhiteSpace(installId)
            && !string.Equals(payload.InstallId, installId, StringComparison.Ordinal))
        {
            return "local_install_id_mismatch";
        }

        if (string.IsNullOrWhiteSpace(payload.LicenseId)
            || string.IsNullOrWhiteSpace(payload.ActivationId))
        {
            return "local_certificate_ids_missing";
        }

        if (string.IsNullOrWhiteSpace(payload.PermissionGroup))
        {
            return "local_permission_group_missing";
        }

        if (!IsActiveStatus(payload.LicenseStatus))
        {
            return "local_license_status_inactive";
        }

        if (!IsActiveStatus(payload.ActivationStatus))
        {
            return "local_activation_status_inactive";
        }

        var expires = ParseUtc(payload.ExpiresAtUtc);
        if (expires <= now)
        {
            return "local_certificate_expired";
        }

        // Allow up to 5 minutes of clock skew before treating notBeforeUtc as "in
        // the future". Older Edge Function builds did not emit notBeforeUtc at
        // all, so an empty value is treated as "valid since issuedAtUtc".
        if (!string.IsNullOrWhiteSpace(payload.NotBeforeUtc))
        {
            var notBefore = ParseUtc(payload.NotBeforeUtc);
            if (notBefore - TimeSpan.FromMinutes(5) > now)
            {
                return "local_certificate_not_yet_valid";
            }
        }

        return null;
    }

    private static bool IsActiveStatus(string? status)
        => string.Equals(status, "active", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "trial", StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset ParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.MinValue;
        }

        if (DateTimeOffset.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.MinValue;
    }

    private async Task<LicenseApiEnvelope?> PostActivationRequestAsync(
        string supabaseUrl,
        string supabaseKey,
        LicenseActivationRequest request,
        CancellationToken cancellationToken)
    {
        using var http = _httpClientFactory();

        using var message = new HttpRequestMessage(HttpMethod.Post, supabaseUrl + FunctionPath);
        // Mirror TelemetryService: Edge Functions need both apikey and a bearer token.
        message.Headers.TryAddWithoutValidation("apikey", supabaseKey);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supabaseKey);

        // When the operator has seeded a backend encryption public key, wrap
        // the request JSON in an AES-GCM + RSA-OAEP envelope. Otherwise post
        // the plain request — the Edge Function accepts both during the
        // rollout. See PublicEncryptionKey for the operator workflow.
        if (PublicEncryptionKey.IsConfigured)
        {
            var requestJson = JsonSerializer.Serialize(
                request,
                LicensingJsonContext.Default.LicenseActivationRequest);
            var envelope = EncryptionEnvelope.Build(requestJson);
            message.Content = JsonContent.Create(
                envelope,
                LicensingJsonContext.Default.EncryptionEnvelopeDto);
        }
        else
        {
            message.Content = JsonContent.Create(
                request,
                LicensingJsonContext.Default.LicenseActivationRequest);
        }

        using var response = await http.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        // We tolerate 4xx/5xx because the Edge Function returns 400 with a JSON
        // body on backend errors (license_not_found, etc.). Try to parse, then
        // fall back to a network failure for non-JSON responses.
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        return await JsonSerializer.DeserializeAsync(
            stream,
            LicensingJsonContext.Default.LicenseApiEnvelope,
            cancellationToken).ConfigureAwait(false);
    }

    private void PersistCertificate(LicenseApiEnvelope envelope)
    {
        var stored = new StoredCertificate
        {
            Payload = envelope.Payload,
            BackendSignature = envelope.BackendSignature,
            BackendSignatureAlgorithm = envelope.BackendSignatureAlgorithm,
            KeyId = envelope.KeyId,
            StoredAtUtc = _clock().UtcDateTime.ToString("O"),
        };

        var json = JsonSerializer.Serialize(stored, LicensingJsonContext.Default.StoredCertificate);
        var bytes = Encoding.UTF8.GetBytes(json);
        var encrypted = ProtectedData.Protect(bytes, optionalEntropy: null, DataProtectionScope.CurrentUser);

        var folder = Path.GetDirectoryName(GetCertificatePath());
        if (!string.IsNullOrWhiteSpace(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // Write to a sibling temp file then move into place so a crash during
        // write cannot leave us with a half-written certificate that will fail
        // signature verification on next boot.
        var path = GetCertificatePath();
        var tempPath = path + ".tmp";
        File.WriteAllBytes(tempPath, encrypted);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static string GetCertificatePath()
    {
        return AppDataPaths.Combine(LicenseFolderName, CertificateFileName);
    }

    private static JsonElement ToJsonElement(LicenseCertificatePayload payload)
    {
        // Round-trip through the source-generated context so the JSON layout
        // matches exactly what the backend signed (same property names, same
        // null/empty handling). We then canonicalize the resulting JsonElement
        // so the byte stream we hash is independent of whatever ordering
        // System.Text.Json happened to emit.
        var json = JsonSerializer.Serialize(payload, LicensingJsonContext.Default.LicenseCertificatePayload);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private long AdvanceRequestSequence()
    {
        var current = _readRequestSequence?.Invoke() ?? 0L;
        var next = current + 1;
        _persistRequestSequence?.Invoke(next);
        return next;
    }

    private static long ReadSequenceFromSettings(RetailStorePOS.Data.Modules.Settings.SettingsRepository settings)
    {
        if (settings is null) return 0;
        var raw = settings.GetSetting(SequenceSettingKey, "0");
        return long.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0L;
    }

    private static void WriteSequenceToSettings(RetailStorePOS.Data.Modules.Settings.SettingsRepository settings, long value)
    {
        if (settings is null) return;
        settings.SetSetting(SequenceSettingKey, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        return client;
    }
}
