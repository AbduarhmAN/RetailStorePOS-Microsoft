# Final License Certification Architecture Review

## 1. Direct Answer

Yes, the approach is mostly right, but the final version should be slightly corrected:

```text
Use public.installations as the device / installation anchor.
Do not create another database.
Do not create a duplicate devices table unless install_id is not stable enough.
Add only the minimum license tables.
Use Supabase Edge Functions as the only public activation endpoint.
Use signed LicenseActivationCertificate locally inside the app.
Do not call Supabase on every feature click.
```

The main missing risks are:

```text
1. Do not store raw license keys. Store license_key_hash + license_key_prefix.
2. Do not put backend private keys, service_role keys, or signing keys in the app.
3. Do not use feature_flags as the license source of truth.
4. Do not put a SECURITY DEFINER function in public unless you understand the exposure risk.
5. Do not rely only on app_version text for mandatory updates.
6. Do not make every app startup call Supabase if a signed local certificate is still valid.
7. Do not treat client-side verification as impossible to bypass.
```

Your app already has a local-first shape, SQLite, WinUI desktop runtime, DPAPI package, and an existing installation/telemetry model, so the best design is **online activation + local signed certificate enforcement**. Your own project notes also say the online path should not sit on the critical POS path, and signed local verification should be cached and used at runtime.

I cannot create separate editable canvases in this chat, so below are the three final canvas-style specs.

---

## 2. Reasoning

# Canvas 1: Final End-to-End License Process

## 1.1 Goal

This system creates a **license activation certificate**, not only a device certificate.

```text
Device identity answers:
“Which installation/device is this?”

License answers:
“What is this installation allowed to use?”

Activation answers:
“Has this app accepted a valid license certificate locally?”
```

Final concept:

```text
install_id + license_key + device key proof
→ Supabase validates license
→ Supabase returns signed LicenseActivationCertificate
→ app verifies certificate locally
→ app activates allowed permission group/features
```

This matches the offline licensing model in your notes: a signed payload should contain product identity, hardware/installation binding, issue/expiry dates, and enabled features, and the client should validate signature, hardware match, product match, expiry, and feature permissions.

---

## 1.2 Actors

```text
RetailStorePOS app
    WinUI desktop client.

Supabase Edge Function
    Public online activation endpoint.

Supabase Postgres
    Existing database with installations/app_versions/events/logs plus new license tables.

Backend signing key
    Private key stored only in Supabase secrets or external KMS.

Backend public signing key
    Embedded in the app to verify activation certificates.

Device key pair
    Created by the app.
    Private key stays local.
    Public key thumbprint is bound to activation.
```

Supabase Edge Functions are server-side TypeScript functions. Supabase’s docs say Edge Functions can centralize security checks, should be designed as short-lived/idempotent operations, and can use secrets from environment variables.

---

## 1.3 Existing tables you already have

From the schema you pasted:

```text
public.installations
public.installation_events
public.error_logs
public.app_versions
```

Use them like this:

| Existing table | Use in license system |
|---|---|
| `public.installations` | Device/installation anchor. Use `install_id` as the installation identity. |
| `public.installation_events` | Optional important license events, not every normal success. |
| `public.error_logs` | License activation failures, crypto failures, save failures. |
| `public.app_versions` | Update policy and mandatory update prompts. |

Do **not** create another database. If you add tables, add them to the same Supabase Postgres database.

---

## 1.4 New minimal license tables

You need these license-specific tables:

```text
public.licenses
public.permission_groups
public.permission_group_features
public.license_activations
```

You do **not** need a duplicate device table if `public.installations.install_id` is stable.

Historical draft assumption:

```text
install_id was treated as UUID-shaped in the activation path.
```

Current implemented variant:

```text
install_id is now treated as text on the backend, not uuid.
The client promotes a legacy saved install_id to a hardware-derived install_id once.
During rollout, the backend must resolve old IDs to the new canonical install_id and merge duplicate installation rows.
After all active clients have migrated, the temporary canonicalization layer can be retired.
```

If your stable identifier is **not** the same as `install_id`, then add this to `public.installations`:

```sql
alter table public.installations
add column if not exists device_identifier_hash text;

create index if not exists idx_installations_device_identifier_hash
on public.installations(device_identifier_hash);
```

That is still better than creating a duplicate device identity table.

---

## 1.5 Full activation conversation

```text
APP:
I already have install_id.
I create/load my device key pair.
The user enters license_key.
I create requestNonce.
I increment requestSequence.
I build activation payload.
I sign it with devicePrivateKey.
I encrypt it for the backend.
I send it to Supabase Edge Function.

BACKEND:
I decrypt the request.
I validate structure and protocolVersion.
I verify the device signature.
I verify timestamp and request sequence.
I look up install_id in public.installations.
I look up license_key_hash in public.licenses.
I check license status, expiry, and permission group.
I check license max_devices.
I create or refresh license_activations row.
I load allowed features.
I create LicenseActivationCertificate.
I sign it with backendPrivateSigningKey.
I return payload + signature.

APP:
I verify backend signature using embedded backendPublicSigningKey.
I check installId matches local install_id.
I check nonce and sequence.
I check licenseStatus and activationStatus.
I check expiry.
I save certificate locally.
I build local feature snapshot.
I mark app as Activated.
```

---

## 1.6 Request payload

The app sends an encrypted envelope. Inside the encrypted body:

```json
{
  "payload": {
    "messageType": "license_activation_request",
    "protocolVersion": 1,
    "productCode": "RETAILSTOREPOS",
    "licenseKey": "USER-ENTERED-LICENSE-KEY",
    "installId": "INSTALL_ID",
    "devicePublicKeyThumbprint": "SHA256_OF_DEVICE_PUBLIC_KEY",
    "requestNonce": "RANDOM_32_BYTE_NONCE",
    "requestSequence": 1,
    "requestTimeUtc": "UTC_TIME"
  },
  "deviceSignature": "BASE64_DEVICE_SIGNATURE",
  "deviceSignatureAlgorithm": "ECDSA-P256-SHA256"
}
```

Status note:

```text
Earlier draft examples implied a UUID-shaped installId.
Implemented client behavior now sends a hardware-derived text install_id after one-time local migration.
Backend validation therefore must accept the canonical text install_id shape used by the client, not UUID-only input.
```

If you want better database safety, the Edge Function should hash the license key before lookup:

```text
license_key_hash = SHA256(normalized_license_key + server_pepper)
```

Then the database stores only:

```text
license_key_hash
license_key_prefix
```

not the full license key.

---

## 1.7 Backend response certificate

```json
{
  "payload": {
    "messageType": "license_activation_certificate",
    "certificateVersion": 1,
    "activationCertificateId": "UUID",
    "licenseId": "UUID",
    "activationId": "UUID",
    "installId": "INSTALL_ID",
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
  },
  "backendSignature": "BASE64_BACKEND_SIGNATURE",
  "backendSignatureAlgorithm": "ECDSA-P256-SHA256",
  "keyId": "backend-signing-key-1"
}
```

The certificate is trusted because it is **signed**, not because it is hidden.

Your project notes correctly warn that public examples often confuse encryption with digital signatures; encryption provides secrecy, but signatures provide authenticity and tamper detection.

---

## 1.8 Runtime behavior

Do not call Supabase every time the user opens a feature.

```text
Startup:
    Load local activation certificate.
    Verify backend signature.
    Verify installId.
    Verify expiry.
    Build in-memory permission snapshot.

Feature usage:
    FeatureAccessService.CanUse("AdvancedReports")
```

This follows the lightweight verification architecture: validate signed artifacts when loaded/refreshed, flatten into a runtime snapshot, then perform fast local checks.

---

## 1.9 Failure matrix

| Situation | Backend result | App result |
|---|---|---|
| Missing install_id | `installation_not_found` | Cannot activate |
| Missing license key | `missing_license_key` | Ask user to enter key |
| License not found | `license_not_found` | Show invalid license |
| License revoked | `license_revoked` | Do not activate |
| License suspended | `license_suspended` | Do not activate |
| License expired | `license_expired` | Ask renewal |
| License not started | `license_not_started` | Show start date |
| Device limit reached | `device_limit_reached` | Ask support/reset |
| App version blocked | `mandatory_update_required` | Show update required |
| Request sequence replay | `sequence_replay_or_rollback` | Rebuild request state |
| Device public key changed | `recovery_required` | Require support or reset flow |
| Backend signature invalid | N/A | Reject certificate |
| Local certificate expired | N/A | Refresh or re-activate |
| Supabase offline | N/A | Use existing valid local certificate |

---

# Canvas 2: Application Implementation Canvas

## 2.1 Add app services

Add a dedicated app-side licensing folder:

```text
Nexill.RetailStorePOS/
  Services/
    Licensing/
      InstallationIdentityProvider.cs
      DeviceKeyService.cs
      LicenseActivationClient.cs
      LicenseCertificateValidator.cs
      LicenseCertificateStore.cs
      FeatureAccessService.cs
      LicenseRefreshService.cs
      LicenseHttpLoggingHandler.cs
```

The repo notes say `LoginRuntime` is a natural central initialization point, and `MainWindow_Loaded` already moves heavy startup work away from the UI thread. That is exactly where licensing should warm local state and schedule refresh without blocking the splash/UI path.

---

## 2.2 Service responsibilities

| Service | Responsibility |
|---|---|
| `InstallationIdentityProvider` | Returns existing `install_id`; optionally device identifier hash if needed. |
| `DeviceKeyService` | Creates/loads local device key pair; protects private key. |
| `LicenseActivationClient` | Sends activation request to Supabase Edge Function. |
| `LicenseCertificateValidator` | Verifies backend signature and certificate fields. |
| `LicenseCertificateStore` | Stores local signed activation certificate atomically. |
| `FeatureAccessService` | Runtime `CanUse(featureCode)` checks. |
| `LicenseRefreshService` | Refreshes near-expiry certificate in background. |
| `LicenseHttpLoggingHandler` | Optional debug tracing for outgoing activation calls. |

Your existing notes say WinUI 3 does not have a built-in browser-style network tab, so the practical debugging approach is centralized `HttpClient` plus a `DelegatingHandler` that writes request/response diagnostics to Visual Studio Output or local logs.

---

## 2.3 App startup algorithm

```text
function InitializeLicensing():
    localCert = LicenseCertificateStore.Load()

    if localCert exists:
        if LicenseCertificateValidator.IsValid(localCert):
            FeatureAccessService.BuildSnapshot(localCert)
            ActivationState = Activated

            if localCert.ExpiresSoon:
                LicenseRefreshService.StartBackgroundRefresh()

            return

    ActivationState = NotActivated
```

Do not call Supabase if a valid local certificate exists and is not near expiry.

This is important for cost, speed, and offline use.

---

## 2.4 Online activation algorithm

```text
function ActivateLicense(licenseKey):
    installId = InstallationIdentityProvider.GetInstallId()

    if installId missing:
        return installation_missing

    deviceKey = DeviceKeyService.LoadOrCreate()

    nonce = SecureRandom(32 bytes)
    sequence = LicenseCertificateStore.NextRequestSequence()

    payload = {
        messageType: "license_activation_request",
        protocolVersion: 1,
        productCode: "RETAILSTOREPOS",
        licenseKey: licenseKey,
        installId: installId,
        devicePublicKeyThumbprint: deviceKey.PublicKeyThumbprint,
        requestNonce: nonce,
        requestSequence: sequence,
        requestTimeUtc: NowUtc()
    }

    signature = Sign(payload, deviceKey.PrivateKey)

    encryptedEnvelope = EncryptForBackend(payload + signature)

    response = LicenseActivationClient.Post(encryptedEnvelope)

    certificate = Parse(response)

    if not LicenseCertificateValidator.Validate(
        certificate,
        expectedInstallId = installId,
        expectedNonce = nonce,
        expectedSequence = sequence
    ):
        return activation_failed

    LicenseCertificateStore.SaveAtomically(certificate)

    FeatureAccessService.BuildSnapshot(certificate)

    return activated
```

---

## 2.5 Local certificate validation

```text
function ValidateCertificate(cert):
    if backendSignature invalid:
        return false

    if cert.messageType != "license_activation_certificate":
        return false

    if cert.productCode != "RETAILSTOREPOS":
        return false

    if cert.installId != localInstallId:
        return false

    if cert.licenseStatus not in ["active", "trial"]:
        return false

    if cert.activationStatus != "active":
        return false

    if cert.expiresAtUtc < NowUtc():
        return false

    return true
```

---

## 2.6 Runtime feature access

```text
function CanUse(featureCode):
    snapshot = CurrentLicenseSnapshot

    if snapshot missing:
        return false

    if snapshot.expired:
        return false

    if featureCode not in snapshot.features:
        return false

    return true
```

Attach this at real business boundaries, not only UI buttons.

Useful hooks from your repo notes include navigation, checkout commit, register close/cash operations, product import, and destructive reset flows.

---

## 2.7 Local storage

Store:

```text
license_activation_certificate.json
last_request_sequence
license_state
device private key
```

Protect sensitive local data with DPAPI / `ProtectedData` if not using TPM/CNG yet. Microsoft describes `ProtectedData` as providing methods for encrypting and decrypting data, and your app already includes the `System.Security.Cryptography.ProtectedData` package according to the retrieved repo notes.

---

## 2.8 App-side severe risks

| Risk | Why it matters | Fix |
|---|---|---|
| Backend private key in app | Catastrophic, attackers can generate certificates | Keep signing private key only in Supabase secrets/KMS |
| Supabase service role in app | Bypasses RLS and exposes admin access | Edge Function only |
| UI-only license checks | Easy to bypass | Enforce at service/workflow boundaries |
| Calling Supabase every feature click | Slow/costly/offline-hostile | Local signed certificate snapshot |
| Local certificate overwritten by bad response | Can break valid users | Replace only after full validation |
| Feature flags as entitlement truth | Not strong enough for licensing | Use signed certificate features |
| App version string comparison | `2.10.0` vs `2.9.0` can sort wrong as text | Add numeric build/version_code later |

Offline/local client checks can raise attacker cost, but they cannot make a customer-controlled desktop app unpatchable. Your notes explicitly say the attacker may control the machine, clock, debugger, memory, executable, and local state, so the goal is layered resistance, not mathematical impossibility.

---

# Canvas 3: Supabase Database and Backend Canvas

## 3.1 Verdict on your existing structure

Your existing schema is a good foundation.

Use:

```text
public.installations.install_id
```

as the license activation device/installation anchor.

That is better than creating:

```text
device_certification.devices
```

because you already have installation tracking, app version, build number, platform metadata, last seen, crash count, and update fields.

---

## 3.2 Required new tables

Use the same database and likely the same `public` schema:

```text
public.licenses
public.permission_groups
public.permission_group_features
public.license_activations
```

If you can create a non-exposed schema for internal SQL functions, use:

```text
internal.activate_license_installation()
```

or similar.

Important Supabase warning: RLS should always be enabled on tables in exposed schemas such as `public`, and service keys bypass RLS and must never be exposed to customers.

---

## 3.3 Improved license tables

Use hashed license keys.

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

    customer_reference text,
    notes text,

    created_at timestamptz not null default timezone('utc'::text, now()),
    updated_at timestamptz not null default timezone('utc'::text, now())
);

create unique index if not exists ux_licenses_license_key_hash
on public.licenses(license_key_hash);

create index if not exists idx_licenses_product_status
on public.licenses(product_code, status);
```

Why hash the license key?

```text
If the database leaks, raw license keys are not immediately exposed.
The user can still type the original license key.
The backend hashes it and compares the hash.
```

---

## 3.4 Permission groups

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
```

Postgres indexes help avoid scanning an entire large table to find rows; indexes act like a table of contents for locating rows faster, while also adding write/storage overhead. Use them on lookup paths, not everywhere.

---

## 3.5 Activation table

Historical draft assumption:

```sql
-- Earlier draft shape:
-- install_id uuid not null references public.installations(install_id) on delete cascade
```

Implemented backend shape:

```sql
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
```

During a live migration from legacy GUID-based install IDs to hardware-derived install IDs, keep a canonicalization layer such as `install_id_aliases` or equivalent migration metadata. That layer exists to make old and new IDs resolve to one installation while mixed client versions are still in the field. Do not remove it until old IDs have stopped arriving for a defined window.

---

## 3.6 RLS

Enable RLS:

```sql
alter table public.licenses enable row level security;
alter table public.permission_groups enable row level security;
alter table public.permission_group_features enable row level security;
alter table public.license_activations enable row level security;
```

First version:

```text
No direct client SELECT/INSERT/UPDATE policies.
Desktop app calls Edge Function.
Edge Function uses service role internally.
```

Supabase says Edge Functions have access to `SUPABASE_SERVICE_ROLE_KEY`, and that this key is safe in Edge Functions but must never be used in browser/client code because it bypasses RLS.

---

## 3.7 SQL transaction function placement

Do **not** put a `SECURITY DEFINER` function casually in `public` if your public schema is exposed through the API.

Better:

```text
Option A, safest:
    Put transaction function in a non-exposed schema, for example internal.activate_license_installation.
    Call it from Edge Function using a server-side DB connection.

Option B, acceptable for early version:
    Keep logic in Edge Function with service role and multiple queries.
    Simpler, slightly more round trips.

Option C, only if you understand the exposure:
    public RPC with tightly restricted execute permissions.
```

Security-definer functions should not be created casually in schemas exposed through API settings.

So the earlier public RPC idea should be adjusted to an `internal` schema if you can create schemas:

```sql
create schema if not exists internal;
```

This is not a new database. It is only a schema namespace in the same database.

---

## 3.8 Backend Edge Function responsibilities

`license-activate` Edge Function should handle:

```text
POST only
parse encrypted envelope
decrypt request
validate message
verify device signature
hash license key
call database logic
build certificate
sign certificate
return response
```

Supabase Edge Functions have CPU/runtime limits, including a documented 2-second CPU-time limit per request and 256 MB maximum memory on hosted Edge Functions, so keep the function short and avoid heavy logging or multi-step scans.

---

## 3.9 Backend pseudo-code

```text
function licenseActivate(envelope):
    reject if method != POST

    signedRequest = decryptEnvelope(envelope)

    payload = signedRequest.payload

    validate protocolVersion
    validate productCode
    validate installId
    validate requestTimeUtc
    validate requestNonce
    validate requestSequence
    verify deviceSignature

    licenseKeyHash = SHA256(normalize(payload.licenseKey) + LICENSE_KEY_PEPPER)

    result = activateLicenseInstallation(
        licenseKeyHash,
        productCode,
        installId,
        devicePublicKeyThumbprint,
        requestSequence
    )

    if result.result_code != "activated":
        return error result

    certPayload = {
        messageType: "license_activation_certificate",
        certificateVersion: 1,
        activationCertificateId: uuid,
        licenseId: result.license_id,
        activationId: result.activation_id,
        installId: result.install_id,
        productCode: productCode,
        permissionGroup: result.permission_group,
        features: result.features,
        licenseStatus: result.license_status,
        activationStatus: result.activation_status,
        issuedAtUtc: serverNow,
        expiresAtUtc: min(result.license_expires_at, serverNow + certValidityDays),
        requestNonce: payload.requestNonce,
        requestSequence: payload.requestSequence
    }

    signature = Sign(certPayload, BACKEND_SIGNING_PRIVATE_KEY)

    return certPayload + signature + keyId
```

---

## 3.10 Database decision logic

The DB logic must check:

```text
installation exists
license exists
license product matches
license active/trial
license not expired
permission group active
activation exists or can be created
max_devices not exceeded
device_public_key_thumbprint unchanged
requestSequence > last_request_sequence
features loaded
```

If any fails, return a precise code:

```text
installation_not_found
license_not_found
license_revoked
license_suspended
license_expired
permission_group_missing
device_limit_reached
device_key_changed_requires_recovery
sequence_replay_or_rollback
activated
```

---

## 3.11 `app_versions` correction

Your existing `app_versions.version_string` is text. That is useful for display, but risky for ordering.

Example:

```text
"2.10.0" can sort before "2.9.0" as plain text.
```

So use `app_versions` carefully.

Current safe use:

```text
If exact app_version row says is_mandatory or inactive, return update_required.
```

Better future improvement:

```sql
alter table public.app_versions
add column if not exists version_code integer;
```

Then use numeric comparison for update gating.

Until then, do not build critical security decisions around text version ordering.

---

## 3.12 What not to store

Do not store these in Supabase license tables:

```text
raw private keys
raw device private key
backend private signing key
backend private decryption key
Supabase service role key
raw hardware identifiers unless legally/operationally required
every successful feature check
every successful startup check
```

Use `installation_events` only for important security/business events, not for hot-path runtime checks.

---

## 3.13 Severe harms if missed

| Missing item | Harm |
|---|---|
| RLS not enabled on public license tables | Client/API exposure risk |
| Service role key in app | Full backend bypass risk |
| Backend signing key in app | Attackers can forge certificates |
| Raw license keys in DB | DB leak exposes usable licenses |
| No max device enforcement | Unlimited device sharing |
| No request sequence | Replay/rollback easier |
| No backend signature validation | Fake activation certificates possible |
| App calls backend every feature | Cost, latency, offline failure |
| UI-only feature checks | Easy bypass |
| No local certificate expiry | Old permissions live forever |
| App version text ordering | Mandatory update logic can be wrong |
| Public SECURITY DEFINER RPC | Accidental privilege exposure |

---

## 3.14 Final recommended build order

```text
1. Decide the canonical install_id source. Current direction: hardware-derived install_id stored locally after first migration.
2. If legacy install IDs already exist, deploy backend canonicalization before client cutover.
3. Add public.licenses.
4. Add public.permission_groups.
5. Add public.permission_group_features.
6. Add public.license_activations.
7. Enable RLS on new tables.
8. Store backend private keys in Supabase secrets.
9. Create license-activate Edge Function.
10. Add app-side local certificate validator/store.
11. Add FeatureAccessService.
12. Add Settings → License activation UI.
13. Add background refresh near certificate expiry.
14. Retire the canonicalization layer only after mixed-version traffic has ended.
```

Implemented status note:

```text
This was not completed by keeping UUID in the backend path.
It was completed by switching install_id to text, migrating the client to a hardware-derived canonical ID, and temporarily canonicalizing old IDs to the new one during rollout.
```

---

## 3. Uncertainty or Limits

The current live direction is to make the hardware-derived value the canonical `install_id` and persist it as text in Supabase. That removes the earlier UUID constraint, but it does introduce a rollout requirement: old GUID-based IDs and new hardware-based IDs must be reconciled while mixed client versions still exist.

I also cannot verify from the schema alone whether your existing Supabase RLS policies are safe. The new license tables should be private-by-default behind Edge Functions, and any temporary alias/canonicalization table should stay locked down and be removed only after the migration window is complete.

Final direction:

```text
Keep your existing database.
Reuse public.installations as the installation/device anchor.
Add compact license tables.
Use Edge Function for activation.
Return signed LicenseActivationCertificate.
Validate locally inside the app.
Use local feature snapshots.
Avoid per-click or per-startup backend calls.
```

That is the cleanest, lowest-cost, and safest version for your current architecture.
