using RetailStorePOS.UI.Common.Services;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Migrations;
using RetailStorePOS.Data.Modules.Settings;

namespace LicensingTests;

/// <summary>
/// Verifies that <see cref="FeatureAccessService"/> consults the supplied
/// license snapshot accessor in the documented precedence order. Uses a real
/// temp-file SQLite database for <see cref="SettingsRepository"/> because the
/// repository is not interface-based; the test cost is small (one
/// <c>DatabaseInitializer.Initialize</c> call per test).
/// </summary>
[TestClass]
public sealed class FeatureAccessServiceTests
{
    private string _testDir = null!;
    private string _dbPath = null!;
    private SqliteConnectionFactory _factory = null!;
    private SettingsRepository _settings = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(
            Path.GetTempPath(),
            "RetailStorePOS_LicensingTests",
            Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        _dbPath = Path.Combine(_testDir, "settings.db");
        DatabaseInitializer.Initialize(_dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(_dbPath);

        _factory = new SqliteConnectionFactory(_dbPath);
        _settings = new SettingsRepository(_factory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private FeatureAccessService BuildService(
        LicenseActivationSnapshot? snapshot = null,
        DateTimeOffset? now = null)
    {
        var fixedNow = now ?? new DateTimeOffset(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);
        return new FeatureAccessService(
            _settings,
            snapshotAccessor: () => snapshot,
            clock: () => fixedNow);
    }

    private static LicenseActivationSnapshot BuildSnapshot(
        IReadOnlyList<string>? features = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? notBeforeAt = null,
        string licenseStatus = "active",
        string activationStatus = "active")
    {
        var issued = new DateTimeOffset(2026, 5, 19, 9, 0, 0, TimeSpan.Zero);
        return new LicenseActivationSnapshot(
            CertificateId: "cert-1",
            LicenseId: "lic-1",
            ActivationId: "act-1",
            InstallId: "INSTALL_TEST",
            ProductCode: "RETAILSTOREPOS",
            PermissionGroup: "PREMIUM",
            Features: features ?? new[] { FeatureAccessService.Features.AdvancedReports },
            LicenseStatus: licenseStatus,
            ActivationStatus: activationStatus,
            IssuedAtUtc: issued,
            NotBeforeUtc: notBeforeAt ?? issued,
            ExpiresAtUtc: expiresAt ?? new DateTimeOffset(2026, 6, 19, 9, 0, 0, TimeSpan.Zero));
    }

    [TestMethod]
    public void NoSnapshot_DeniesAdvancedReports()
    {
        var service = BuildService(snapshot: null);
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
        Assert.IsFalse(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void NoSnapshot_UnknownFeature_IsDeniedByDefault()
    {
        var service = BuildService(snapshot: null);
        Assert.IsFalse(service.CanUse("SomeFutureFeature"));
    }

    [TestMethod]
    public void NoSnapshot_DashboardDatePill_IsDeniedByDefault()
    {
        var service = BuildService(snapshot: null);
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.DashboardDatePill));
    }

    [TestMethod]
    public void Snapshot_WithFeature_AllowsThatFeature()
    {
        var snapshot = BuildSnapshot(features: new[] { FeatureAccessService.Features.AdvancedReports });
        var service = BuildService(snapshot);
        Assert.IsTrue(service.CanUse(FeatureAccessService.Features.AdvancedReports));
        Assert.IsTrue(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void Snapshot_WithAdvancedReports_AllowsDashboardDatePill()
    {
        var snapshot = BuildSnapshot(features: new[] { FeatureAccessService.Features.AdvancedReports });
        var service = BuildService(snapshot);
        Assert.IsTrue(service.CanUse(FeatureAccessService.Features.DashboardDatePill));
        Assert.IsTrue(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void Snapshot_MissingFeature_Denies()
    {
        // An activated lower-tier license cannot accidentally see Premium UI.
        var snapshot = BuildSnapshot(features: Array.Empty<string>());
        var service = BuildService(snapshot);
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
        Assert.IsTrue(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void Snapshot_Expired_DeniesPaidFeatures()
    {
        var snapshot = BuildSnapshot(
            features: Array.Empty<string>(),
            expiresAt: new DateTimeOffset(2026, 5, 19, 8, 0, 0, TimeSpan.Zero));
        var service = BuildService(snapshot);
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
        Assert.IsFalse(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void Snapshot_RevokedStatus_DeniesPaidFeatures()
    {
        var snapshot = BuildSnapshot(
            features: new[] { FeatureAccessService.Features.AdvancedReports },
            licenseStatus: "revoked");
        var service = BuildService(snapshot);
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
        Assert.IsFalse(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void ForceFreeMode_DeniesEvenWhenSnapshotAllows()
    {
        var snapshot = BuildSnapshot();
        var service = BuildService(snapshot);
        service.SetForceFreeMode(true);
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
    }

    [TestMethod]
    public void DeveloperOverride_AllowOverridesSnapshotDeny()
    {
        var snapshot = BuildSnapshot(features: Array.Empty<string>());
        var service = BuildService(snapshot);
        service.SetDeveloperOverride(FeatureAccessService.Features.AdvancedReports, true);
#if DEBUG
        Assert.IsTrue(service.CanUse(FeatureAccessService.Features.AdvancedReports));
#else
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
#endif
    }

    [TestMethod]
    public void DeveloperOverride_DenyOverridesSnapshotAllow()
    {
        var snapshot = BuildSnapshot();
        var service = BuildService(snapshot);
        service.SetDeveloperOverride(FeatureAccessService.Features.AdvancedReports, false);
#if DEBUG
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
#else
        Assert.IsTrue(service.CanUse(FeatureAccessService.Features.AdvancedReports));
#endif
    }

    [TestMethod]
    public void OnSnapshotChanged_RaisesFeatureAccessChanged()
    {
        var service = BuildService();
        var raised = 0;
        service.FeatureAccessChanged += (_, _) => raised++;
        service.OnSnapshotChanged();
        Assert.AreEqual(1, raised);
    }

    [TestMethod]
    public void SnapshotAccessor_Throwing_TreatsAsNoSnapshot()
    {
        var service = new FeatureAccessService(
            _settings,
            snapshotAccessor: () => throw new InvalidOperationException("boom"),
            clock: () => DateTimeOffset.UtcNow);

        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
        Assert.IsFalse(service.IsLicenseEnforcementActive);
    }

    [TestMethod]
    public void EmptyFeatureKey_IsAlwaysDenied()
    {
        var service = BuildService(BuildSnapshot());
        Assert.IsFalse(service.CanUse(""));
        Assert.IsFalse(service.CanUse("   "));
        Assert.IsFalse(service.CanUse(null!));
    }

    [TestMethod]
    public void Snapshot_WithFutureNotBefore_IsNotYetValid()
    {
        // notBeforeUtc is in the future relative to the test clock so the
        // snapshot must NOT be considered currently valid even though the
        // expiry is far away.
        var clockNow = new DateTimeOffset(2026, 5, 19, 12, 0, 0, TimeSpan.Zero);
        var snapshot = BuildSnapshot(
            notBeforeAt: clockNow.AddDays(1),
            expiresAt: clockNow.AddDays(30));

        var service = new FeatureAccessService(
            _settings,
            snapshotAccessor: () => snapshot,
            clock: () => clockNow);

        Assert.IsFalse(service.IsLicenseEnforcementActive);
        // A not-yet-valid certificate cannot grant paid features.
        Assert.IsFalse(service.CanUse(FeatureAccessService.Features.AdvancedReports));
    }
}
