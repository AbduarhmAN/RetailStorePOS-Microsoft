using RetailStorePOS.App.Services;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Models;
using RetailStorePOS.Data.Modules.Migrations;
using RetailStorePOS.Data.Modules.Settings;
using RetailStorePOS.Data.Repositories;

namespace LicensingTests;

[TestClass]
public sealed class BootstrapAdminCredentialsTests
{
    private string _testDir = null!;
    private SqliteConnectionFactory _factory = null!;
    private SettingsRepository _settings = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RetailStorePOS_BootstrapTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        var dbPath = Path.Combine(_testDir, "settings.db");
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);
        _factory = new SqliteConnectionFactory(dbPath);
        _settings = new SettingsRepository(_factory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_testDir, true); } catch { }
    }

    [TestMethod]
    public void GenerateAndPersist_ReturnsLengthsAndNonTrivialValues()
    {
        var creds = new BootstrapAdminCredentials(_settings);
        var pair = creds.GenerateAndPersist();

        Assert.AreEqual(12, pair.Password.Length, "Password should be 12 chars.");
        Assert.AreEqual(4, pair.Pin.Length, "PIN should be 4 digits.");
        Assert.IsTrue(pair.Pin.All(char.IsDigit), "PIN must be all digits.");
        Assert.AreNotEqual("1234", pair.Password);
        Assert.AreNotEqual("1234", pair.Pin);
    }

    [TestMethod]
    public void Load_AfterGenerate_ReturnsSameCredentials()
    {
        var creds = new BootstrapAdminCredentials(_settings);
        var generated = creds.GenerateAndPersist();

        // Build a fresh service to confirm we round-trip via the DB, not via memory.
        var reloaded = new BootstrapAdminCredentials(_settings).Load();

        Assert.IsNotNull(reloaded);
        Assert.AreEqual(generated.Password, reloaded!.Password);
        Assert.AreEqual(generated.Pin, reloaded.Pin);
    }

    [TestMethod]
    public void Clear_RemovesPersistedValues()
    {
        var creds = new BootstrapAdminCredentials(_settings);
        creds.GenerateAndPersist();
        creds.Clear();

        Assert.IsNull(creds.Load());
    }

    [TestMethod]
    public void Load_WithNoStoredCredentials_ReturnsNull()
    {
        var creds = new BootstrapAdminCredentials(_settings);
        Assert.IsNull(creds.Load());
    }

    [TestMethod]
    public void Generate_TwiceRunsProduceDifferentValues()
    {
        var creds = new BootstrapAdminCredentials(_settings);
        var first = creds.GenerateAndPersist();
        creds.Clear();
        var second = creds.GenerateAndPersist();

        // The keyspace is wide enough that two consecutive randoms colliding
        // has effectively zero probability.
        Assert.AreNotEqual(first.Password, second.Password);
        Assert.AreNotEqual(first.Pin, second.Pin);
    }
}

[TestClass]
public sealed class AuditLogServiceTests
{
    [TestMethod]
    public void Log_WhenWriterThrows_RaisesWriteFailedAndCapturesLastFailure()
    {
        var reportedExceptions = 0;
        var service = new AuditLogServiceTestHarness(
            writer: _ => throw new InvalidOperationException("simulated DB outage"),
            exceptionReporter: (_, _) => reportedExceptions++);

        Exception? raised = null;
        service.Service.WriteFailed += (_, _, ex) => raised = ex;

        service.Service.Log("TEST_ACTION", "details");

        Assert.IsNotNull(raised, "WriteFailed event should fire on writer exception.");
        Assert.IsNotNull(service.Service.LastFailure);
        Assert.AreEqual("TEST_ACTION", service.Service.LastFailure!.Action);
        Assert.IsTrue(reportedExceptions > 0, "exceptionReporter should have been called.");
    }

    [TestMethod]
    public void Log_WhenWriterThrows_DoesNotPropagateException()
    {
        var service = new AuditLogServiceTestHarness(
            writer: _ => throw new InvalidOperationException("simulated DB outage"));

        // Must not throw. Audit logging is a side-effect, not a control-flow gate.
        service.Service.Log("CHECKOUT", "sale 123");
    }

    [TestMethod]
    public void Log_SuccessfulWriteAfterFailure_ClearsLastFailure()
    {
        var shouldThrow = true;
        var service = new AuditLogServiceTestHarness(writer: _ =>
        {
            if (shouldThrow)
            {
                throw new InvalidOperationException("transient failure");
            }
        });

        service.Service.Log("X");
        Assert.IsNotNull(service.Service.LastFailure);

        shouldThrow = false;
        service.Service.Log("LOGIN_SUCCESS", "details", userId: 1);
        Assert.IsNull(service.Service.LastFailure, "Successful write must clear the failure marker.");
    }

    [TestMethod]
    public void Log_OnFailure_PersistsOutboxFile()
    {
        // Best-effort check that an outbox JSON gets written under AppDataPaths.
        // Snapshot files before and after to detect the new entry without
        // depending on a specific filename pattern. The outbox folder is shared
        // across tests, but the count strictly increases when our failure path
        // runs successfully.
        var folder = AppDataPaths.Combine("audit_outbox");
        Directory.CreateDirectory(folder);
        var before = new HashSet<string>(Directory.GetFiles(folder, "*.json"));

        var service = new AuditLogServiceTestHarness(
            writer: _ => throw new InvalidOperationException("simulated DB outage"));
        service.Service.Log("OUTBOX_TEST", "expected on disk");

        var after = Directory.GetFiles(folder, "*.json");
        var newFiles = after.Where(p => !before.Contains(p)).ToArray();

        Assert.IsTrue(newFiles.Length >= 1, "Failure path should write at least one outbox file.");
        try
        {
            // Best-effort cleanup so the per-machine outbox does not grow.
            foreach (var path in newFiles) File.Delete(path);
        }
        catch { /* best-effort */ }
    }

    /// <summary>
    /// Wraps the internal Action&lt;AuditLog&gt; constructor so each test can
    /// supply its own behaviour without needing a fake repository class.
    /// </summary>
    private sealed class AuditLogServiceTestHarness
    {
        public AuditLogService Service { get; }

        public AuditLogServiceTestHarness(
            Action<AuditLog> writer,
            Action<Exception, string>? exceptionReporter = null)
        {
            // The internal constructor is visible thanks to InternalsVisibleTo.
            Service = (AuditLogService)Activator.CreateInstance(
                typeof(AuditLogService),
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                binder: null,
                args: new object?[] { writer, exceptionReporter },
                culture: null)!;
        }
    }
}
