# Legacy Draft Notice

This file is a legacy working draft. It is **not** the authoritative install-identity design anymore.

Old assumption:

```text
install_id was treated as UUID-oriented in the activation path.
```

New implemented direction:

```text
install_id is hardware-derived on the client and stored as text on the backend.
legacy saved install IDs are migrated once on the client.
the backend temporarily canonicalizes old and new IDs during rollout.
```

Done status:

```text
The implemented system did not complete the old UUID-only path exactly as written here.
It completed a revised path: text install_id in backend, one-time client migration, and temporary backend canonicalization during rollout.
```

Use [device_certification_protocol_and_versioning_spec.md](E:\Projects\Retail_Store\V\2.0.0\documents\device_certification_protocol_and_versioning_spec.md) as the current reference for install identity and license activation behavior.

# Verification-Only License Architecture

## 1. Purpose

This document defines the **verification-only license system** for the application.

This phase does **not** include:

```text
payment providers
Stripe
Lemon Squeezy
PayPal
Gumroad
checkout sessions
provider webhooks
subscription billing
customer portals
```

This phase focuses only on:

```text
license key verification
installation/device binding
permission group validation
signed activation certificate
local certificate validation
local feature access
future-safe design for payment providers later
```

The goal is to build a licensing foundation that works now and does not block future payment-provider integration.

---

## 2. Final Direction

Build this first:

```text
User enters license key
→ app sends signed verification request to backend
→ backend checks license record
→ backend checks installation/device identity
→ backend checks permission group/features
→ backend returns signed LicenseActivationCertificate
→ app verifies certificate locally
→ app unlocks allowed features from local snapshot
```

Do **not** build payment checkout yet.

Do **not** connect provider APIs yet.

Do **not** let payment provider design leak into the app.

The app should only understand this stable concept:

```text
I have a signed license activation certificate from my backend.
```

Later, payment providers can create or update license records on the backend, but the app-side verification flow can stay the same.

---

## 3. What This Phase Does

### It verifies

```text
Does this license key exist?
Is it active?
Is it valid for this product?
Is it expired?
Is it allowed for this installation?
Is the device count limit respected?
Which permission group is allowed?
Which features are included?
```

### It returns

```text
A signed LicenseActivationCertificate.
```

### The app stores

```text
license_activation_certificate.json
```

### The app uses

```text
FeatureAccessService.CanUse(featureCode)
```

---

## 4. What This Phase Does Not Do

This phase does not answer:

```text
Did the customer pay through Stripe?
Did PayPal confirm payment?
Did Lemon Squeezy renew the subscription?
Did Gumroad issue a license?
```

Those questions belong to a future payment-provider layer.

For now, license records can be created manually in Supabase by the developer/admin.

---

## 5. Existing Supabase Foundation

You already have:

```text
public.installations
public.installation_events
public.error_logs
public.app_versions
```

Use them like this:

| Existing table | Role in verification-only licensing |
|---|---|
| `public.installations` | Installation/device anchor. Use `install_id`. |
| `public.installation_events` | Optional important license events only. |
| `public.error_logs` | Activation/verification errors. |
| `public.app_versions` | Mandatory update and compatibility checks. |

Do not create a new database.

Do not create a duplicate device table unless `install_id` is not stable enough.

---

## 6. New Tables for Verification-Only Phase

Minimum required tables:

```text
public.licenses
public.permission_groups
public.permission_group_features
public.license_activations
```

Do not create these yet:

```text
public.customers
public.subscriptions
public.payment_providers
public.checkout_sessions
public.payment_events
```

Those belong to the future payment-provider phase.

---

## 7. Table: public.licenses

Stores manually created license records.

Use hashed license keys instead of raw license keys.

```sql
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
```

Allowed `status` values:

```text
active
trial
suspended
revoked
expired
```

Recommended indexes:

```sql
create unique index if not exists ux_licenses_license_key_hash
on public.licenses(license_key_hash);

create index if not exists idx_licenses_product_status
on public.licenses(product_code, status);
```

Why include `source` and `external_reference` now?

```text
source = manual today
source = stripe / lemon / paypal later
external_reference = future provider object ID
```

This keeps the table future-safe without implementing payments now.

---

## 8. Table: public.permission_groups

Stores license editions or permission groups.

```sql
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
```

Example groups:

```text
TRIAL
STANDARD
PREMIUM
ENTERPRISE
```

---

## 9. Table: public.permission_group_features

Stores which features each permission group allows.

```sql
create table if not exists public.permission_group_features (
    id uuid primary key default gen_random_uuid(),

    product_code text not null,
    permission_group text not null,
    feature_code text not null,
    enabled boolean not null default true,

    created_at timestamptz not null default timezone('utc'::text, now()),

    unique(product_code, permission_group, feature_code)
);
```

Recommended index:

```sql
create index if not exists idx_permission_group_features_lookup
on public.permission_group_features(product_code, permission_group)
where enabled = true;
```

Example features:

```text
BasicPOS
Products
Sales
BasicReports
AdvancedReports
InventoryAnalytics
ExportReports
```

---

## 10. Table: public.license_activations

Stores the binding between a license and an installation.

```sql
create table if not exists public.license_activations (
    id uuid primary key default gen_random_uuid(),

    license_id uuid not null references public.licenses(id) on delete cascade,
    install_id uuid not null references public.installations(install_id) on delete cascade,

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
```

Recommended indexes:

```sql
create index if not exists idx_license_activations_license
on public.license_activations(license_id);

create index if not exists idx_license_activations_install
on public.license_activations(install_id);

create index if not exists idx_license_activations_status
on public.license_activations(status);
```

---

## 11. RLS Direction

Enable RLS:

```sql
alter table public.licenses enable row level security;
alter table public.permission_groups enable row level security;
alter table public.permission_group_features enable row level security;
alter table public.license_activations enable row level security;
```

First version policy:

```text
Desktop app must not query license tables directly.
Desktop app calls Supabase Edge Function only.
Edge Function uses service role internally.
```

So for version 1:

```text
No public SELECT policies.
No public INSERT policies.
No public UPDATE policies.
```

---

## 12. Supabase Secrets

Required secrets:

```text
BACKEND_SIGNING_PRIVATE_KEY
BACKEND_SIGNING_KEY_ID
LICENSE_KEY_PEPPER
MIN_PROTOCOL_VERSION
MAX_PROTOCOL_VERSION
PREFERRED_PROTOCOL_VERSION
LICENSE_CERTIFICATE_VALIDITY_DAYS
```

Optional if you use request encryption:

```text
BACKEND_DECRYPTION_PRIVATE_KEY
BACKEND_ENCRYPTION_KEY_ID
```

The app must never contain:

```text
BACKEND_SIGNING_PRIVATE_KEY
BACKEND_DECRYPTION_PRIVATE_KEY
SUPABASE_SERVICE_ROLE_KEY
LICENSE_KEY_PEPPER
```

The app may contain:

```text
BACKEND_PUBLIC_SIGNING_KEY
BACKEND_PUBLIC_ENCRYPTION_KEY, if request encryption is used
Supabase function URL
Supabase anon/publishable key, if needed to call the Edge Function
```

---

## 13. License Key Storage Rule

Do not store raw license keys.

When creating a license manually:

```text
raw license key shown once to admin/customer
backend stores license_key_hash
backend stores license_key_prefix for support display
```

Example:

```text
raw license key:
RSPS-PREM-83KD-91AF-77ZQ

license_key_prefix:
RSPS-PREM-83KD

license_key_hash:
SHA256(normalized_license_key + LICENSE_KEY_PEPPER)
```

When the app sends a license key:

```text
Edge Function normalizes it
Edge Function hashes it with LICENSE_KEY_PEPPER
Edge Function looks up public.licenses.license_key_hash
```

---

## 14. License Verification Request

The app sends a verification request to the backend.

Minimum inner payload:

```json
{
  "payload": {
    "messageType": "license_verification_request",
    "protocolVersion": 1,
    "productCode": "RETAILSTOREPOS",
    "licenseKey": "USER_ENTERED_LICENSE_KEY",
    "installId": "INSTALL_UUID",
    "devicePublicKeyThumbprint": "SHA256_OF_DEVICE_PUBLIC_KEY",
    "requestNonce": "RANDOM_32_BYTE_NONCE",
    "requestSequence": 1,
    "requestTimeUtc": "UTC_TIME"
  },
  "deviceSignature": "BASE64_DEVICE_SIGNATURE",
  "deviceSignatureAlgorithm": "ECDSA-P256-SHA256"
}
```

The function name can be:

```text
license-api
```

Action:

```json
{
  "action": "verify_license"
}
```

or a dedicated function:

```text
license-verify
```

Recommended for version 1:

```text
license-api with action = verify_license
```

This stays future-safe for later payment actions.

---

## 15. Backend Verification Algorithm

```text
function verifyLicense(request):
    reject if method != POST

    parse request

    if encrypted envelope is used:
        decrypt request

    validate messageType
    validate protocolVersion
    validate productCode
    validate installId
    validate licenseKey exists
    validate requestNonce
    validate requestSequence
    validate requestTimeUtc

    verify deviceSignature, if device key signing is enabled

    find installation by installId

    if installation not found:
        return installation_not_found

    licenseKeyHash = SHA256(normalize(licenseKey) + LICENSE_KEY_PEPPER)

    find license by licenseKeyHash + productCode

    if license not found:
        return license_not_found

    if license status is revoked:
        return license_revoked

    if license status is suspended:
        return license_suspended

    if license expired:
        return license_expired

    if license starts in future:
        return license_not_started

    find permission group

    if permission group missing or inactive:
        return permission_group_unavailable

    find existing activation by license_id + install_id

    if activation exists:
        validate activation status
        validate device_public_key_thumbprint if stored
        validate requestSequence > last_request_sequence
        update last_refreshed_at and last_request_sequence

    if activation does not exist:
        count active activations for license
        if count >= max_devices:
            return device_limit_reached
        create activation

    load enabled features for permission group

    build LicenseActivationCertificate

    sign certificate with BACKEND_SIGNING_PRIVATE_KEY

    return signed certificate
```

---

## 16. Backend Result Codes

Success:

```text
verified
activated
refreshed
```

Failure:

```text
installation_not_found
license_not_found
license_revoked
license_suspended
license_expired
license_not_started
permission_group_unavailable
device_limit_reached
activation_revoked
activation_suspended
device_key_changed_requires_recovery
sequence_replay_or_rollback
protocol_too_old
protocol_too_new
invalid_request_signature
```

---

## 17. LicenseActivationCertificate

Returned certificate payload:

```json
{
  "messageType": "license_activation_certificate",
  "certificateVersion": 1,
  "activationCertificateId": "UUID",
  "licenseId": "UUID",
  "activationId": "UUID",
  "installId": "UUID",
  "productCode": "RETAILSTOREPOS",
  "permissionGroup": "PREMIUM",
  "features": [
    "BasicPOS",
    "Products",
    "Sales",
    "BasicReports",
    "AdvancedReports",
    "InventoryAnalytics",
    "ExportReports"
  ],
  "licenseStatus": "active",
  "activationStatus": "active",
  "issuedAtUtc": "SERVER_TIME",
  "expiresAtUtc": "CERTIFICATE_EXPIRY",
  "requestNonce": "SAME_REQUEST_NONCE",
  "requestSequence": 1
}
```

Signed response:

```json
{
  "payload": {},
  "backendSignature": "BASE64_BACKEND_SIGNATURE",
  "backendSignatureAlgorithm": "ECDSA-P256-SHA256",
  "keyId": "backend-signing-key-1"
}
```

---

## 18. App Validation Algorithm

```text
function acceptLicenseActivationCertificate(response):
    parse response

    if backendSignature missing:
        reject

    if keyId unknown:
        reject

    verify backendSignature using BACKEND_PUBLIC_SIGNING_KEY

    if signature invalid:
        reject

    if messageType != license_activation_certificate:
        reject

    if productCode != RETAILSTOREPOS:
        reject

    if installId != local install_id:
        reject

    if requestNonce != pending requestNonce:
        reject

    if requestSequence != pending requestSequence:
        reject

    if licenseStatus not in active/trial:
        reject

    if activationStatus != active:
        reject

    if expiresAtUtc < now:
        reject

    save certificate atomically

    build local feature snapshot

    set ActivationState = Activated
```

---

## 19. App Runtime Permission Check

Do not call backend for every feature.

Use local certificate snapshot.

```text
FeatureAccessService.CanUse("AdvancedReports")
```

Algorithm:

```text
function CanUse(featureCode):
    snapshot = CurrentLicenseSnapshot

    if snapshot missing:
        return false

    if snapshot expired:
        return false

    if featureCode not in snapshot.features:
        return false

    return true
```

Add checks at service/workflow boundaries, not only UI buttons.

Examples:

```text
advanced reports generation
export reports
inventory analytics
admin-only destructive operations
premium-only modules
```

---

## 20. Refresh Strategy

Certificate validity should be shorter than license validity.

Example:

```text
license expires in 1 year
activation certificate expires in 30-90 days
refresh window starts 7 days before certificate expiry
```

Startup behavior:

```text
if local certificate exists and signature is valid:
    if not expired and not near expiry:
        activate locally
        do not call backend

    if not expired but near expiry:
        activate locally
        refresh in background

    if expired:
        require online refresh or re-verification

else:
    show Not Activated
```

This keeps the app fast and offline-friendly.

---

## 21. Future Payment Provider Integration

When payment providers are added later, do **not** change app verification behavior.

Payment provider layer should only create/update license records.

Future flow:

```text
Stripe/Lemon/PayPal confirms payment
→ webhook updates public.licenses or public.subscriptions
→ app still calls license-api verify_license / activate_or_refresh
→ backend still returns signed LicenseActivationCertificate
→ app still validates locally
```

The app should not know whether the license came from:

```text
manual license
Stripe payment
Lemon Squeezy payment
PayPal subscription
admin-issued trial
```

That information belongs to backend records only.

Future-safe fields already included:

```text
public.licenses.source
public.licenses.external_reference
```

Example later:

```text
source = stripe
external_reference = sub_123 or checkout_session_123
```

---

## 22. What Not to Build Now

Do not build now:

```text
checkout_sessions
payment_events
subscriptions
payment_providers
Stripe webhook
Lemon webhook
PayPal webhook
customer portal
Google sign-in
provider adapter layer
Realtime checkout waiting
deep links
billing dashboard
```

Build only:

```text
manual license records
license verification endpoint
signed activation certificate
local certificate validation
local feature permissions
```

---

## 23. Minimum Build Order

### Database

```text
1. Confirm public.installations.install_id is stable.
2. Create public.licenses.
3. Create public.permission_groups.
4. Create public.permission_group_features.
5. Create public.license_activations.
6. Enable RLS on new tables.
7. Insert STANDARD and PREMIUM permission groups.
8. Insert feature mappings.
9. Insert one test license manually.
```

### Backend

```text
1. Create license-api Edge Function.
2. Add verify_license action.
3. Add license key hashing.
4. Add license lookup.
5. Add installation lookup.
6. Add activation create/refresh logic.
7. Add certificate signing.
8. Return signed certificate.
```

### App

```text
1. Add LicenseActivationClient.
2. Add LicenseCertificateValidator.
3. Add LicenseCertificateStore.
4. Add FeatureAccessService.
5. Add Settings → License screen.
6. Add local startup validation.
7. Add feature gates.
8. Add background refresh near expiry.
```

### Testing

```text
1. Valid license activates.
2. Invalid license rejected.
3. Revoked license rejected.
4. Expired license rejected.
5. Device limit reached.
6. Certificate tampering rejected.
7. Offline startup works with valid certificate.
8. Feature denied if not in permission group.
```

---

## 24. Final Recommended Architecture for This Phase

Use this:

```text
Existing Supabase database
    public.installations
    public.app_versions
    public.installation_events
    public.error_logs

New verification tables
    public.licenses
    public.permission_groups
    public.permission_group_features
    public.license_activations

Backend
    license-api
        verify_license
        activate_or_refresh later

App
    signed local activation certificate
    local feature snapshot
    no backend call per feature
```

This gives you a complete verification-only license system now.

Later, payment providers can be added without changing the core app verification model.

---

## 25. Final Rule

```text
Payment provider creates or updates license truth later.
Verification system consumes license truth now.
Signed activation certificate carries permission truth to the app.
The app trusts only the signed certificate.
```

This is the cleanest direction for the current phase.
