BEGIN TRANSACTION;

-- Mirrors ProductRepository.Create(...)
-- INSERT INTO products (
--   sku, name, barcode, unit, price_cents, cost_price_cents,
--   tax_category_id, tax_group_id,
--   quantity, quantity_store, quantity_warehouse,
--   min_threshold_store, min_threshold_warehouse,
--   purchased_at, last_sale_at, cashier_name, created_at, updated_at
-- )
--
-- Notes:
-- - quantity is intentionally duplicated from quantity_store to match the repository.
-- - tax_group_id points to the auto-managed profile created by seed_tax_rule_variants.sql.
-- - timestamps use ISO UTC text, matching the C# repository shape.

WITH seed_products(
    sku,
    name,
    barcode,
    unit,
    price_cents,
    cost_price_cents,
    tax_category_id,
    tax_profile_name,
    quantity_store,
    quantity_warehouse,
    min_threshold_store,
    min_threshold_warehouse
) AS (
    VALUES
        ('TAX-001', 'Bottle of Juice',      '100000000001', 'each',  250,   140, 1, '_auto_Standard Percentage Inclusive 7%',      20.0, 50.0, 5.0, 10.0),
        ('TAX-002', 'Chocolate Bar',        '100000000002', 'each',  100,    55, 1, '_auto_Standard Percentage Exclusive 7%',      25.0, 40.0, 5.0, 10.0),
        ('TAX-003', 'Imported Perfume',     '100000000003', 'each', 2500,  1400, 1, '_auto_Fixed Amount Inclusive $1.00',          8.0, 12.0, 2.0, 5.0),
        ('TAX-004', 'Battery Pack',         '100000000004', 'each',  800,   450, 1, '_auto_Fixed Amount Exclusive $1.00',         18.0, 30.0, 5.0, 10.0),
        ('TAX-005', 'Luxury Watch',         '100000000005', 'each',50000, 32000, 1, '_auto_Tiered Rate Inclusive',                  3.0,  6.0, 1.0, 2.0),
        ('TAX-006', 'Premium Handbag',      '100000000006', 'each',35000, 21000, 1, '_auto_Tiered Rate Exclusive',                  4.0,  8.0, 1.0, 2.0),
        ('TAX-007', 'Fuel by Liter',        '100000000007', 'liter', 180,   120, 1, '_auto_Per Unit Inclusive $0.20',             150.0, 300.0, 20.0, 50.0),
        ('TAX-008', 'Rice by Kg',           '100000000008', 'kg',    320,   210, 1, '_auto_Per Unit Exclusive $0.20',             100.0, 200.0, 15.0, 30.0),
        ('TAX-009', 'Banana',               '100000000009', 'each', 1000,   500, 1, '_auto_Percentage on Margin Inclusive 10%',    30.0, 60.0, 10.0, 20.0),
        ('TAX-010', 'Used Phone',           '100000000010', 'each',12000,  8000, 1, '_auto_Percentage on Margin Exclusive 10%',    6.0, 10.0, 2.0, 5.0),
        ('TAX-011', 'B2B Laptop Sale',      '100000000011', 'each',90000, 70000, 1, '_auto_Reverse Charge Inclusive',               2.0,  4.0, 1.0, 2.0),
        ('TAX-012', 'Industrial Equipment', '100000000012', 'each',250000,190000, 1, '_auto_Reverse Charge Exclusive',               1.0,  2.0, 1.0, 1.0)
)
INSERT INTO products (
    sku,
    name,
    barcode,
    unit,
    price_cents,
    cost_price_cents,
    tax_category_id,
    tax_group_id,
    quantity,
    quantity_store,
    quantity_warehouse,
    min_threshold_store,
    min_threshold_warehouse,
    purchased_at,
    last_sale_at,
    cashier_name,
    created_at,
    updated_at
)
SELECT
    sp.sku,
    sp.name,
    sp.barcode,
    sp.unit,
    sp.price_cents,
    sp.cost_price_cents,
    sp.tax_category_id,
    tg.id,
    sp.quantity_store,
    sp.quantity_store,
    sp.quantity_warehouse,
    sp.min_threshold_store,
    sp.min_threshold_warehouse,
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
    NULL,
    'Seed Script',
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM seed_products sp
JOIN tax_groups tg
    ON tg.name = sp.tax_profile_name
WHERE NOT EXISTS (
    SELECT 1
    FROM products p
    WHERE p.sku = sp.sku
);

COMMIT;
