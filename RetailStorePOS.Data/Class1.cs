using Microsoft.Data.Sqlite;

namespace RetailStorePOS.Data;

public static class DatabaseInitializer
{
    public static void Initialize(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path is required.", nameof(databasePath));
        }

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();

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
    updated_at TEXT NOT NULL
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

-- Add tax_category_id to products if not exists
-- SQLite doesn't support ADD COLUMN IF NOT EXISTS, so we check via pragma
-- For new databases, we'll create the column. For existing, we'll handle in migration.

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

-- ═══════════════════════════════════════════════════════════════════════
-- TAX AUTHORITIES — Who receives the money
-- Covers: Behavior #3 (Tax Authority Reporting)
-- ═══════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS tax_authorities (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL,
    authority_code TEXT NOT NULL UNIQUE,
    registration_number TEXT,
    created_at TEXT NOT NULL
);

-- ═══════════════════════════════════════════════════════════════════════
-- TAX RULES — The Heart of the Calculation Engine (Hot Cache)
-- Covers: Models 1-10, Behaviors 1, 7, 8
-- calc_type: PERCENTAGE | FIXED_AMOUNT | TIERED | PER_UNIT_MEASURE
--            | PERCENTAGE_ON_MARGIN | REVERSE_CHARGE
-- scope:     PRODUCT | ORDER
-- ═══════════════════════════════════════════════════════════════════════
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

-- ═══════════════════════════════════════════════════════════════════════
-- TAX RULE TIERS — Graduated Bracket Rates
-- Covers: Model 9 (Tiered/Slab Rates)
-- Only used when parent tax_rules.calc_type = 'TIERED'
-- ═══════════════════════════════════════════════════════════════════════
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

-- ═══════════════════════════════════════════════════════════════════════
-- TAX GROUPS — Named Tax Combinations
-- 'Standard Retail' = State + County + City taxes
-- 'Tax Exempt' = empty group (no rules)
-- ═══════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS tax_groups (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    is_default INTEGER NOT NULL DEFAULT 0,
    is_active INTEGER NOT NULL DEFAULT 1,
    created_at TEXT NOT NULL
);

-- ═══════════════════════════════════════════════════════════════════════
-- TAX GROUP RULES — Many-to-Many Junction (Groups <-> Rules)
-- Covers: Models 3 (Additive) and 4 (Compounding)
-- Same sequence_order = additive. Higher = compounding.
-- ═══════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS tax_group_rules (
    group_id INTEGER NOT NULL,
    rule_id INTEGER NOT NULL,
    sequence_order INTEGER NOT NULL DEFAULT 1,
    PRIMARY KEY (group_id, rule_id),
    FOREIGN KEY (group_id) REFERENCES tax_groups(id) ON DELETE CASCADE,
    FOREIGN KEY (rule_id) REFERENCES tax_rules(id) ON DELETE CASCADE
);

-- ═══════════════════════════════════════════════════════════════════════
-- TAX RULE RATE HISTORY — Cold Audit Archive (Dual-Write Pattern)
-- Covers: Behavior #7 (Rate Change Versioning)
-- Checkout reads ONLY tax_rules.rate_value (hot). This is for audits.
-- ═══════════════════════════════════════════════════════════════════════
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

-- ═══════════════════════════════════════════════════════════════════════
-- ORDER TAX OVERRIDES — Audit Defense Table
-- Covers: Behavior #6 (Tax Override Audit Trail), Model 11 (Customer Exemptions)
-- Two-person authorization chain: cashier initiates, manager approves.
-- ═══════════════════════════════════════════════════════════════════════
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

-- ═══════════════════════════════════════════════════════════════════════
-- TENDER TYPES — Payment Methods & Tax Exemption Flags
-- Covers: Behavior #9 (Payment-Method Tax Exemptions — SNAP/EBT/WIC)
-- ═══════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS tender_types (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE,
    is_tax_exempt_tender INTEGER NOT NULL DEFAULT 0,
    exempt_product_scope TEXT,
    is_active INTEGER NOT NULL DEFAULT 1
);

-- ═══════════════════════════════════════════════════════════════════════
-- BUNDLE COMPONENTS — Composite Product Breakdown
-- Covers: Behavior #10 (Proportional Bundle Tax Allocation)
-- ═══════════════════════════════════════════════════════════════════════
CREATE TABLE IF NOT EXISTS bundle_components (
    bundle_product_id INTEGER NOT NULL,
    component_product_id INTEGER NOT NULL,
    quantity REAL NOT NULL DEFAULT 1,
    component_retail_price_cents INTEGER NOT NULL,
    PRIMARY KEY (bundle_product_id, component_product_id),
    FOREIGN KEY (bundle_product_id) REFERENCES products(id) ON DELETE CASCADE,
    FOREIGN KEY (component_product_id) REFERENCES products(id) ON DELETE CASCADE
);

-- Seed: Default tender types
INSERT INTO tender_types (name, is_tax_exempt_tender, is_active)
SELECT 'Cash', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM tender_types WHERE name = 'Cash');

INSERT INTO tender_types (name, is_tax_exempt_tender, is_active)
SELECT 'Card', 0, 1
WHERE NOT EXISTS (SELECT 1 FROM tender_types WHERE name = 'Card');

-- Seed: Default tax group (empty = no tax)
INSERT INTO tax_groups (name, is_default, is_active, created_at)
SELECT 'No Tax', 1, 1, datetime('now')
WHERE NOT EXISTS (SELECT 1 FROM tax_groups WHERE name = 'No Tax');

-- Seed: Rounding settings
INSERT INTO settings (key, value)
SELECT 'tax_rounding_strategy', 'PER_LINE'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'tax_rounding_strategy');

INSERT INTO settings (key, value)
SELECT 'cash_rounding_unit', '0.01'
WHERE NOT EXISTS (SELECT 1 FROM settings WHERE key = 'cash_rounding_unit');
";
        command.ExecuteNonQuery();

        // Run migrations for existing databases
        RunMigrations(connection);
    }

    private static void RunMigrations(SqliteConnection connection)
    {
        // Migration 1: Add tax_category_id to products if missing
        if (!ColumnExists(connection, "products", "tax_category_id"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN tax_category_id INTEGER DEFAULT 1;";
            cmd.ExecuteNonQuery();
        }

        // Migration 2: Add tax columns to sale_items if missing
        if (!ColumnExists(connection, "sale_items", "tax_rate_percent"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN tax_rate_percent REAL NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "sale_items", "tax_cents"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN tax_cents INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }

        // Migration 3: Add can_override_price to users if missing
        if (!ColumnExists(connection, "users", "can_override_price"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE users ADD COLUMN can_override_price INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        // Migration 4: Add quantity to products if missing
        if (!ColumnExists(connection, "products", "quantity"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN quantity REAL NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }

        // Migration 5: Add dual-zone inventory columns if missing
        if (!ColumnExists(connection, "products", "quantity_store"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN quantity_store REAL NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();

            using var backfillCmd = connection.CreateCommand();
            backfillCmd.CommandText = @"
UPDATE products
SET quantity_store = quantity
WHERE COALESCE(quantity_store, 0) = 0
  AND COALESCE(quantity, 0) <> 0;";
            backfillCmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "products", "quantity_warehouse"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN quantity_warehouse REAL NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "products", "min_threshold_store"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN min_threshold_store REAL NOT NULL DEFAULT 5;";
            cmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "products", "min_threshold_warehouse"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN min_threshold_warehouse REAL NOT NULL DEFAULT 10;";
            cmd.ExecuteNonQuery();
        }

        // Migration 6: Add cost_price_cents to products if missing
        if (!ColumnExists(connection, "products", "cost_price_cents"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN cost_price_cents INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
        if (!ColumnExists(connection, "products", "purchased_at"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN purchased_at TEXT;";
            cmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "products", "last_sale_at"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN last_sale_at TEXT;";
            cmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "products", "cashier_name"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN cashier_name TEXT NOT NULL DEFAULT '';";
            cmd.ExecuteNonQuery();
        }

        if (!ColumnExists(connection, "sales", "cashier_name"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE sales ADD COLUMN cashier_name TEXT NOT NULL DEFAULT '';";
            cmd.ExecuteNonQuery();
        }

        using (var backfillPurchasedAt = connection.CreateCommand())
        {
            backfillPurchasedAt.CommandText = @"
UPDATE products
SET purchased_at = COALESCE(purchased_at, created_at)
WHERE purchased_at IS NULL;";
            backfillPurchasedAt.ExecuteNonQuery();
        }

        // Migration 7: Add tax_group_id to products (new tax engine)
        if (!ColumnExists(connection, "products", "tax_group_id"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE products ADD COLUMN tax_group_id INTEGER REFERENCES tax_groups(id);";
            cmd.ExecuteNonQuery();
        }

        // Migration 8: Add tax_snapshot JSON to sale_items (immutable receipt history)
        if (!ColumnExists(connection, "sale_items", "tax_snapshot"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN tax_snapshot TEXT;";
            cmd.ExecuteNonQuery();
        }

        // Migration 9: Add refund link to sale_items (Behavior #4 — never recalculate)
        if (!ColumnExists(connection, "sale_items", "original_sale_item_id"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE sale_items ADD COLUMN original_sale_item_id INTEGER;";
            cmd.ExecuteNonQuery();
        }

        // Migration 10: Backfill tax_group_id from legacy tax_categories
        if (ColumnExists(connection, "products", "tax_category_id") &&
            ColumnExists(connection, "products", "tax_group_id") &&
            TableExists(connection, "tax_categories"))
        {
            using var cmd = connection.CreateCommand();
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

        // Migration 11: Add is_auto_managed to tax_groups
        if (!ColumnExists(connection, "tax_groups", "is_auto_managed"))
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "ALTER TABLE tax_groups ADD COLUMN is_auto_managed INTEGER NOT NULL DEFAULT 0;";
            cmd.ExecuteNonQuery();
        }
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

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
        cmd.Parameters.AddWithValue("@name", tableName);
        return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
    }
}

