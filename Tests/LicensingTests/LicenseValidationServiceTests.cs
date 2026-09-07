using RetailStorePOS.UI.Common.Services;

namespace LicensingTests;

[TestClass]
public sealed class LicenseValidationServiceTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("http://example.supabase.co")]
    [DataRow("https://user:password@example.supabase.co")]
    [DataRow("https://example.supabase.co?redirect=elsewhere")]
    [DataRow("https://example.supabase.co#fragment")]
    [DataRow("/relative-path")]
    public async Task InvalidBackendUrl_RejectsBothRequestsBeforeReadingIdentityOrSendingSecrets(string? url)
    {
        var service = new LicenseValidationService(
            supabaseUrlAccessor: () => url,
            supabaseKeyAccessor: () => "test-public-key",
            installIdAccessor: () => throw new InvalidOperationException("Identity must not be read."),
            httpClientFactory: () => throw new InvalidOperationException("HTTP must not be created."),
            clock: () => DateTimeOffset.UtcNow);

        Assert.AreEqual(LicenseActivationOutcome.BackendNotConfigured,
            (await service.ActivateAsync("test-license-key")).Outcome);
        Assert.AreEqual(LicenseActivationOutcome.BackendNotConfigured,
            (await service.ReissueByInstallAsync()).Outcome);
    }

    [TestMethod]
    [DataRow("https://example.supabase.co")]
    [DataRow("https://licenses.example.com/")]
    public void IsSecureBackendUrl_AcceptsHttpsProjectAndCustomDomains(string url)
    {
        Assert.IsTrue(LicenseValidationService.IsSecureBackendUrl(url));
    }

    [TestMethod]
    public void ValidateResponseBinding_VerificationRequiresExactNonceAndSequence()
    {
        var request = BuildRequest();
        var payload = BuildPayload(request.RequestNonce, request.RequestSequence);

        Assert.IsNull(LicenseValidationService.ValidateResponseBinding(payload, request));
        Assert.AreEqual(
            "local_nonce_mismatch",
            LicenseValidationService.ValidateResponseBinding(
                payload with { RequestNonce = "different" },
                request));
        Assert.AreEqual(
            "local_sequence_mismatch",
            LicenseValidationService.ValidateResponseBinding(
                payload with { RequestSequence = request.RequestSequence + 1 },
                request));
    }

    [TestMethod]
    public void ValidateResponseBinding_RejectsMissingSequence()
    {
        var request = BuildRequest();
        var payload = BuildPayload(request.RequestNonce, null);

        Assert.AreEqual(
            "local_sequence_missing",
            LicenseValidationService.ValidateResponseBinding(payload, request));
    }

    [TestMethod]
    public void ValidateResponseBinding_ReissueAcceptsServerSequenceResynchronization()
    {
        var request = BuildRequest() with
        {
            Action = "reissue_by_install",
            MessageType = "license_reissue_request",
        };
        var payload = BuildPayload(request.RequestNonce, request.RequestSequence + 10);

        Assert.IsNull(LicenseValidationService.ValidateResponseBinding(payload, request));
    }

    [TestMethod]
    public void ValidateResponseBinding_ReissueRejectsLowerOrUnsafeSequence()
    {
        var request = BuildRequest() with { Action = "reissue_by_install" };
        Assert.AreEqual("local_sequence_mismatch", LicenseValidationService.ValidateResponseBinding(
            BuildPayload(request.RequestNonce, request.RequestSequence - 1), request));
        foreach (var sequence in new[] { -1L, 0L, 9_007_199_254_740_992L, long.MaxValue })
        {
            Assert.AreEqual("local_sequence_invalid", LicenseValidationService.ValidateResponseBinding(
                BuildPayload(request.RequestNonce, sequence), request));
        }
    }

    [TestMethod]
    public void ValidateCertificateFields_AcceptsCurrentVersionAndValidWindow()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var payload = BuildValidCertificate(now);

        Assert.IsNull(LicenseValidationService.ValidateCertificateFields(payload, "INSTALL-1", now));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void ValidateCertificateFields_RejectsUnavailableLocalIdentity(string? installId)
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

        Assert.AreEqual(
            "local_install_id_unavailable",
            LicenseValidationService.ValidateCertificateFields(BuildValidCertificate(now), installId, now));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("ANOTHER-INSTALL")]
    public void ValidateCertificateFields_RejectsMissingOrDifferentCertificateIdentity(string? installId)
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var payload = BuildValidCertificate(now) with { InstallId = installId };

        Assert.AreEqual(
            "local_install_id_mismatch",
            LicenseValidationService.ValidateCertificateFields(payload, "INSTALL-1", now));
    }

    [TestMethod]
    public void ValidateCertificateFields_RejectsWrongVersionAndMalformedTimes()
    {
        var now = new DateTimeOffset(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);
        var payload = BuildValidCertificate(now);

        Assert.AreEqual(
            "local_certificate_version_mismatch",
            LicenseValidationService.ValidateCertificateFields(
                payload with { CertificateVersion = 2 },
                "INSTALL-1",
                now));
        Assert.AreEqual(
            "local_issued_at_invalid",
            LicenseValidationService.ValidateCertificateFields(
                payload with { IssuedAtUtc = "invalid" },
                "INSTALL-1",
                now));
        Assert.AreEqual(
            "local_not_before_invalid",
            LicenseValidationService.ValidateCertificateFields(
                payload with { NotBeforeUtc = "invalid" },
                "INSTALL-1",
                now));
    }

    private static LicenseActivationRequest BuildRequest() => new()
    {
        RequestNonce = "nonce-1",
        RequestSequence = 42,
        InstallId = "INSTALL-1",
    };

    private static LicenseCertificatePayload BuildPayload(string nonce, long? sequence) => new()
    {
        RequestNonce = nonce,
        RequestSequence = sequence,
    };

    private static LicenseCertificatePayload BuildValidCertificate(DateTimeOffset now) => new()
    {
        MessageType = "license_activation_certificate",
        CertificateVersion = 1,
        LicenseId = "license-1",
        ActivationId = "activation-1",
        InstallId = "INSTALL-1",
        ProductCode = "RETAILSTOREPOS",
        PermissionGroup = "PREMIUM",
        LicenseStatus = "active",
        ActivationStatus = "active",
        IssuedAtUtc = now.AddMinutes(-1).ToString("O"),
        NotBeforeUtc = now.AddMinutes(-1).ToString("O"),
        ExpiresAtUtc = now.AddDays(1).ToString("O"),
    };
}
