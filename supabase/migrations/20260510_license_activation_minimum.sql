create extension if not exists pgcrypto;

create table if not exists public.licenses (
    id uuid primary key default gen_random_uuid(),

    license_key_hash text not null unique,
    license_key_prefix text not null,

    product_code text not null,
    permission_group text not null,
    status text not null default 'active',

    max_devices int not null default 1,

    starts_at timestamptz not null default timezone('utc'::text, now()),
    expires_at timestamptz,

    source text not null default 'manual',
    external_reference text,
    notes text,

    created_at timestamptz not null default timezone('utc'::text, now()),
    updated_at timestamptz not null default timezone('utc'::text, now())
);

create unique index if not exists ux_licenses_license_key_hash
on public.licenses(license_key_hash);

create index if not exists idx_licenses_product_status
on public.licenses(product_code, status);

create table if not exists public.permission_groups (
    id uuid primary key default gen_random_uuid(),

    product_code text not null,
    permission_group text not null,
    display_name text not null,
    status text not null default 'active',

    created_at timestamptz not null default timezone('utc'::text, now()),
    updated_at timestamptz not null default timezone('utc'::text, now()),

    unique(product_code, permission_group)
);

create table if not exists public.permission_group_features (
    id uuid primary key default gen_random_uuid(),

    product_code text not null,
    permission_group text not null,
    feature_code text not null,
    enabled boolean not null default true,

    created_at timestamptz not null default timezone('utc'::text, now()),

    unique(product_code, permission_group, feature_code)
);

create index if not exists idx_permission_group_features_lookup
on public.permission_group_features(product_code, permission_group)
where enabled = true;

create table if not exists public.license_activations (
    id uuid primary key default gen_random_uuid(),

    license_id uuid not null references public.licenses(id) on delete cascade,
    install_id text not null references public.installations(install_id) on delete cascade,

    status text not null default 'active',

    device_public_key_thumbprint text,
    last_request_sequence bigint not null default 0,

    activated_at timestamptz not null default timezone('utc'::text, now()),
    last_refreshed_at timestamptz,
    revoked_at timestamptz,
    revoke_reason text,

    created_at timestamptz not null default timezone('utc'::text, now()),
    updated_at timestamptz not null default timezone('utc'::text, now()),

    unique(license_id, install_id)
);

create index if not exists idx_license_activations_license
on public.license_activations(license_id);

create index if not exists idx_license_activations_install
on public.license_activations(install_id);

create index if not exists idx_license_activations_status
on public.license_activations(status);

alter table public.licenses enable row level security;
alter table public.permission_groups enable row level security;
alter table public.permission_group_features enable row level security;
alter table public.license_activations enable row level security;

insert into public.permission_groups (
    product_code,
    permission_group,
    display_name,
    status
)
values
    ('RETAILSTOREPOS', 'STANDARD', 'Standard', 'active'),
    ('RETAILSTOREPOS', 'PREMIUM', 'Premium', 'active')
on conflict (product_code, permission_group) do update
set
    display_name = excluded.display_name,
    status = excluded.status,
    updated_at = timezone('utc'::text, now());

insert into public.permission_group_features (
    product_code,
    permission_group,
    feature_code,
    enabled
)
values
    ('RETAILSTOREPOS', 'STANDARD', 'BasicPOS', true),
    ('RETAILSTOREPOS', 'STANDARD', 'Products', true),
    ('RETAILSTOREPOS', 'STANDARD', 'Sales', true),
    ('RETAILSTOREPOS', 'STANDARD', 'BasicReports', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'BasicPOS', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'Products', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'Sales', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'BasicReports', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'AdvancedReports', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'InventoryAnalytics', true),
    ('RETAILSTOREPOS', 'PREMIUM', 'ExportReports', true)
on conflict (product_code, permission_group, feature_code) do update
set enabled = excluded.enabled;
