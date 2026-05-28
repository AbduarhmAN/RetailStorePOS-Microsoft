-- Manual Premium license activation template for legacy bootstrap:
-- The placeholder {{HMAC_INSTALL_ID}} represents the HMAC(install_id, LICENSE_KEY_PEPPER)
-- which is used as licenses.license_key_hash for this legacy install.

insert into public.permission_groups (
    product_code,
    permission_group,
    display_name,
    status
)
select
    'RETAILSTOREPOS',
    'PREMIUM',
    'Premium',
    'active'
where not exists (
    select 1
    from public.permission_groups
    where product_code = 'RETAILSTOREPOS'
      and permission_group = 'PREMIUM'
);

update public.permission_groups
set
    display_name = 'Premium',
    status = 'active',
    updated_at = timezone('utc'::text, now())
where product_code = 'RETAILSTOREPOS'
  and permission_group = 'PREMIUM';

insert into public.permission_group_features (
    product_code,
    permission_group,
    feature_code,
    enabled
)
select
    'RETAILSTOREPOS',
    'PREMIUM',
    feature_code,
    true
from (
    values
        ('BasicPOS'),
        ('Products'),
        ('Sales'),
        ('BasicReports'),
        ('AdvancedReports'),
        ('DashboardDatePill'),
        ('InventoryAnalytics'),
        ('ExportReports')
) as features(feature_code)
where not exists (
    select 1
    from public.permission_group_features existing
    where existing.product_code = 'RETAILSTOREPOS'
      and existing.permission_group = 'PREMIUM'
      and existing.feature_code = features.feature_code
);

update public.permission_group_features
set enabled = true
where product_code = 'RETAILSTOREPOS'
  and permission_group = 'PREMIUM'
  and feature_code in (
      'BasicPOS',
      'Products',
      'Sales',
      'BasicReports',
      'AdvancedReports',
      'DashboardDatePill',
      'InventoryAnalytics',
      'ExportReports'
  );

insert into public.licenses (
    license_key_hash,
    license_key_prefix,
    product_code,
    permission_group,
    status,
    max_devices,
    source,
    notes
)
values (
    '{{HMAC_INSTALL_ID}}',
    'E65E5823',
    'RETAILSTOREPOS',
    'PREMIUM',
    'active',
    1,
    'manual',
    'Manual Premium activation seed template for legacy bootstrap.'
)
on conflict (license_key_hash) do update
set
    product_code = excluded.product_code,
    permission_group = excluded.permission_group,
    status = 'active',
    max_devices = excluded.max_devices,
    source = excluded.source,
    notes = excluded.notes,
    updated_at = timezone('utc'::text, now());
