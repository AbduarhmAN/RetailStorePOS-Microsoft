using RetailStorePOS.App.Services;
using RetailStorePOS.App.Services.Licensing;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Modules.Migrations;
using RetailStorePOS.Data.Modules.Settings;
using RetailStorePOS.Data.Modules.UsersAuth;
using RetailStorePOS.Data.Repositories;

namespace LicensingTests;

[TestClass]
public sealed class AuthorizationGuardTests
{
    private string _testDir = null!;
    private SqliteConnectionFactory _factory = null!;
    private SettingsRepository _settings = null!;
    private UserRepository _users = null!;
    private AuditLogRepository _auditRepo = null!;
    private AuditLogService _audit = null!;
    private AuthService _auth = null!;
    private FeatureAccessService _features = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RetailStorePOS_AuthorizationGuardTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        var dbPath = Path.Combine(_testDir, "settings.db");
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        _factory = new SqliteConnectionFactory(dbPath);
        _settings = new SettingsRepository(_factory);
        _users = new UserRepository(_factory);
        _auditRepo = new AuditLogRepository(_factory);
        _audit = new AuditLogService(_auditRepo);
        _auth = new AuthService(_users, _audit);
        _features = new FeatureAccessService(_settings);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    private void SignInAdmin()
    {
        _auth.CreateFirstAdmin("admin", "Administrator", "TempPassword123!", "654321");
        var result = _auth.LoginWithPassword("admin", "TempPassword123!");
        Assert.IsTrue(result.Success, "Test setup failed: admin login did not succeed.");
    }

    [TestMethod]
    public void RequireAuthenticated_WithNoSession_Throws()
    {
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => guard.RequireAuthenticated());
        Assert.IsFalse(guard.IsAuthenticated);
        Assert.IsFalse(guard.IsAdmin);
    }

    [TestMethod]
    public void RequireAuthenticated_WithSignedInUser_DoesNotThrow()
    {
        SignInAdmin();
        var guard = new AuthorizationGuard(_auth, _features);
        guard.RequireAuthenticated();
        Assert.IsTrue(guard.IsAuthenticated);
    }

    [TestMethod]
    public void RequireAdmin_WithNoSession_Throws()
    {
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => guard.RequireAdmin());
    }

    [TestMethod]
    public void RequireAdmin_WithAdmin_DoesNotThrow()
    {
        SignInAdmin();
        var guard = new AuthorizationGuard(_auth, _features);
        guard.RequireAdmin();
        Assert.IsTrue(guard.IsAdmin);
    }

    [TestMethod]
    public void RequireFeature_WithNoSession_ThrowsBeforeLicenseCheck()
    {
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            guard.RequireFeature(FeatureAccessService.Features.AdvancedReports));
    }

    [TestMethod]
    public void RequireFeature_WhenFeatureAccessDenies_Throws()
    {
        SignInAdmin();
        _features.SetForceFreeMode(true);
        var guard = new AuthorizationGuard(_auth, _features);

        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            guard.RequireFeature(FeatureAccessService.Features.AdvancedReports));
    }

    [TestMethod]
    public void RequireFeature_WhenFeatureAccessAllows_DoesNotThrow()
    {
        SignInAdmin();
        // FeatureAccessService default policy allows AdvancedReports in
        // pre-release mode without a snapshot.
        var guard = new AuthorizationGuard(_auth, _features);
        guard.RequireFeature(FeatureAccessService.Features.AdvancedReports);
    }

    [TestMethod]
    public void RequireFeature_WithEmptyKey_ThrowsArgumentException()
    {
        SignInAdmin();
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.ThrowsExactly<ArgumentException>(() => guard.RequireFeature(""));
        Assert.ThrowsExactly<ArgumentException>(() => guard.RequireFeature("   "));
    }

    [TestMethod]
    public void RequirePermission_WithFailingPredicate_Throws()
    {
        SignInAdmin();
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            guard.RequirePermission(() => false, "TestPermission"));
    }

    [TestMethod]
    public void RequirePermission_WithThrowingPredicate_TreatsAsDenied()
    {
        SignInAdmin();
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            guard.RequirePermission(() => throw new InvalidOperationException("bad"), "TestPermission"));
    }

    [TestMethod]
    public void IsAdmin_WhenSessionLocked_IsFalse()
    {
        SignInAdmin();
        var guard = new AuthorizationGuard(_auth, _features);
        Assert.IsTrue(guard.IsAdmin);

        _auth.Lock();
        Assert.IsFalse(guard.IsAdmin, "A locked session must not count as 'admin'.");
        Assert.IsFalse(guard.IsAuthenticated, "A locked session must not count as 'authenticated'.");
    }
}
