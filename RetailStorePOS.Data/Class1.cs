using Microsoft.Data.Sqlite;
using RetailStorePOS.Data.Modules.Contracts;
using System.Reflection;

namespace RetailStorePOS.Data.Modules.Migrations;

public static class DatabaseInitializer
{
    public static void Initialize(string databasePath)
    {
        var localDatabasePath = MigrationPlatformContract.NormalizeDatabasePath(databasePath);
        var databaseAlreadyExisted = File.Exists(localDatabasePath);

        var directory = Path.GetDirectoryName(localDatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encryptionResult = DatabaseEncryptionService.PrepareDatabaseAccess(localDatabasePath);
        using var connection = DatabaseEncryptionService.OpenPreparedConnection(localDatabasePath, encryptionResult);

        // Downgrade prevention: refuse to open a database created by a newer app version.
        // This protects against data corruption when a user installs an older version.
        EnforceMinimumAppVersion(connection);

        using var command = connection.CreateCommand();
        command.CommandText = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS products (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sku TEXT,
    name TEXT NOT NULL,
    barcode TEXT,
    unit TEXT,
    price_cents INTEGER NOT NULL,
    cost_price_cents INTEGER NOT NULL DEFAULT 0,
    tax_category_id INTEGER DEFAULT 1,
    quantity REAL NOT NULL DEFAULT 0,
    quantity_store REAL NOT NULL DEFAULT 0,
    quantity_warehouse REAL NOT NULL DEFAULT 0,
    min_threshold_store REAL NOT NULL DEFAULT 5,
    min_threshold_warehouse REAL NOT NULL DEFAULT 10,
    purchased_at TEXT,
    last_sale_at TEXT,
    cashier_name TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    thumbnail_path TEXT,
    product_dna TEXT
);

CREATE INDEX IF NOT EXISTS idx_products_barcode ON products (barcode);
CREATE INDEX IF NOT EXISTS idx_products_name ON products (name);

CREATE TABLE IF NOT EXISTS sales (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    receipt_number INTEGER NOT NULL,
    subtotal_cents INTEGER NOT NULL,
    tax_cents INTEGER NOT NULL,
    total_cents INTEGER NOT NULL,
    tendered_cents INTEGER NOT NULL,
    change_cents INTEGER NOT NULL,
    payment_type TEXT NOT NULL,
    cashier_name TEXT NOT NULL DEFAULT '',
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS sale_items (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id INTEGER NOT NULL,
    product_id INTEGER NOT NULL,
    name TEXT NOT NULL,
    barcode TEXT,
    price_cents INTEGER NOT NULL,
    quantity REAL NOT NULL,
    tax_rate_percent REAL NOT NULL DEFAULT 0,
    tax_cents INTEGER NOT NULL DEFAULT 0,
    total_cents INTEGER NOT NULL,
    tax_snapshot TEXT,
    FOREIGN KEY (sale_id) REFERENCES sales(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS schema_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS telemetry_outbox (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    created_at TEXT NOT NULL,
    endpoint TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    merge_duplicates INTEGER NOT NULL DEFAULT 0,
    attempt_count INTEGER NOT NULL DEFAULT 0,
    last_attempt_at TEXT NULL,
    last_error TEXT NULL,
    sent_at TEXT NULL
);

CREATE INDEX IF NOT EXISTS idx_telemetry_outbox_sent_created
    ON telemetry_outbox (sent_at, created_at);

CREATE TABLE IF NOT EXISTS installation_events (
    id TEXT PRIMARY KEY,
    install_id TEXT NOT NULL,
    event_type TEXT NOT NULL,
    occurred_at TEXT NOT NULL,
    payload_json TEXT NOT NULL,
    created_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_installation_events_install_created
    ON installation_events (install_id, created_at);

CREATE INDEX IF NOT EXISTS idx_installation_events_type_occurred
    ON installation_events (event_type, occurred_at);

CREATE TABLE IF NOT EXISTS receipt_sequence (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    next_value INTEGER NOT NULL
);

INSERT INTO receipt_sequence (id, next_value)
SELECT 1, 1
WHERE NOT EXISTS (SELECT 1 FROM receipt_sequence WHERE id = 1);

INSERT INTO settings (key, value)
SELECT 'currency_code', 'USD'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'currency_code');

INSERT INTO settings (key, value)
SELECT 'tax_enabled', '0'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'tax_enabled');

INSERT INTO settings (key, value)
SELECT 'tax_rate_percent', '0'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'tax_rate_percent');

CREATE TABLE IF NOT EXISTS tax_categories (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    rate_percent REAL NOT NULL DEFAULT 0,
    is_default INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_tax_categories_name ON tax_categories (name);

-- Insert default tax categories if not exist
INSERT INTO tax_categories (name, rate_percent, is_default)
SELECT 'No Tax', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM tax_categories WHERE name = 'No Tax');

INSERT INTO tax_categories (name, rate_percent, is_default)
SELECT 'Standard', 10, 0
WHERE NOT EXISTS (SELECT 1 FROM tax_categories WHERE name = 'Standard');

INSERT INTO tax_categories (name, rate_percent, is_default)
SELECT 'Reduced', 5, 0
WHERE NOT EXISTS (SELECT 1 FROM tax_categories WHERE name = 'Reduced');

CREATE TABLE IF NOT EXISTS users (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    username TEXT NOT NULL UNIQUE,
    display_name TEXT NOT NULL,
    password_hash TEXT,
    pin_hash TEXT,
    is_admin INTEGER NOT NULL DEFAULT 0,
    can_checkout INTEGER NOT NULL DEFAULT 1,
    can_manage_products INTEGER NOT NULL DEFAULT 0,
    can_manage_settings INTEGER NOT NULL DEFAULT 0,
    can_manage_users INTEGER NOT NULL DEFAULT 0,
    can_view_reports INTEGER NOT NULL DEFAULT 0,
    can_close_day INTEGER NOT NULL DEFAULT 0,
    can_override_price INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    failed_login_count INTEGER NOT NULL DEFAULT 0,
    last_failed_login_at_utc TEXT,
    locked_until_utc TEXT,
    must_change_password INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_users_username ON users (username);
CREATE INDEX IF NOT EXISTS idx_users_pin ON users (pin_hash);

CREATE TABLE IF NOT EXISTS sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    session_token TEXT NOT NULL UNIQUE,
    started_at TEXT NOT NULL,
    last_activity TEXT NOT NULL,
    is_active INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE TABLE IF NOT EXISTS audit_logs (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER,
    action TEXT NOT NULL,
    details TEXT,
    created_at TEXT NOT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs (created_at);
CREATE INDEX IF NOT EXISTS idx_audit_logs_user_id ON audit_logs (user_id);

CREATE TABLE IF NOT EXISTS tax_authorities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    authority_code TEXT NOT NULL UNIQUE,
    registration_number TEXT,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS tax_rules (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    tax_authority_id INTEGER,
    calc_type TEXT NOT NULL,
    scope TEXT NOT NULL DEFAULT 'PRODUCT',
    rate_value REAL NOT NULL DEFAULT 0,
    is_inclusive INTEGER NOT NULL DEFAULT 0,
    applies_after_discount INTEGER NOT NULL DEFAULT 1,
    sequence_order INTEGER NOT NULL DEFAULT 1,
    min_tax_amount_cents INTEGER,
    max_tax_amount_cents INTEGER,
    threshold_min_cents INTEGER,
    threshold_max_cents INTEGER,
    threshold_scope TEXT,
    effective_from TEXT,
    effective_until TEXT,
    is_active INTEGER NOT NULL DEFAULT 1,
    is_default INTEGER NOT NULL DEFAULT 0,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (tax_authority_id) REFERENCES tax_authorities(id)
);

CREATE INDEX IF NOT EXISTS idx_tax_rules_active
    ON tax_rules (is_active, effective_from, effective_until);

CREATE TABLE IF NOT EXISTS tax_rule_tiers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tax_rule_id INTEGER NOT NULL,
    bracket_min_cents INTEGER NOT NULL,
    bracket_max_cents INTEGER,
    tier_rate_value REAL NOT NULL,
    FOREIGN KEY (tax_rule_id) REFERENCES tax_rules(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_tax_rule_tiers_rule
    ON tax_rule_tiers (tax_rule_id);

CREATE TABLE IF NOT EXISTS tax_groups (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    is_default INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS tax_group_rules (
    group_id INTEGER NOT NULL,
    rule_id INTEGER NOT NULL,
    sequence_order INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (group_id, rule_id),
    FOREIGN KEY (group_id) REFERENCES tax_groups(id) ON DELETE CASCADE,
    FOREIGN KEY (rule_id) REFERENCES tax_rules(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS tax_rule_rate_history (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    tax_rule_id INTEGER NOT NULL,
    old_rate_value REAL NOT NULL,
    new_rate_value REAL NOT NULL,
    changed_at TEXT NOT NULL,
    changed_by_user_id INTEGER,
    FOREIGN KEY (tax_rule_id) REFERENCES tax_rules(id),
    FOREIGN KEY (changed_by_user_id) REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS idx_tax_rate_history_rule
    ON tax_rule_rate_history (tax_rule_id, changed_at);

CREATE TABLE IF NOT EXISTS order_tax_overrides (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id INTEGER NOT NULL,
    tax_rule_id INTEGER,
    original_tax_cents INTEGER NOT NULL,
    override_tax_cents INTEGER NOT NULL DEFAULT 0,
    reason_code TEXT NOT NULL,
    certificate_number TEXT,
    override_by_user_id INTEGER NOT NULL,
    approved_by_user_id INTEGER,
    created_at TEXT NOT NULL,
    FOREIGN KEY (sale_id) REFERENCES sales(id),
    FOREIGN KEY (tax_rule_id) REFERENCES tax_rules(id),
    FOREIGN KEY (override_by_user_id) REFERENCES users(id),
    FOREIGN KEY (approved_by_user_id) REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS idx_order_tax_overrides_sale
    ON order_tax_overrides (sale_id);

CREATE TABLE IF NOT EXISTS tender_types (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    is_tax_exempt_tender INTEGER NOT NULL DEFAULT 0,
    exempt_product_scope TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS bundle_components (
    bundle_product_id INTEGER NOT NULL,
    component_product_id INTEGER NOT NULL,
    quantity REAL NOT NULL DEFAULT 1,
    component_retail_price_cents INTEGER NOT NULL,
    PRIMARY KEY (bundle_product_id, component_product_id),
    FOREIGN KEY (bundle_product_id) REFERENCES products(id) ON DELETE CASCADE,
    FOREIGN KEY (component_product_id) REFERENCES products(id) ON DELETE CASCADE
);

-- Seed defaults
INSERT INTO tender_types (name, is_tax_exempt_tender, is_active)
SELECT 'Cash', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM tender_types WHERE name = 'Cash');

INSERT INTO tender_types (name, is_tax_exempt_tender, is_active)
SELECT 'Card', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM tender_types WHERE name = 'Card');

INSERT INTO tax_groups (name, is_default, is_active, created_at)
SELECT 'No Tax', 1, 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM tax_groups WHERE name = 'No Tax');

INSERT INTO settings (key, value)
SELECT 'tax_rounding_strategy', 'PER_LINE'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'tax_rounding_strategy');

INSERT INTO settings (key, value)
SELECT 'cash_rounding_unit', '0.01'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'cash_rounding_unit');
";
        command.ExecuteNonQuery();

        // Ensure columns referenced by startup-time repositories exist even if their
        // full migration (backfill/indexing) is deferred to the maintenance path.
        EnsureStartupSchemaCompatibility(connection);

        // Run only StartupSafe migrations for the splash path
        var migrationResult = RunMigrations(
            connection,
            MigrationCategory.StartupSafe,
            allowOutOfOrderStartupMigrations: !databaseAlreadyExisted);
        RefreshSchemaMetadata(
            connection,
            migrationResult,
            databaseJustCreated: !databaseAlreadyExisted,
            encryptionResult: encryptionResult);
    }

    public static (int Version, bool Success) RunMaintenanceMigrations(string databasePath)
    {
        var localDatabasePath = MigrationPlatformContract.NormalizeDatabasePath(databasePath);
        var encryptionResult = DatabaseEncryptionService.PrepareDatabaseAccess(localDatabasePath);
        using var connection = DatabaseEncryptionService.OpenPreparedConnection(localDatabasePath, encryptionResult);

        EnforceMinimumAppVersion(connection);

        try
        {
            var currentVersion = GetUserVersion(connection);
            var pendingHeavyMigration = GetPendingHeavyMigration(GetMigrationSteps(), currentVersion);
            var backupPath = pendingHeavyMigration is null
                ? null
                : CreatePreMigrationBackup(localDatabasePath, connection, currentVersion, pendingHeavyMigration.Version);

            // Task 7.4 Validation: Pre-migration integrity check
            ValidateDatabase(connection, "pre_maintenance");

            // Run all pending migrations regardless of category
            var migrationResult = RunMigrations(connection);

            // Task 7.4 Validation: Post-migration final check
            ValidateDatabase(connection, "post_maintenance");
            RefreshSchemaMetadata(connection, migrationResult, backupPath: backupPath, encryptionResult: encryptionResult);

            return (GetUserVersion(connection), true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Maintenance migration failed: {ex}");
            throw;
        }
    }

    private static void ValidateDatabase(SqliteConnection connection, string phase)
    {
        try
        {
            using var cmd = connection.CreateCommand();

            // Core SQLite integrity check
            cmd.CommandText = "PRAGMA quick_check;";
            var result = cmd.ExecuteScalar()?.ToString();
            if (result != "ok")
            {
                throw new InvalidOperationException($"Database quick_check failed during {phase}: {result}");
            }

            // Relationship integrity check
            cmd.CommandText = "PRAGMA foreign_key_check;";
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                throw new InvalidOperationException($"Database foreign_key_check failed during {phase}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Database validation failed: {ex.Message}");
            throw;
        }
    }

    private static MigrationExecutionSummary RunMigrations(
        SqliteConnection connection,
        MigrationCategory? maxCategory = null,
        bool allowOutOfOrderStartupMigrations = false)
    {
        var currentVersion = GetUserVersion(connection);
        var startingVersion = currentVersion;
        var migrations = GetMigrationSteps();
        var lastAppliedVersion = currentVersion;
        var appliedAny = false;

        foreach (var migration in migrations.Where(m => m.Version > currentVersion))
        {
            if (maxCategory.HasValue && migration.Category > maxCategory.Value)
            {
                // Existing databases must preserve linear migration order.
                // Freshly created databases already have the latest baseline schema,
                // so startup can safely continue applying later additive/index steps.
                if (allowOutOfOrderStartupMigrations)
                {
                    continue;
                }

                break;
            }

            if (maxCategory == MigrationCategory.StartupSafe && !MigrationPlatformContract.ShouldRunInStartupPath(migration.Category))
            {
                if (allowOutOfOrderStartupMigrations)
                {
                    continue;
                }

                break;
            }

            // Task 7.4: Ensure each step is isolated and validated
            using var transaction = connection.BeginTransaction();
            try
            {
                migration.Execute(connection);

                // Update version BEFORE committing so it's atomic with the change
                using var verCmd = connection.CreateCommand();
                verCmd.Transaction = transaction;
                verCmd.CommandText = $"PRAGMA user_version = {migration.Version};";
                verCmd.ExecuteNonQuery();

                // For heavy migrations, validate before committing
                if (MigrationPlatformContract.ShouldValidateBeforeCommit(migration.Category))
                {
                    ValidateDatabase(connection, $"step_{migration.Version}");
                }

                transaction.Commit();
                appliedAny = true;
                lastAppliedVersion = migration.Version;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Migration {migration.Version} failed: {ex.Message}");
                throw;
            }
        }

        return new MigrationExecutionSummary
        {
            StartingVersion = startingVersion,
            EndingVersion = appliedAny ? lastAppliedVersion : startingVersion,
            AppliedAny = appliedAny
        };
    }

    private static int GetUserVersion(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetUserVersion(SqliteConnection connection, int version)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA user_version = {version};";
        cmd.ExecuteNonQuery();
    }

    private static List<MigrationStep> GetMigrationSteps()
    {
        return new List<MigrationStep>
        {
            new MigrationStep
            {
                Version = 1,
                Description = "Add tax_category_id to products",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "tax_category_id"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN tax_category_id INTEGER DEFAULT 1;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 2,
                Description = "Add tax columns to sale_items",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "sale_items", "tax_rate_percent"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN tax_rate_percent REAL NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                    if (!ColumnExists(conn, "sale_items", "tax_cents"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN tax_cents INTEGER NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 3,
                Description = "Add can_override_price to users",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "users", "can_override_price"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE users ADD COLUMN can_override_price INTEGER NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 4,
                Description = "Add quantity to products",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "quantity"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN quantity REAL NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 5,
                Description = "Add dual-zone inventory columns and backfill",
                Category = MigrationCategory.HeavyBackfill,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "quantity_store"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN quantity_store REAL NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();

                        using var backfillCmd = conn.CreateCommand();
                        backfillCmd.CommandText = @"
UPDATE products
SET quantity_store = quantity
WHERE COALESCE(quantity_store, 0) = 0
  AND COALESCE(quantity, 0) <> 0;";
                        backfillCmd.ExecuteNonQuery();
                    }

                    if (!ColumnExists(conn, "products", "quantity_warehouse"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN quantity_warehouse REAL NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }

                    if (!ColumnExists(conn, "products", "min_threshold_store"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN min_threshold_store REAL NOT NULL DEFAULT 5;";
                        cmd.ExecuteNonQuery();
                    }

                    if (!ColumnExists(conn, "products", "min_threshold_warehouse"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN min_threshold_warehouse REAL NOT NULL DEFAULT 10;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 6,
                Description = "Add cost_price_cents to products and backfill purchased_at",
                Category = MigrationCategory.HeavyBackfill,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "cost_price_cents"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN cost_price_cents INTEGER NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                    if (!ColumnExists(conn, "products", "purchased_at"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN purchased_at TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                    if (!ColumnExists(conn, "products", "last_sale_at"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN last_sale_at TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                    if (!ColumnExists(conn, "products", "cashier_name"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN cashier_name TEXT NOT NULL DEFAULT '';";
                        cmd.ExecuteNonQuery();
                    }
                    if (!ColumnExists(conn, "sales", "cashier_name"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE sales ADD COLUMN cashier_name TEXT NOT NULL DEFAULT '';";
                        cmd.ExecuteNonQuery();
                    }
                    using var backfillPurchasedAt = conn.CreateCommand();
                    backfillPurchasedAt.CommandText = @"
UPDATE products
SET purchased_at = COALESCE(purchased_at, created_at)
WHERE purchased_at IS NULL;";
                    backfillPurchasedAt.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 7,
                Description = "Add tax_group_id to products",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "tax_group_id"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN tax_group_id INTEGER REFERENCES tax_groups(id);";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 8,
                Description = "Add tax_snapshot JSON to sale_items",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "sale_items", "tax_snapshot"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN tax_snapshot TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 9,
                Description = "Add refund link to sale_items",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "sale_items", "original_sale_item_id"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN original_sale_item_id INTEGER;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 10,
                Description = "Backfill tax_group_id from legacy tax_categories",
                Category = MigrationCategory.HeavyBackfill,
                Execute = conn =>
                {
                    if (ColumnExists(conn, "products", "tax_category_id") &&
                        ColumnExists(conn, "products", "tax_group_id") &&
                        TableExists(conn, "tax_categories"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = @"
UPDATE products
SET tax_group_id = COALESCE(
    (SELECT tg.id FROM tax_groups tg
     JOIN tax_categories tc ON LOWER(TRIM(tc.name)) = LOWER(TRIM(tg.name))
     WHERE tc.id = products.tax_category_id),
    (SELECT id FROM tax_groups WHERE is_default = 1 LIMIT 1)
)
WHERE tax_group_id IS NULL;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 11,
                Description = "Add is_auto_managed to tax_groups",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "tax_groups", "is_auto_managed"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE tax_groups ADD COLUMN is_auto_managed INTEGER NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 12,
                Description = "Global Search Indices",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    using (var receiptIndexCmd = conn.CreateCommand())
                    {
                        receiptIndexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_sales_receipt ON sales (receipt_number);";
                        receiptIndexCmd.ExecuteNonQuery();
                    }

                    if (ColumnExists(conn, "sales", "cashier_name"))
                    {
                        using var cashierIndexCmd = conn.CreateCommand();
                        cashierIndexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_sales_cashier ON sales (cashier_name COLLATE NOCASE);";
                        cashierIndexCmd.ExecuteNonQuery();
                    }

                    using (var createdIndexCmd = conn.CreateCommand())
                    {
                        createdIndexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_sales_created ON sales (created_at);";
                        createdIndexCmd.ExecuteNonQuery();
                    }

                    using var paymentIndexCmd = conn.CreateCommand();
                    paymentIndexCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_sales_payment ON sales (payment_type);";
                    paymentIndexCmd.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 13,
                Description = "Add (install_id, occurred_at) index to installation_events",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_installation_events_install_occurred ON installation_events (install_id, occurred_at);";
                    cmd.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 14,
                Description = "Add run_id column to installation_events",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "installation_events", "run_id"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE installation_events ADD COLUMN run_id TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 15,
                Description = "Add estimation columns to installation_events",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "installation_events", "last_activity_at"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE installation_events ADD COLUMN last_activity_at TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                    if (!ColumnExists(conn, "installation_events", "last_activity_source"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE installation_events ADD COLUMN last_activity_source TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 16,
                Description = "Create register_sessions table",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS register_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    opening_amount_cents INTEGER NOT NULL,
    opening_note TEXT,
    opened_at TEXT NOT NULL,
    closed_at TEXT,
    closing_amount_cents INTEGER,
    closing_note TEXT,
    FOREIGN KEY (user_id) REFERENCES users(id)
);
CREATE INDEX IF NOT EXISTS idx_register_sessions_open ON register_sessions (closed_at);
CREATE INDEX IF NOT EXISTS idx_register_sessions_user ON register_sessions (user_id, opened_at);";
                    cmd.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 17,
                Description = "Create register_cash_adjustments table",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS register_cash_adjustments (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id INTEGER NOT NULL,
    user_id INTEGER,
    adjustment_type TEXT NOT NULL CHECK (adjustment_type IN ('IN', 'OUT')),
    amount_cents INTEGER NOT NULL,
    reason TEXT NOT NULL,
    created_at TEXT NOT NULL,
    FOREIGN KEY (session_id) REFERENCES register_sessions(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id)
);
CREATE INDEX IF NOT EXISTS idx_register_cash_adjustments_session ON register_cash_adjustments (session_id, created_at);
CREATE INDEX IF NOT EXISTS idx_register_cash_adjustments_user ON register_cash_adjustments (user_id, created_at);";
                    cmd.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 18,
                Description = "Add thumbnail_path to products",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "thumbnail_path"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE products ADD COLUMN thumbnail_path TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 19,
                Description = "Add product_dna column with unique index and backfill",
                Category = MigrationCategory.HeavyBackfill,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "products", "product_dna"))
                    {
                        using var addCol = conn.CreateCommand();
                        addCol.CommandText = "ALTER TABLE products ADD COLUMN product_dna TEXT;";
                        addCol.ExecuteNonQuery();
                    }

                    // Backfill existing products that have no DNA
                    using var selectCmd = conn.CreateCommand();
                    selectCmd.CommandText = "SELECT id, name, created_at FROM products WHERE product_dna IS NULL;";
                    using var reader = selectCmd.ExecuteReader();

                    var updates = new List<(long Id, string Dna)>();
                    while (reader.Read())
                    {
                        var id = reader.GetInt64(0);
                        var name = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        var createdAtStr = reader.IsDBNull(2) ? null : reader.GetString(2);

                        DateTime createdAt;
                        if (!string.IsNullOrWhiteSpace(createdAtStr) &&
                            DateTime.TryParse(createdAtStr, System.Globalization.CultureInfo.InvariantCulture,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                        {
                            createdAt = parsed;
                        }
                        else
                        {
                            createdAt = DateTime.UtcNow;
                        }

                        var dna = RetailStorePOS.Data.Modules.Products.ProductDnaGenerator.Generate(name, createdAt);
                        updates.Add((id, dna));
                    }
                    reader.Close();

                    // Ensure uniqueness: if a collision occurs, append the product ID
                    var usedDnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using var updateCmd = conn.CreateCommand();
                    updateCmd.CommandText = "UPDATE products SET product_dna = @dna WHERE id = @id;";
                    var dnaParam = updateCmd.Parameters.Add("@dna", Microsoft.Data.Sqlite.SqliteType.Text);
                    var idParam = updateCmd.Parameters.Add("@id", Microsoft.Data.Sqlite.SqliteType.Integer);
                    updateCmd.Prepare();

                    foreach (var (id, dna) in updates)
                    {
                        var finalDna = dna;
                        if (!usedDnas.Add(finalDna))
                        {
                            // Collision detected — append product ID to make it unique
                            var suffix = id.ToString("X4");
                            finalDna = $"{finalDna}-{suffix}";
                        }
                        usedDnas.Add(finalDna);

                        dnaParam.Value = finalDna;
                        idParam.Value = id;
                        updateCmd.ExecuteNonQuery();
                    }

                    // Create unique index after all rows are backfilled
                    using var idxCmd = conn.CreateCommand();
                    idxCmd.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS idx_products_dna ON products(product_dna);";
                    idxCmd.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 20,
                Description = "Add authentication lockout columns to users",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "users", "failed_login_count"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE users ADD COLUMN failed_login_count INTEGER NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }

                    if (!ColumnExists(conn, "users", "last_failed_login_at_utc"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE users ADD COLUMN last_failed_login_at_utc TEXT;";
                        cmd.ExecuteNonQuery();
                    }

                    if (!ColumnExists(conn, "users", "locked_until_utc"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE users ADD COLUMN locked_until_utc TEXT;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 21,
                Description = "Add bootstrap password change flag to users",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    if (!ColumnExists(conn, "users", "must_change_password"))
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = "ALTER TABLE users ADD COLUMN must_change_password INTEGER NOT NULL DEFAULT 0;";
                        cmd.ExecuteNonQuery();
                    }
                }
            },
            new MigrationStep
            {
                Version = 22,
                Description = "Create schema_metadata table",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS schema_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL
);";
                    cmd.ExecuteNonQuery();
                }
            },
            new MigrationStep
            {
                Version = 23,
                Description = "Advanced reporting indexes (sales by created_at+payment, sale_items by sale+product)",
                Category = MigrationCategory.StartupSafe,
                Execute = conn =>
                {
                    using (var salesCreatedPaymentCmd = conn.CreateCommand())
                    {
                        salesCreatedPaymentCmd.CommandText =
                            "CREATE INDEX IF NOT EXISTS idx_sales_created_payment ON sales (created_at, payment_type);";
                        salesCreatedPaymentCmd.ExecuteNonQuery();
                    }

                    using (var saleItemsSaleProductCmd = conn.CreateCommand())
                    {
                        saleItemsSaleProductCmd.CommandText =
                            "CREATE INDEX IF NOT EXISTS idx_sale_items_sale_product ON sale_items (sale_id, product_id);";
                        saleItemsSaleProductCmd.ExecuteNonQuery();
                    }
                }
            }
        };
    }

    private static bool ColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (reader.GetString(1).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static MigrationStep? GetPendingHeavyMigration(IEnumerable<MigrationStep> migrations, int currentVersion)
    {
        return migrations
            .Where(m => m.Version > currentVersion && m.Category >= MigrationCategory.HeavyBackfill)
            .OrderBy(m => m.Version)
            .FirstOrDefault();
    }

    private static string CreatePreMigrationBackup(
        string databasePath,
        SqliteConnection connection,
        int currentVersion,
        int targetVersion)
    {
        EnsureCheckpointedDatabase(connection);

        connection.Close();
        try
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var backupPath = $"{databasePath}.pre-v{currentVersion}-to-v{targetVersion}-{timestamp}.bak";
            File.Copy(databasePath, backupPath, overwrite: false);
            return backupPath;
        }
        finally
        {
            connection.Open();
        }
    }

    private static void EnsureCheckpointedDatabase(SqliteConnection connection)
    {
        using var checkpointCommand = connection.CreateCommand();
        checkpointCommand.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        checkpointCommand.ExecuteNonQuery();
    }

    private static void RefreshSchemaMetadata(
        SqliteConnection connection,
        MigrationExecutionSummary migrationResult,
        bool databaseJustCreated = false,
        string? backupPath = null,
        EncryptionPreparationResult? encryptionResult = null)
    {
        EnsureSchemaMetadataTable(connection);

        var nowUtc = DateTime.UtcNow.ToString("O");
        var currentAppVersion = GetCurrentAppVersion();

        if (!SchemaMetadataExists(connection, "created_by_app_version"))
        {
            var createdByValue = databaseJustCreated
                ? currentAppVersion
                : "unknown-pre-schema-metadata";
            UpsertSchemaMetadata(connection, "created_by_app_version", createdByValue, nowUtc);
        }

        if (migrationResult.AppliedAny || !SchemaMetadataExists(connection, "min_app_version"))
        {
            UpsertSchemaMetadata(connection, "min_app_version", currentAppVersion, nowUtc);
        }

        if (migrationResult.AppliedAny || !SchemaMetadataExists(connection, "last_migration_at"))
        {
            UpsertSchemaMetadata(connection, "last_migration_at", nowUtc, nowUtc);
        }

        if (migrationResult.AppliedAny || !SchemaMetadataExists(connection, "last_migration_version"))
        {
            UpsertSchemaMetadata(connection, "last_migration_version", migrationResult.EndingVersion.ToString(), nowUtc);
        }

        if (!SchemaMetadataValueEquals(connection, "encryption_version", encryptionResult?.MetadataEncryptionVersion ?? DatabaseEncryptionService.CurrentEncryptionVersion))
        {
            UpsertSchemaMetadata(
                connection,
                "encryption_version",
                encryptionResult?.MetadataEncryptionVersion ?? DatabaseEncryptionService.CurrentEncryptionVersion,
                nowUtc);
        }

        if (!SchemaMetadataExists(connection, "current_schema_version") || migrationResult.AppliedAny)
        {
            UpsertSchemaMetadata(connection, "current_schema_version", GetUserVersion(connection).ToString(), nowUtc);
        }

        if (!string.IsNullOrWhiteSpace(backupPath))
        {
            UpsertSchemaMetadata(connection, "last_pre_migration_backup_path", backupPath, nowUtc);
            UpsertSchemaMetadata(connection, "last_pre_migration_backup_at", nowUtc, nowUtc);
        }

        if (encryptionResult?.WasMigratedToEncrypted == true)
        {
            UpsertSchemaMetadata(connection, "last_encryption_migration_at", nowUtc, nowUtc);
            if (!string.IsNullOrWhiteSpace(encryptionResult.PlaintextBackupPath))
            {
                UpsertSchemaMetadata(connection, "last_encryption_plaintext_backup_path", encryptionResult.PlaintextBackupPath, nowUtc);
            }

            if (!string.IsNullOrWhiteSpace(encryptionResult.EncryptedTempPath))
            {
                UpsertSchemaMetadata(connection, "last_encryption_temp_path", encryptionResult.EncryptedTempPath, nowUtc);
            }
        }
    }

    private static void EnsureSchemaMetadataTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS schema_metadata (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TEXT NOT NULL
);";
        cmd.ExecuteNonQuery();
    }

    private static bool SchemaMetadataExists(SqliteConnection connection, string key)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM schema_metadata WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    private static bool SchemaMetadataValueEquals(SqliteConnection connection, string key, string expectedValue)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM schema_metadata WHERE key = @key;";
        cmd.Parameters.AddWithValue("@key", key);
        return string.Equals(cmd.ExecuteScalar()?.ToString(), expectedValue, StringComparison.Ordinal);
    }

    private static void UpsertSchemaMetadata(SqliteConnection connection, string key, string value, string updatedAtUtc)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
INSERT INTO schema_metadata (key, value, updated_at)
VALUES (@key, @value, @updated_at)
ON CONFLICT(key) DO UPDATE SET
    value = excluded.value,
    updated_at = excluded.updated_at;";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@updated_at", updatedAtUtc);
        cmd.ExecuteNonQuery();
    }

    private static void EnsureStartupSchemaCompatibility(SqliteConnection connection)
    {
        if (TableExists(connection, "products") && !ColumnExists(connection, "products", "product_dna"))
        {
            using var addProductDnaColumn = connection.CreateCommand();
            addProductDnaColumn.CommandText = "ALTER TABLE products ADD COLUMN product_dna TEXT;";
            addProductDnaColumn.ExecuteNonQuery();
        }
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }

    /// <summary>
    /// The highest migration version this build of the app understands.
    /// Must be updated whenever a new MigrationStep is added to GetMigrationSteps().
    /// </summary>
    private const int MaxKnownSchemaVersion = 23;

    private static string GetCurrentAppVersion()
    {
        return typeof(DatabaseInitializer).Assembly
                   .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
               ?? typeof(DatabaseInitializer).Assembly.GetName().Version?.ToString()
               ?? "unknown";
    }

    /// <summary>
    /// Prevents opening a database that was migrated by a newer app version.
    /// A newer database may have columns, tables, or constraints this version
    /// doesn't understand — opening it risks silent data corruption.
    /// 
    /// For a brand-new database (user_version = 0), this always passes.
    /// </summary>
    private static void EnforceMinimumAppVersion(SqliteConnection connection)
    {
        var dbVersion = GetUserVersion(connection);

        if (dbVersion > MaxKnownSchemaVersion)
        {
            throw new DatabaseTooNewException(dbVersion, MaxKnownSchemaVersion);
        }
    }
}

internal sealed class MigrationExecutionSummary
{
    public int StartingVersion { get; init; }
    public int EndingVersion { get; init; }
    public bool AppliedAny { get; init; }
}

/// <summary>
/// Thrown when the local database was created or migrated by a newer version of the app.
/// The UI layer should catch this and show a user-friendly "please update" message.
/// </summary>
public sealed class DatabaseTooNewException : InvalidOperationException
{
    public int DatabaseVersion { get; }
    public int AppMaxVersion { get; }

    public DatabaseTooNewException(int databaseVersion, int appMaxVersion)
        : base($"This database requires a newer version of the application. " +
               $"Database schema version: {databaseVersion}, app supports up to: {appMaxVersion}. " +
               $"Please update the application. Downgrading is not supported.")
    {
        DatabaseVersion = databaseVersion;
        AppMaxVersion = appMaxVersion;
    }
}
