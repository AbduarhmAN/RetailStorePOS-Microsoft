using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Migrations;

namespace LicensingTests;

[TestClass]
public sealed class DatabaseBackupServiceTests
{
    private string _testDir = null!;
    private string _dbPath = null!;
    private string _backupDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "RetailStorePOS_BackupTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);

        _dbPath = Path.Combine(_testDir, "pos.db");
        _backupDir = Path.Combine(_testDir, "backups");

        DatabaseInitializer.Initialize(_dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(_dbPath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(_testDir, true);
        }
        catch { }
    }

    [TestMethod]
    public void VerifyIntegrity_OnFreshDatabase_ReturnsOk()
    {
        var service = new DatabaseBackupService(_backupDir);
        var result = service.VerifyIntegrity(_dbPath);
        Assert.IsTrue(result.IsOk, $"integrity_check should pass on a fresh DB, got: {result.Detail}");
    }

    [TestMethod]
    public void VerifyIntegrity_OnMissingFile_ReturnsNotOk()
    {
        var service = new DatabaseBackupService(_backupDir);
        var result = service.VerifyIntegrity(Path.Combine(_testDir, "does_not_exist.db"));
        Assert.IsFalse(result.IsOk);
    }

    [TestMethod]
    public void CreateBackup_ProducesFile_WithMatchingContent()
    {
        var service = new DatabaseBackupService(_backupDir);
        var path = service.CreateBackup(_dbPath);

        Assert.IsTrue(File.Exists(path));
        Assert.IsTrue(path.StartsWith(_backupDir, StringComparison.OrdinalIgnoreCase));

        // Backup must itself pass integrity check.
        var integrity = service.VerifyIntegrity(path);
        Assert.IsTrue(integrity.IsOk, $"Backup file should be readable, got: {integrity.Detail}");
    }

    [TestMethod]
    public void FindLatestBackup_ReturnsMostRecent()
    {
        var service = new DatabaseBackupService(_backupDir);
        var first = service.CreateBackup(_dbPath);
        // Sleep a second so the timestamp suffix differs.
        Thread.Sleep(1100);
        var second = service.CreateBackup(_dbPath);

        var latest = service.FindLatestBackup();
        Assert.AreEqual(second, latest);
        Assert.AreNotEqual(first, latest);
    }

    [TestMethod]
    public void FindLatestBackup_WithNoBackups_ReturnsNull()
    {
        var service = new DatabaseBackupService(_backupDir);
        Assert.IsNull(service.FindLatestBackup());
    }

    [TestMethod]
    public void GetLatestBackupTimestampUtc_ParsesFromFilename()
    {
        var service = new DatabaseBackupService(_backupDir);
        var before = DateTime.UtcNow.AddSeconds(-5);
        service.CreateBackup(_dbPath);
        var after = DateTime.UtcNow.AddSeconds(5);

        var ts = service.GetLatestBackupTimestampUtc();
        Assert.IsNotNull(ts);
        Assert.IsTrue(ts.Value >= before && ts.Value <= after,
            $"Backup timestamp {ts:O} should fall between {before:O} and {after:O}.");
    }

    [TestMethod]
    public void TrimBackups_KeepsOnlyTheNNewest()
    {
        var service = new DatabaseBackupService(_backupDir);

        for (var i = 0; i < 4; i++)
        {
            service.CreateBackup(_dbPath);
            Thread.Sleep(1100); // ensure distinct timestamps
        }

        service.TrimBackups(keepCount: 2);

        var remaining = Directory.GetFiles(_backupDir, "pos_*.db");
        Assert.AreEqual(2, remaining.Length, "TrimBackups should leave exactly the 2 newest files.");
    }

    [TestMethod]
    public void TryRestoreFromLatestBackup_WithNoBackup_ReturnsNoBackupAvailable()
    {
        var service = new DatabaseBackupService(_backupDir);
        var result = service.TryRestoreFromLatestBackup(_dbPath);
        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.BackupPath);
    }

    [TestMethod]
    public void TryRestoreFromLatestBackup_RecoversFromCorruption()
    {
        var service = new DatabaseBackupService(_backupDir);

        // Take a known-good snapshot.
        var goodBackup = service.CreateBackup(_dbPath);

        // Drop pooled connections so we can overwrite the main DB file.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        // Corrupt the live DB by overwriting it with garbage bytes that are
        // definitely not a valid SQLite header.
        File.WriteAllBytes(_dbPath, new byte[] { 0xFF, 0xFE, 0x00, 0x01, 0x02, 0x03 });

        var integrityBefore = service.VerifyIntegrity(_dbPath);
        Assert.IsFalse(integrityBefore.IsOk, "Corrupt DB must fail integrity check.");

        var restore = service.TryRestoreFromLatestBackup(_dbPath);
        Assert.IsTrue(restore.Succeeded, $"Restore should succeed, got: {restore.ErrorMessage}");
        Assert.AreEqual(goodBackup, restore.BackupPath);

        var integrityAfter = service.VerifyIntegrity(_dbPath);
        Assert.IsTrue(integrityAfter.IsOk, "Restored DB should pass integrity check.");
    }
}
