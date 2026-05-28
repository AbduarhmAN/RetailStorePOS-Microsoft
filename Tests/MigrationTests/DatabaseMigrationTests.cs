using Microsoft.Data.Sqlite;
using RetailStorePOS.Data;
using RetailStorePOS.Data.Modules.Migrations;

namespace MigrationTests;

[TestClass]
public class DatabaseMigrationTests
{
    private string _testDbDir = null!;

    [TestInitialize]
    public void Setup()
    {
        _testDbDir = Path.Combine(Path.GetTempPath(), "RetailStorePOS_MigrationTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDbDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_testDbDir, true); } catch { }
    }

    private string GetTestDbPath(string name = "test.db") => Path.Combine(_testDbDir, name);

    private static string GetFixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", "Databases", name);
    }

    private static SqliteConnection OpenConnection(string dbPath)
    {
        var factory = new SqliteConnectionFactory(dbPath);
        return factory.OpenConnection();
    }

    private int GetUserVersion(string dbPath)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private bool TableExists(string dbPath, string tableName)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private bool ColumnExists(string dbPath, string tableName, string columnName)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private void SetUserVersion(string dbPath, int version)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    // ========================================
    // TEST: Fresh database reaches latest version
    // ========================================

    [TestMethod]
    public void FreshDatabase_InitializesToLatestVersion()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);

        Assert.AreEqual(24, GetUserVersion(dbPath), "Fresh database should be at version 24");
    }

    [TestMethod]
    public void FreshDatabase_HasAllCoreTables()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);

        Assert.IsTrue(TableExists(dbPath, "products"));
        Assert.IsTrue(TableExists(dbPath, "sales"));
        Assert.IsTrue(TableExists(dbPath, "sale_items"));
        Assert.IsTrue(TableExists(dbPath, "settings"));
        Assert.IsTrue(TableExists(dbPath, "users"));
        Assert.IsTrue(TableExists(dbPath, "sessions"));
        Assert.IsTrue(TableExists(dbPath, "audit_logs"));
        Assert.IsTrue(TableExists(dbPath, "tax_authorities"));
        Assert.IsTrue(TableExists(dbPath, "tax_rules"));
        Assert.IsTrue(TableExists(dbPath, "tax_groups"));
        Assert.IsTrue(TableExists(dbPath, "register_sessions"));
        Assert.IsTrue(TableExists(dbPath, "register_cash_adjustments"));
        Assert.IsTrue(TableExists(dbPath, "register_session_assignments"));
        Assert.IsTrue(TableExists(dbPath, "schema_metadata"));
    }

    [TestMethod]
    public void FreshDatabase_HasSecurityColumns()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);

        Assert.IsTrue(ColumnExists(dbPath, "users", "failed_login_count"));
        Assert.IsTrue(ColumnExists(dbPath, "users", "last_failed_login_at_utc"));
        Assert.IsTrue(ColumnExists(dbPath, "users", "locked_until_utc"));
        Assert.IsTrue(ColumnExists(dbPath, "users", "must_change_password"));
        Assert.IsTrue(ColumnExists(dbPath, "sales", "register_session_id"));
        Assert.IsTrue(ColumnExists(dbPath, "sales", "cashier_user_id"));
        Assert.IsTrue(ColumnExists(dbPath, "register_sessions", "closed_by_user_id"));
    }

    [TestMethod]
    public void FreshDatabase_MaintenanceMigrations_Succeed()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);

        var result = DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        Assert.IsTrue(result.Success);
        Assert.AreEqual(24, result.Version);
    }

    // ========================================
    // TEST: Data preservation during upgrade
    // ========================================

    [TestMethod]
    public void Upgrade_PreservesExistingProducts()
    {
        var dbPath = GetTestDbPath();

        // Create full DB and insert test data
        DatabaseInitializer.Initialize(dbPath);
        InsertTestProduct(dbPath, "Migration Test Product", 1599);

        // Roll back version and re-migrate
        SetUserVersion(dbPath, 5);
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        // Verify data survived
        Assert.IsTrue(ProductExists(dbPath, "Migration Test Product"), "Product should survive migration");
    }

    [TestMethod]
    public void Upgrade_PreservesExistingUsers()
    {
        var dbPath = GetTestDbPath();

        DatabaseInitializer.Initialize(dbPath);
        InsertTestUser(dbPath, "testcashier", "Test Cashier");

        SetUserVersion(dbPath, 5);
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        Assert.IsTrue(UserExists(dbPath, "testcashier"), "User should survive migration");
    }

    [TestMethod]
    public void Upgrade_PreservesExistingSales()
    {
        var dbPath = GetTestDbPath();

        DatabaseInitializer.Initialize(dbPath);
        InsertTestSale(dbPath, 1001);

        SetUserVersion(dbPath, 10);
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        Assert.IsTrue(SaleExists(dbPath, 1001), "Sale should survive migration");
    }

    // ========================================
    // TEST: Downgrade prevention
    // ========================================

    [TestMethod]
    public void DowngradePrevention_ThrowsForNewerDatabase()
    {
        var dbPath = GetTestDbPath();

        DatabaseInitializer.Initialize(dbPath);
        SetUserVersion(dbPath, 99); // Simulate future version

        var threw = false;
        try
        {
            DatabaseInitializer.Initialize(dbPath);
        }
        catch (DatabaseTooNewException)
        {
            threw = true;
        }

        Assert.IsTrue(threw, "Should throw DatabaseTooNewException for newer database");
    }

    [TestMethod]
    public void DowngradePrevention_AllowsCurrentVersion()
    {
        var dbPath = GetTestDbPath();

        DatabaseInitializer.Initialize(dbPath);
        // Open again — should not throw
        DatabaseInitializer.Initialize(dbPath);

        Assert.AreEqual(24, GetUserVersion(dbPath));
    }

    // ========================================
    // TEST: Schema metadata
    // ========================================

    [TestMethod]
    public void SchemaMetadata_IsPopulatedAfterInit()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        Assert.IsTrue(TableExists(dbPath, "schema_metadata"));
        Assert.IsTrue(SchemaMetadataHasKey(dbPath, "current_schema_version"));
    }

    [TestMethod]
    public void FreshDatabase_StoresEncryptionMetadata()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);

        Assert.IsTrue(SchemaMetadataHasValue(dbPath, "encryption_version", DatabaseEncryptionService.CurrentEncryptionVersion));
    }

    [TestMethod]
    public void FreshDatabase_IsNotReadableWithoutEncryptionKey()
    {
        var dbPath = GetTestDbPath();
        DatabaseInitializer.Initialize(dbPath);

        AssertPlainSqliteCannotRead(dbPath);
    }

    [TestMethod]
    public void PlaintextDatabase_InitializeKeepsLegacyPlaintextAndPreservesData()
    {
        var dbPath = GetTestDbPath();

        using (var rawConn = new SqliteConnection($"Data Source={dbPath}"))
        {
            rawConn.Open();
            using var rawCmd = rawConn.CreateCommand();
            rawCmd.CommandText = @"
CREATE TABLE legacy_probe (
    id INTEGER PRIMARY KEY,
    name TEXT NOT NULL
);
INSERT INTO legacy_probe (name) VALUES ('before encryption');";
            rawCmd.ExecuteNonQuery();
        }

        DatabaseInitializer.Initialize(dbPath);

        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM legacy_probe WHERE name = 'before encryption';";
        Assert.AreEqual(1L, Convert.ToInt64(cmd.ExecuteScalar()));
        Assert.IsTrue(SchemaMetadataHasValue(dbPath, "encryption_version", DatabaseEncryptionService.LegacyPlaintextEncryptionVersion));
        AssertPlainSqliteCanRead(dbPath);
        Assert.IsFalse(SchemaMetadataHasKey(dbPath, "last_encryption_migration_at"));
        Assert.IsFalse(SchemaMetadataHasKey(dbPath, "last_encryption_plaintext_backup_path"));
        Assert.AreEqual(0, Directory.GetFiles(_testDbDir, "*.unencrypted-pre-sqlcipher-*.bak").Length,
            "Legacy plaintext databases should not trigger encryption backups");
        Assert.AreEqual(0, Directory.GetFiles(_testDbDir, "*.sqlcipher-*.db").Length,
            "Legacy plaintext databases should not create SQLCipher temp files");
    }

    [TestMethod]
    public void RealV110Database_UpgradesLegacyPlaintextAndPreservesExistingRows()
    {
        var fixturePath = GetFixturePath("retailstorepos-1.1.0-pos.db");
        if (!File.Exists(fixturePath))
        {
            Assert.Inconclusive($"Missing migration fixture: {fixturePath}");
        }

        var dbPath = GetTestDbPath("retailstorepos-1.1.0-upgrade.db");
        File.Copy(fixturePath, dbPath, overwrite: false);

        var originalRowCounts = ReadPlaintextUserTableCounts(dbPath);
        Assert.IsTrue(originalRowCounts.Count > 0, "The v1.1.0 fixture should contain at least one user table.");

        DatabaseInitializer.Initialize(dbPath);
        var result = DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        Assert.IsTrue(result.Success, "The real v1.1.0 database should upgrade through all current migrations.");
        Assert.AreEqual(24, result.Version);
        Assert.IsTrue(SchemaMetadataHasValue(dbPath, "encryption_version", DatabaseEncryptionService.LegacyPlaintextEncryptionVersion));
        Assert.IsTrue(TableExists(dbPath, "schema_metadata"));
        Assert.IsTrue(TableExists(dbPath, "products"));
        Assert.IsTrue(TableExists(dbPath, "users"));
        Assert.IsTrue(TableExists(dbPath, "sales"));
        AssertOriginalRowsPreserved(dbPath, originalRowCounts);
        AssertPlainSqliteCanRead(dbPath);
        Assert.IsFalse(SchemaMetadataHasKey(dbPath, "last_encryption_migration_at"));
    }

    // ========================================
    // TEST: Pre-migration backup
    // ========================================

    [TestMethod]
    public void MaintenanceMigrations_CreatesBackupForHeavyMigrations()
    {
        var dbPath = GetTestDbPath();

        // Create full DB, then roll back to before a heavy migration (v5 is HeavyBackfill)
        DatabaseInitializer.Initialize(dbPath);
        SetUserVersion(dbPath, 4);

        // Run maintenance — should create backup before v5
        DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        var bakFiles = Directory.GetFiles(_testDbDir, "*.bak");
        Assert.IsTrue(bakFiles.Length > 0, "Backup file should be created for heavy migrations");
    }

    // ========================================
    // TEST: Idempotency — running Initialize twice is safe
    // ========================================

    [TestMethod]
    public void Initialize_IsIdempotent()
    {
        var dbPath = GetTestDbPath();

        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.Initialize(dbPath);
        DatabaseInitializer.Initialize(dbPath);

        Assert.AreEqual(24, GetUserVersion(dbPath));
    }

    [TestMethod]
    public void MaintenanceMigrations_IsIdempotent()
    {
        var dbPath = GetTestDbPath();

        DatabaseInitializer.Initialize(dbPath);
        var r1 = DatabaseInitializer.RunMaintenanceMigrations(dbPath);
        var r2 = DatabaseInitializer.RunMaintenanceMigrations(dbPath);
        var r3 = DatabaseInitializer.RunMaintenanceMigrations(dbPath);

        Assert.IsTrue(r1.Success);
        Assert.IsTrue(r2.Success);
        Assert.IsTrue(r3.Success);
        Assert.AreEqual(24, r3.Version);
    }

    // ========================================
    // HELPERS
    // ========================================

    private void InsertTestProduct(string dbPath, string name, int priceCents)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO products (name, price_cents, quantity, quantity_store, created_at, updated_at) VALUES (@name, @price, 10, 10, datetime('now'), datetime('now'));";
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@price", priceCents);
        cmd.ExecuteNonQuery();
    }

    private bool ProductExists(string dbPath, string name)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM products WHERE name = @name;";
        cmd.Parameters.AddWithValue("@name", name);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private void InsertTestUser(string dbPath, string username, string displayName)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO users (username, display_name, is_active, created_at, updated_at) VALUES (@u, @d, 1, datetime('now'), datetime('now'));";
        cmd.Parameters.AddWithValue("@u", username);
        cmd.Parameters.AddWithValue("@d", displayName);
        cmd.ExecuteNonQuery();
    }

    private bool UserExists(string dbPath, string username)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users WHERE username = @u;";
        cmd.Parameters.AddWithValue("@u", username);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private void InsertTestSale(string dbPath, int receiptNumber)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO sales (receipt_number, subtotal_cents, tax_cents, total_cents, tendered_cents, change_cents, payment_type, cashier_name, created_at) VALUES (@r, 1000, 100, 1100, 1200, 100, 'cash', 'test', datetime('now'));";
        cmd.Parameters.AddWithValue("@r", receiptNumber);
        cmd.ExecuteNonQuery();
    }

    private bool SaleExists(string dbPath, int receiptNumber)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sales WHERE receipt_number = @r;";
        cmd.Parameters.AddWithValue("@r", receiptNumber);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private bool SchemaMetadataHasKey(string dbPath, string key)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM schema_metadata WHERE key = @k;";
        cmd.Parameters.AddWithValue("@k", key);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private bool SchemaMetadataHasValue(string dbPath, string key, string expectedValue)
    {
        using var conn = OpenConnection(dbPath);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM schema_metadata WHERE key = @k;";
        cmd.Parameters.AddWithValue("@k", key);
        return string.Equals(cmd.ExecuteScalar()?.ToString(), expectedValue, StringComparison.Ordinal);
    }

    private static Dictionary<string, long> ReadPlaintextUserTableCounts(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        var tableNames = new List<string>();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT name
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%'
ORDER BY name;";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        foreach (var tableName in tableNames)
        {
            counts[tableName] = CountRows(conn, tableName);
        }

        return counts;
    }

    private void AssertOriginalRowsPreserved(string dbPath, IReadOnlyDictionary<string, long> originalRowCounts)
    {
        using var conn = OpenConnection(dbPath);
        foreach (var (tableName, originalCount) in originalRowCounts)
        {
            Assert.IsTrue(TableExists(dbPath, tableName), $"Original table should still exist after migration: {tableName}");
            var upgradedCount = CountRows(conn, tableName);
            Assert.IsTrue(
                upgradedCount >= originalCount,
                $"Table {tableName} lost rows during migration. Before={originalCount}, After={upgradedCount}");
        }
    }

    private static long CountRows(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM {QuoteIdentifier(tableName)};";
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static void AssertPlainSqliteCannotRead(string dbPath)
    {
        Assert.Throws<SqliteException>(() =>
        {
            using var conn = new SqliteConnection($"Data Source={dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
            cmd.ExecuteScalar();
        });
    }

    private static void AssertPlainSqliteCanRead(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master;";
        var result = cmd.ExecuteScalar();
        Assert.IsNotNull(result);
    }
}
