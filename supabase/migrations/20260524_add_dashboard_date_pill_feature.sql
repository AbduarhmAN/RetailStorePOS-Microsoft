insert into public.permission_group_features (
    product_code,
    permission_group,
    feature_code,
    enabled
)
values
    ('RETAILSTOREPOS', 'PREMIUM', 'DashboardDatePill', true)
on conflict (product_code, permission_group, feature_code) do update
set enabled = excluded.enabled;
