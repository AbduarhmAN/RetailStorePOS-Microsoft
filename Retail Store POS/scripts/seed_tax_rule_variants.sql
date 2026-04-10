BEGIN TRANSACTION;

-- Seed one shared authority for sample tax rules.
INSERT INTO tax_authorities (name, authority_code, registration_number, created_at)
SELECT 'Seed Tax Authority', 'SEED_AUTH', 'SEED-001', strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
WHERE NOT EXISTS (
    SELECT 1
    FROM tax_authorities
    WHERE authority_code = 'SEED_AUTH'
);

-- 12 base tax-rule variants = 6 calc types x 2 inclusive modes.
WITH seed_rules(name, calc_type, scope, rate_value, is_inclusive) AS (
    VALUES
        ('Standard Percentage Inclusive 7%', 'PERCENTAGE', 'PRODUCT', 7.0, 1),
        ('Standard Percentage Exclusive 7%', 'PERCENTAGE', 'PRODUCT', 7.0, 0),
        ('Fixed Amount Inclusive $1.00', 'FIXED_AMOUNT', 'PRODUCT', 1.0, 1),
        ('Fixed Amount Exclusive $1.00', 'FIXED_AMOUNT', 'PRODUCT', 1.0, 0),
        ('Tiered Rate Inclusive', 'TIERED', 'PRODUCT', 0.0, 1),
        ('Tiered Rate Exclusive', 'TIERED', 'PRODUCT', 0.0, 0),
        ('Per Unit Inclusive $0.20', 'PER_UNIT_MEASURE', 'PRODUCT', 0.2, 1),
        ('Per Unit Exclusive $0.20', 'PER_UNIT_MEASURE', 'PRODUCT', 0.2, 0),
        ('Percentage on Margin Inclusive 10%', 'PERCENTAGE_ON_MARGIN', 'PRODUCT', 10.0, 1),
        ('Percentage on Margin Exclusive 10%', 'PERCENTAGE_ON_MARGIN', 'PRODUCT', 10.0, 0),
        ('Reverse Charge Inclusive', 'REVERSE_CHARGE', 'PRODUCT', 0.0, 1),
        ('Reverse Charge Exclusive', 'REVERSE_CHARGE', 'PRODUCT', 0.0, 0)
)
INSERT INTO tax_rules (
    name,
    tax_authority_id,
    calc_type,
    scope,
    rate_value,
    is_inclusive,
    applies_after_discount,
    sequence_order,
    min_tax_amount_cents,
    max_tax_amount_cents,
    threshold_min_cents,
    threshold_max_cents,
    threshold_scope,
    effective_from,
    effective_until,
    is_active,
    is_default,
    created_at,
    updated_at
)
SELECT
    sr.name,
    ta.id,
    sr.calc_type,
    sr.scope,
    sr.rate_value,
    sr.is_inclusive,
    1,
    1,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    NULL,
    1,
    0,
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now'),
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM seed_rules sr
CROSS JOIN (
    SELECT id
    FROM tax_authorities
    WHERE authority_code = 'SEED_AUTH'
    LIMIT 1
) ta
WHERE NOT EXISTS (
    SELECT 1
    FROM tax_rules tr
    WHERE tr.name = sr.name
);

-- Keep the dual-write history table in sync with the repository behavior.
INSERT INTO tax_rule_rate_history (
    tax_rule_id,
    old_rate_value,
    new_rate_value,
    changed_at,
    changed_by_user_id
)
SELECT
    tr.id,
    0,
    tr.rate_value,
    tr.created_at,
    NULL
FROM tax_rules tr
WHERE tr.name IN (
    'Standard Percentage Inclusive 7%',
    'Standard Percentage Exclusive 7%',
    'Fixed Amount Inclusive $1.00',
    'Fixed Amount Exclusive $1.00',
    'Tiered Rate Inclusive',
    'Tiered Rate Exclusive',
    'Per Unit Inclusive $0.20',
    'Per Unit Exclusive $0.20',
    'Percentage on Margin Inclusive 10%',
    'Percentage on Margin Exclusive 10%',
    'Reverse Charge Inclusive',
    'Reverse Charge Exclusive'
)
AND NOT EXISTS (
    SELECT 1
    FROM tax_rule_rate_history h
    WHERE h.tax_rule_id = tr.id
);

-- Mirror GetOrCreateAutoManagedProfile(...): one auto-managed profile per rule.
INSERT INTO tax_groups (name, is_default, is_active, is_auto_managed, created_at)
SELECT
    '_auto_' || tr.name,
    0,
    1,
    1,
    strftime('%Y-%m-%dT%H:%M:%fZ', 'now')
FROM tax_rules tr
WHERE tr.name IN (
    'Standard Percentage Inclusive 7%',
    'Standard Percentage Exclusive 7%',
    'Fixed Amount Inclusive $1.00',
    'Fixed Amount Exclusive $1.00',
    'Tiered Rate Inclusive',
    'Tiered Rate Exclusive',
    'Per Unit Inclusive $0.20',
    'Per Unit Exclusive $0.20',
    'Percentage on Margin Inclusive 10%',
    'Percentage on Margin Exclusive 10%',
    'Reverse Charge Inclusive',
    'Reverse Charge Exclusive'
)
AND NOT EXISTS (
    SELECT 1
    FROM tax_groups tg
    WHERE tg.name = '_auto_' || tr.name
      AND COALESCE(tg.is_auto_managed, 0) = 1
);

INSERT INTO tax_group_rules (group_id, rule_id, sequence_order)
SELECT
    tg.id,
    tr.id,
    1
FROM tax_rules tr
JOIN tax_groups tg
    ON tg.name = '_auto_' || tr.name
WHERE tr.name IN (
    'Standard Percentage Inclusive 7%',
    'Standard Percentage Exclusive 7%',
    'Fixed Amount Inclusive $1.00',
    'Fixed Amount Exclusive $1.00',
    'Tiered Rate Inclusive',
    'Tiered Rate Exclusive',
    'Per Unit Inclusive $0.20',
    'Per Unit Exclusive $0.20',
    'Percentage on Margin Inclusive 10%',
    'Percentage on Margin Exclusive 10%',
    'Reverse Charge Inclusive',
    'Reverse Charge Exclusive'
)
AND NOT EXISTS (
    SELECT 1
    FROM tax_group_rules tgr
    WHERE tgr.group_id = tg.id
      AND tgr.rule_id = tr.id
);

-- Tiered rules need at least one bracket row to be meaningful later.
INSERT INTO tax_rule_tiers (tax_rule_id, bracket_min_cents, bracket_max_cents, tier_rate_value)
SELECT tr.id, 0, 10000, 5.0
FROM tax_rules tr
WHERE tr.name IN ('Tiered Rate Inclusive', 'Tiered Rate Exclusive')
AND NOT EXISTS (
    SELECT 1
    FROM tax_rule_tiers t
    WHERE t.tax_rule_id = tr.id
);

COMMIT;
