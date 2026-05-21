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
    '__LICENSE_KEY_HASH__',
    '__LICENSE_KEY_PREFIX__',
    'RETAILSTOREPOS',
    'PREMIUM',
    'active',
    1,
    'manual',
    'Replace placeholders before executing.'
);
