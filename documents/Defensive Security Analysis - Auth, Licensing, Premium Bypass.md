# Defensive Security Analysis — RetailStorePOS

**Scope:** Authentication, authorization, licensing/premium-feature bypass, payment logic, local data integrity, Supabase backend posture, build/distribution integrity.
**Perspective:** Defensive (red-team-informed) review.
**Analysis date:** 2026-05-18
**Status:** Structural inventory only — no source files were read for this pass. Findings are split between **confirmed structural facts** and **possible vulnerability classes** that require code-level evidence to confirm.

---

## Field identified

A **WinUI 3 / .NET 9–10 Windows desktop POS application** (`Nexill.RetailStorePOS`) with:

- Local **SQLite** datastore (`pos.db`) and a `DatabaseEncryptionService.cs`.
- A **local user/auth** module: `Services/AuthService.cs`, `Repositories/UserRepository.cs`, `Models/User.cs`, `Models/Session.cs`, `Views/LoginPage.xaml.cs`, `LoginRuntime.cs`.
- A **license / premium-feature gating** path: separate `LicenseKeyGen` console project, `LicenseKeyGen/backend-signing-public.pem`, Supabase Edge Function `supabase/functions/license-api`, `supabase/public-keys/backend-signing-public.jwk.json`, manual test license seed.
- **Telemetry** going to Supabase (`installation_events`, `telemetry_outbox`).
- **Distribution** via MSIX (`Package.appxmanifest`, `build-msix.ps1`), Inno Setup single-file installer (`setup_singlefile*.iss`), and Microsoft Store path; signing with `Deployment/Certification/NexillTest.pfx`.
- DPAPI usage declared (`System.Security.Cryptography.ProtectedData`).

This is **not** a typical public web app, so several OWASP web categories apply differently. Risk centers on **client-side trust**, **license/premium bypass**, **local data integrity**, **backend (Supabase) authorization**, and **build/distribution integrity**.

---

## Security objective

Protect three primary assets, in this order:

1. **Revenue integrity** — premium/license entitlements cannot be unlocked without a valid, non-replayable, non-forgeable license, and sales/receipts cannot be altered to defraud merchants or your reporting.
2. **Data confidentiality and tamper-resistance** — local POS data (sales, taxes, users, audit log) cannot be silently modified by an attacker with file-system access.
3. **Backend isolation** — the Supabase backend (license-api, telemetry tables) cannot be abused by a forged client to issue licenses, drain data, or pollute analytics.

---

## Threat model

Because this is a desktop app, the most realistic adversaries are not anonymous internet attackers but:

- **T1 — Local user / cracker on the same Windows machine** running the app, with full file-system and process-memory access. (Highest realism; you cannot fully defeat this, only raise cost.)
- **T2 — Insider/employee** at a deployed store with a low-privilege POS user account trying to escalate to admin/manager (refund abuse, price override, void-after-cash).
- **T3 — Network attacker** between client and Supabase (TLS interception, replay).
- **T4 — Supabase abuse**: anonymous or stolen-key requests to the `license-api` function, abuse of anon/RLS policies, telemetry forgery.
- **T5 — Supply-chain / build attacker**: compromised MSIX/Inno installer, leaked code-signing PFX, malicious NuGet/npm dep.
- **T6 — Crack distributor**: posting patched binaries, key generators, or registry/SQLite snapshots that flip the premium flag.

OWASP/CWE references used below: OWASP Top 10 2021 (A01 Broken Access Control, A02 Crypto Failures, A04 Insecure Design, A05 Misconfig, A07 ID&Auth, A08 Integrity), OWASP MASVS-AUTH/MASVS-RESILIENCE (closest analog for offline-licensed desktop apps), CWE-287, CWE-639, CWE-307, CWE-602 (client-side enforcement), CWE-798 (hardcoded secrets), CWE-345 (insufficient verification of authenticity), CWE-352/CSRF-equivalent for webhook/edge-function abuse, CWE-916 (weak password hashing), CWE-208 (timing channel), CWE-22 (path traversal in image/CSV imports).

---

## Key vulnerability categories

Grouped by **Confirmed structurally present** (the surface exists; correctness needs code review) vs **Possible** (commonly seen in apps of this shape; need evidence).

### A. Authentication and session (local POS users) — Confirmed surface, correctness unverified

- **A1. Weak password hashing (CWE-916).** Possible if `UserRepository.cs` stores anything other than a memory-hard KDF (Argon2id, scrypt, or PBKDF2 with ≥600k SHA-256 iters per OWASP Password Storage Cheat Sheet). MD5/SHA-1/SHA-256-without-salt would be a critical finding.
- **A2. Plain-text or DPAPI-only credential storage with shared key.** DPAPI scoped to `LocalMachine` instead of `CurrentUser` means any local user/process can decrypt.
- **A3. Session token weaknesses (CWE-384, CWE-613).** A `sessions` table exists; risks: predictable IDs, no expiry, no rotation on privilege change, no invalidation on password change.
- **A4. Missing brute-force / lockout (CWE-307).** No evidence of rate-limit or lockout on `LoginPage`. Insider can iterate PINs/passwords offline against the SQLite hash directly if A1/A2 are weak.
- **A5. Privilege confusion / role checks in client only (CWE-602, OWASP A01).** If role checks (manager-only refund, void, price override, settings) are only in ViewModels/UI bindings, an attacker who modifies the SQLite `users.role` column or patches the binary bypasses them.
- **A6. PIN-only auth.** POS apps frequently use 4–6 digit PINs; without lockout this is brute-forceable in seconds against the local hash.

### B. Authorization / access control (CWE-285, CWE-639, OWASP A01)

- **B1. IDOR-equivalent on local data.** Even on desktop: a low-privilege user could open SQLite directly (it's a file) and read other users' hashes, audit logs, sales — unless DB encryption is real (see D).
- **B2. Missing server-side authorization on Supabase tables.** If RLS is disabled or uses `auth.role() = 'anon'` permissively, anyone with the anon key (which ships in the client and is therefore public) can read/write telemetry, license, or installation tables.
- **B3. Function-level authorization in `license-api`.** If the edge function trusts a client-supplied `installation_id` or `device_id` without correlating it to an authenticated/signed request, an attacker can request unlimited licenses or bind a license to arbitrary device IDs.

### C. Premium-feature / license bypass (OWASP A04 Insecure Design, CWE-602, CWE-345) — Highest business-impact area

- **C1. Client-side-only premium check.** If `IsPremium`/`IsLicensed` is read from a settings row, a registry key, or a JSON file and gates UI without re-validating a signed token at the point of feature use, a flat write to that field unlocks everything. This is the single most common bypass for desktop apps.
- **C2. License signature verification flaws.** With `backend-signing-public.pem` in source it appears you do offline signature verification — good. Common bugs:
  - Verifying signature but **not** the embedded `installation_id`/`hardware_id`, allowing one license to unlock all installs.
  - Verifying signature but **not** the `not_before`/`expires_at` claims, allowing expired licenses.
  - Accepting algorithm `none` or downgrade (classic JWT pitfall, CVE-2015-9235 class).
  - Using a vulnerable curve/key length, or RSA-PKCS1v1.5 without strict padding checks.
  - Public key shipped as a writable file → attacker swaps in their own key and signs their own licenses (CWE-347).
- **C3. Replay / clone (CWE-294).** If a license file is just copied from one machine to another and there's no per-install binding (hardware fingerprint signed into the token), it's freely shareable.
- **C4. Clock manipulation.** Pure local-clock expiry checks are bypassed by setting the system clock back. Trusted-time anchor (last seen server time / monotonic counter / audited app heartbeat) needed.
- **C5. Grace-period abuse.** Offline grace windows that reset on reinstall or DB reset = infinite trial.
- **C6. License generator in repo.** `LicenseKeyGen/Program.cs` plus `backend-signing-public.pem` is fine **only** if the corresponding **private key has never been committed** and is held only in the Supabase function's secrets. Need to confirm.
- **C7. Trial / install-id forgery.** If install IDs are client-generated GUIDs that are accepted unconditionally, attackers rotate IDs to refresh trial.

### D. Local data integrity and confidentiality (OWASP A02, CWE-311, CWE-326)

- **D1. SQLite encryption strength.** `DatabaseEncryptionService.cs` exists, but Microsoft.Data.Sqlite does **not** ship with SQLCipher by default; using a custom envelope encryption (encrypt blobs only) leaves table structure, indexes, and column data exposed. Need to verify whether real page-level encryption (SQLCipher / SEE) is in use, and how the key is derived.
- **D2. Encryption key storage.** If the DB key is derived from a hardcoded constant, app secret, or user PIN without a KDF, it's recoverable from the binary (CWE-798, CWE-321). DPAPI-protected keys are reasonable but tied to user/machine, with the trade-offs noted in A2.
- **D3. Audit log integrity.** `AuditLogRepository.cs` exists. Without hash-chaining or signing, an attacker with DB access edits the log to hide refund fraud, price overrides, or post-close edits.
- **D4. Receipt sequence integrity.** `receipt_sequence` table is owned by Sales. If gaps are not detected and reconciled (and not signed/HMACed), receipts can be deleted or reordered to skim cash.

### E. Backend (Supabase) misconfiguration (OWASP A05)

- **E1. Anon key exposure.** The Supabase anon key will end up in the shipped binary; that's normal but only safe if **every** table relying on it has correct RLS. If a developer ever ran `disable RLS` or wrote permissive policies, you have an open data store.
- **E2. Service-role key leakage.** Service-role keys must never be in the client, in `supabase/config.toml` committed values, or in a public bucket. Confirm none are in repo or build artifacts.
- **E3. Edge-function input validation.** `license-api` should validate JSON schema, length-limit inputs, reject unsigned/unauthenticated requests, and rate-limit per IP/install.
- **E4. CORS / origin policy.** Edge functions reachable from `*` make CSRF-equivalent abuse trivial; for a license API, restrict to expected callers or rely on per-request HMAC.
- **E5. Webhook verification.** No payment webhooks were observed in the file inventory, but if you add Stripe/PayPal/etc., they must verify the provider's signature header (Stripe-Signature, PayPal-Transmission-Sig) against the raw body before parsing — failing this is a top-three SaaS bypass.

### F. Rate limiting / abuse prevention

- **F1. Login lockout** (see A4).
- **F2. License-API rate limit.** Without per-IP and per-install limits, an attacker brute-forces install-id or licence-code spaces.
- **F3. Telemetry forgery.** Anyone with the anon key can flood `installation_events` to pollute analytics or hide their own activity.

### G. Server-side validation gaps (OWASP A03/A04)

- **G1. Trusting client time, client tax totals, client price.** Receipts/sales are computed on the desktop. If they are pushed to a backend later for reporting/sync without recomputation, an insider can rewrite line items before sync. (You may not currently sync sales — needs confirmation.)
- **G2. CSV/image import.** `ProductImportProgress.cs`, CSV importers, and `006-product-pictures` plan accept user-supplied files. Risks: zip-slip / path traversal (CWE-22), oversize/decompression bombs, image parsing in `Windows.Graphics.Imaging` against malicious files (CVE history exists for image codecs).

### H. Secrets and supply-chain (OWASP A02/A08, CWE-798)

- **H1. Secrets in source/artifacts.** Need explicit grep for `service_role`, `SUPABASE_SERVICE_ROLE_KEY`, private keys, `*.pfx` passwords, OAuth client secrets, and Stripe keys across `supabase/`, `Deployment/`, `artifacts/`, and `.env*`.
- **H2. Code-signing PFX in repo.** `Deployment/Certification/NexillTest.pfx` is committed — if this is your **production** signing cert, that's a critical exposure (anyone can sign malicious updates that look authentic). If it is only a test cert and the production cert is provisioned out-of-band, document and confirm.
- **H3. Installer integrity.** Inno Setup single-file installers must be signed; an MSIX bundle is signed by definition, but the Inno path needs `signtool` integration. Unsigned installers enable trojanized-update attacks against your user base (highest blast radius).
- **H4. Update channel authenticity.** If the app self-updates (or fetches metadata via `Sync-ReleaseMetadata.ps1` patterns) without verifying a signature on the update payload, see CVE-2020-1464 class.
- **H5. Dependency pinning.** Verify package references in `Directory.Build.props` and `.csproj` files are pinned and that the `node_modules` (Supabase CLI) is dev-only and not bundled.

### I. Cryptographic implementation issues

- **I1. Use of `System.Security.Cryptography.ProtectedData` (DPAPI).** Fine for per-user secrets on Windows, but: not portable, not effective against malware running as the same user, and `LocalMachine` scope reduces protection.
- **I2. RNG.** All tokens, install IDs, license nonces, and session IDs must use `RandomNumberGenerator` (cryptographic), not `Random` (CWE-338).
- **I3. Password equality / signature comparison must be constant-time** (CWE-208).

### J. Deployment / install-time risks

- **J1. Per-machine vs per-user install.** A per-machine install path (Program Files) means a low-priv user cannot tamper with binaries (good), but a per-user MSIX install means binaries live in writable user space — an attacker can patch them. Confirm install scope.
- **J2. AppData paths.** `AppDataPaths.cs` likely uses `LocalAppData`, which is per-user — anyone running as that user can read all encrypted blobs/DB keys derived from per-user DPAPI.

---

## Evidence needed to confirm each issue

| Finding | Files / artifacts to inspect | What confirms it |
|---|---|---|
| A1 (hash strength) | `RetailStorePOS.Data/Repositories/UserRepository.cs`, `Models/User.cs`, the migration that creates `users` | KDF used, iteration count, salt source/length |
| A2 (DPAPI scope) | All `ProtectedData.Protect(...)` call sites | `DataProtectionScope.CurrentUser` vs `LocalMachine` |
| A3 (sessions) | `Models/Session.cs`, `AuthService.cs`, `LoginRuntime.cs`, sessions migration | Token length/source, expiry, invalidation paths |
| A4/F1 (lockout) | `Views/LoginPage.xaml.cs`, `AuthService.cs` | Counter, backoff, persistent across restarts |
| A5/B1 (role checks) | `UsersPageViewModel.cs`, every privileged action (refund/void/price override/settings/users) | Server- or service-layer guard, not just XAML `IsEnabled` binding |
| C1 (client-only premium) | Search the codebase for `IsPremium`, `IsLicensed`, feature flags, settings keys | Whether the check re-verifies a signed token at use or only reads a bool |
| C2 (signature verification) | License verification routine in `Nexill.RetailStorePOS` (search for `backend-signing-public`, `Verify`, `Ed25519`/`RSA`) and `supabase/functions/license-api/index.ts` | Algorithm whitelist, claim validation, key pinning, key file integrity |
| C3 (per-install binding) | Same + `DeviceIdentityRunner`, `Tests/DeviceIdentityTests/*` | Hardware/install ID is part of signed payload and re-checked at every launch |
| C4 (clock) | License verification, telemetry heartbeat | Use of trusted server time, monotonic counter, "max-clock-skew" detection |
| C6 (private key) | `git log --all` against `*.pem`, `*.key`, `LicenseKeyGen/`, `supabase/functions/license-api/` | Whether `backend-signing-private*` was ever committed |
| D1/D2 (DB encryption) | `RetailStorePOS.Data/DatabaseEncryptionService.cs`, `SqliteConnectionFactory.cs` | Whether SQLCipher or column-level encryption is used and how the key is derived/stored |
| D3 (audit chain) | `Repositories/AuditLogRepository.cs`, `Models/AuditLog.cs` | Presence of prev-hash field or per-row signature |
| D4 (receipt seq) | `Modules/Sales/SaleRepository.cs` (in `RetailStorePOS.Data/Modules/Sales`), `receipt_sequence` migration | Gap detection, signing/HMAC of finalized receipts |
| E1/E2 (RLS, secret leak) | `supabase/migrations/*.sql`, `supabase/config.toml`, repo-wide secret scan | Per-table RLS policies; absence of service-role key in client/repo |
| E3/F2 (license-api) | `supabase/functions/license-api/index.ts` | Auth header, schema validation, rate limit |
| G2 (file imports) | Product import code paths (CSV/image), `006-product-pictures` spec | Path normalization, MIME/header validation, size limits |
| H1/H2 (secrets) | Repo-wide regex scan, `Deployment/Certification/NexillTest.pfx` provenance | Confirmation that committed PFX is **only** test, never used to sign released MSIX/EXE |
| H3/H4 (installer/update integrity) | `Deployment/build-singlefile-installer*.ps1`, `Sync-ReleaseMetadata.ps1`, `setup_singlefile*.iss`, `AutoSign-MSIX.ps1` | `signtool` invocation present and using prod cert; update payloads verified |
| I2 (RNG) | Search `new Random(`, `Guid.NewGuid()` for security uses | Replace with `RandomNumberGenerator.GetBytes` for tokens |

---

## Defensive tests you can run safely

All are read-only or sandboxed; none modify production.

1. **Static secret scan.** Run `gitleaks detect --source . --redact` and `trufflehog filesystem .` over the repo, including `.git/` history. Look for service-role keys, private keys, PFX passwords, Stripe keys.
2. **Public-key file integrity test.** Replace `backend-signing-public.pem` in an installed copy with an attacker-controlled key and a self-signed license; verify the app **rejects** it. If it accepts, signature verification is broken or the key isn't pinned.
3. **License binding test.** Take a valid license from machine A, install on machine B with a different hardware/install ID, and confirm activation **fails**. If it succeeds, you have C3.
4. **Clock-rollback test.** Activate a time-limited/trial license, set Windows clock back 30 days, relaunch, confirm app refuses or detects rollback.
5. **Premium-flag tamper test.** With the app closed, open `pos.db` (read-only copy) in DB Browser for SQLite, identify any plaintext `is_premium`/`license_state` rows; if found, that's C1. (Do not modify a production DB.)
6. **Hash-strength check.** Inspect a test user's stored credential row; confirm it's an Argon2id/PBKDF2 envelope with non-trivial parameters. If it looks like raw hex of length 32/40/64 with no salt prefix, escalate.
7. **Brute-force timing test.** In a test build, time 1000 wrong-password attempts against `AuthService.AuthenticateAsync`. If average latency stays under ~250 ms with no lockout, A4/F1 are open.
8. **Supabase RLS audit.** In Supabase dashboard or `supabase db dump --schema public`, list every table and confirm `ALTER TABLE ... ENABLE ROW LEVEL SECURITY` plus least-privilege policies. Try a `curl` with the anon key against each table.
9. **Edge-function fuzz.** Send malformed JSON, oversized payloads, missing auth, and replayed nonces to `license-api`; confirm 4xx responses, no 5xx leaks, no second-issue of the same license.
10. **Installer signature verification.** `Get-AuthenticodeSignature` on the produced MSIX/EXE. Confirm `Status = Valid`, `SignerCertificate` is the production cert (not `NexillTest.pfx`).
11. **Process-memory privacy check.** Run the app, dump memory with a benign tool (e.g., Process Explorer minidump on a test box), and grep for plaintext passwords/keys/license payloads. Ephemeral presence is unavoidable; persistent presence is a finding.
12. **Path-traversal fuzzing on importers.** Feed CSVs and image archives with `..\..\` paths, oversize headers, and zip-slip patterns into a sandboxed test instance.
13. **Audit-log tamper test.** In a test DB, modify an audit row directly; confirm a tamper-evident check (hash chain, daily HMAC) flags it on next launch.

---

## Recommended patches (controls only — implementation is your call)

### Authentication / access control

- Use **Argon2id** (recommended params per OWASP) or **PBKDF2-HMAC-SHA256 ≥ 600,000 iterations** with a per-user 16-byte random salt; store algorithm/params alongside the hash for future migration.
- Implement **progressive lockout** (e.g., 5 failures → 30s, 10 → 5m, 20 → admin-unlock) and persist the counter in the DB.
- Generate session tokens with `RandomNumberGenerator.GetBytes(32)`; rotate on privilege change; expire after configurable idle and absolute lifetimes; invalidate on password change.
- Enforce **role checks at the service/repository layer**, not in views. Treat the UI as untrusted.
- Use **constant-time comparison** (`CryptographicOperations.FixedTimeEquals`) for all hash/MAC/signature comparisons.

### Premium / license

- Keep license verification **stateless and signature-based**, with claims `installation_id`, `not_before`, `expires_at`, `tier`, `nonce`. Verify signature, algorithm allow-list, and **all** claims at every relevant feature entry — not once at startup.
- Bind to a **stable hardware/install fingerprint** computed from multiple sources (machine GUID, TPM EK if available, MAC fallback) and **sign that fingerprint into the license**. Re-derive on launch.
- Use a **trusted-time anchor**: store last-known-good server time (signed by backend) in DPAPI; refuse to advance entitlement if local clock < anchor.
- Keep the license **private key** only in Supabase Edge Function secrets; rotate on suspicion; never commit.
- Pin the **public key** by embedding it as a compile-time constant (`byte[]`) in addition to (or instead of) a file, so tampering with the file alone doesn't bypass.
- Make the premium gate a function call that returns a freshly verified entitlement object — not a boolean field on a settings row.

### Local data

- If the threat model includes T1 (local crackers) at all, switch to **page-level encryption** (SQLCipher, or move to E.F. Core with a provider that supports it) rather than column blobs, and derive the key with Argon2id from a user secret + DPAPI-wrapped salt.
- Hash-chain the **audit log** (`row.hash = H(prev_hash || row_payload)`) and verify on startup; alert on mismatch.
- HMAC finalized **receipts** with a per-install key; detect gaps in `receipt_sequence` and surface in the readiness dashboard.

### Backend

- Verify **RLS is on** for every table; default-deny; one explicit policy per role.
- Treat the anon key as public; never put service-role keys client-side.
- For `license-api`: require an HMAC signed by the install (challenge-response) or a short-lived token; validate JSON schema; rate-limit per IP and install; idempotent issue-license endpoint keyed by `(install_id, license_request_id)`.
- If a payment provider is added, **verify webhook signatures** against the raw body using the provider's documented header before any state change; require timestamp tolerance ≤ 5 min to block replays.

### Secrets / build

- Run `gitleaks` in CI on every PR; fail builds on findings.
- Move `NexillTest.pfx` out of repo if it's used for any artifact users see; production code-signing cert lives in Azure Key Vault / Trusted Signing / hardware token only.
- Sign **every** released artifact (MSIX is signed by manifest; the Inno single-file EXE must be signed via `signtool` with the same cert).
- If an in-app updater is added, require a signed update manifest verified with a pinned key.

### Imports / file handling

- Reject CSVs > a configured size; validate MIME and re-encode images via `Windows.Graphics.Imaging` before persisting; canonicalize and sandbox output paths under `AppDataPaths` with `Path.GetFullPath` containment check.

---

## Priority table

Severity uses CVSS-style intuition (Likelihood × Impact). "Confirmed" = surface exists and one of the listed bug patterns commonly applies; "Possible" = needs evidence.

| # | Issue | Status | Severity | Exploitability | Business impact | Priority |
|---|---|---|---|---|---|---|
| 1 | C1 Client-side-only premium check | Possible | Critical | Trivial (DB/file flip) | Direct revenue loss, scaled via cracks | **P0** |
| 2 | C2 License signature-verification flaws (alg/claims/key pinning) | Possible | Critical | Medium–High | Mass revenue loss | **P0** |
| 3 | H2 Production code-signing cert handling (`NexillTest.pfx`) | Confirmed in repo, intent unknown | Critical if prod | Trivial if prod | Trojanized updates to all users | **P0** until clarified |
| 4 | E1 Supabase RLS gaps / E2 service-role leakage | Possible | Critical | Trivial if anon key works | Data theft, telemetry forgery, license abuse | **P0** |
| 5 | A1 Weak password hashing | Possible | High | Offline brute force after T1 access | Insider account takeover | **P1** |
| 6 | A4/F1 No login lockout | Possible | High | Trivial (PIN brute force) | Insider takeover, refund fraud | **P1** |
| 7 | A5 Role checks only in UI | Possible | High | Medium | Insider escalation, refund/void fraud | **P1** |
| 8 | C3 No per-install binding / C4 Clock rollback | Possible | High | Trivial if missing | License sharing | **P1** |
| 9 | D1/D2 SQLite encryption strength and key handling | Confirmed surface | High | Medium (T1) | Sales/users data leak | **P1** |
| 10 | E3/F2 License-api validation & rate-limit | Possible | High | Medium | License brute-force, abuse | **P1** |
| 11 | D3 Audit log tampering | Possible | Medium–High | Medium (needs T1) | Fraud concealment | **P2** |
| 12 | D4 Receipt sequence integrity | Possible | Medium | Medium | Cash skimming, audit failure | **P2** |
| 13 | A3 Session token weaknesses | Possible | Medium | Medium | Privilege misuse | **P2** |
| 14 | G2 Importer path traversal / image parsing | Possible | Medium | Low–Medium | Local code/data corruption | **P2** |
| 15 | H3/H4 Installer/update signing | Likely OK for MSIX, verify Inno path | Medium | Low | Supply-chain compromise | **P2** |
| 16 | I2 Non-cryptographic RNG for secrets | Possible | Medium | Medium | Token prediction | **P2** |
| 17 | F3 Telemetry forgery | Possible | Low–Medium | Trivial | Polluted analytics | **P3** |
| 18 | G1 Trust in client-computed totals on later sync | Possible (if/when cloud sync added) | Medium | Medium | Reporting fraud | **P3** |

---

## Questions to answer before stronger conclusions

1. **License flow.** Is license verification fully offline (signed token, public-key verify) or does it require at least one online activation per install? Where exactly is the public key embedded, and is it also in source as a constant?
2. **Premium gate.** Where in the codebase is `IsPremium` (or the equivalent) consumed? Is it a property on a settings/preferences object read once at startup, or is each premium feature gated by a fresh signature check?
3. **Hashing.** What algorithm and parameters does `UserRepository` use to store credentials? Is there a salt column? An algorithm/version field?
4. **Sessions.** How is the session token generated, where stored (DB row vs encrypted file vs DPAPI), and how is it invalidated?
5. **DB encryption.** Is `DatabaseEncryptionService` doing page-level (SQLCipher/SEE) encryption or column/blob-level only? How is the key derived and stored?
6. **Supabase posture.** Is RLS enabled on every public schema table? Is the service-role key referenced anywhere outside `supabase/functions/*` server-side code? Is the `license-api` function authenticated, and how?
7. **Webhooks/payments.** Are payment providers (Stripe, Paddle, PayPal, app store IAP) involved at all, or is the only commercial path your own license issuance? If providers exist, where is the webhook handler and how does it verify signatures?
8. **Code-signing cert provenance.** Is `Deployment/Certification/NexillTest.pfx` used to sign **any** artifact a user receives, or only for local dev/test? Where does the production signing cert live?
9. **Distribution channels.** Are Microsoft Store MSIX, Inno single-file installer, and `uptodown` all live channels? Each needs a signing story.
10. **Sync.** Are sales/receipts ever pushed to a backend (now or planned), and if so, are line totals/taxes recomputed server-side?
11. **Hardware fingerprint.** What does `DeviceIdentityRunner` / `Tests/DeviceIdentityTests` actually compute, and is it part of the signed license payload?
12. **Audit log.** Is there any tamper-evidence mechanism (hash chain, periodic HMAC, signed daily summary), or is `audit_log` an ordinary INSERT-only table?
13. **Roles model.** What roles exist (Cashier, Manager, Admin?), and where are the authoritative role checks for refund/void/price-override/user-management/settings?
14. **Dependency hygiene.** Are NuGet versions pinned (no floating versions)? Any preview/unsigned packages?

---

## Next step (suggested)

The fastest way to convert "Possible" findings into Confirmed/Refuted with line-level citations is to read, in this order:

1. License verification path — embedded public key, signature algo, claims checked, public-key file integrity.
2. Premium-gate consumers — every call site that branches on `IsPremium`/`IsLicensed`.
3. Supabase RLS state and the `license-api` Edge Function (auth, validation, rate limit).
4. `UserRepository` hash storage and `AuthService` lockout behavior.
5. `DatabaseEncryptionService` key derivation and scope.
