# RetailStorePOS — Security Review & Implementation Roadmap

**Date:** 2026-05-13  
**Last Updated:** 2026-05-17  
**Scope:** Full architecture, code, Supabase, database, authentication, licensing, offline security  
**App Status:** Free version live, preparing infrastructure for paid features

---

## COMPLETION STATUS

### ✅ DONE

| # | Task | Date Completed |
|---|------|----------------|
| P0-1 | Private signing key removed from disk | 2026-05-13 |
| P0-2 | New ECDSA P-256 key pair generated securely (via Supabase Edge Function) | 2026-05-13 |
| P0-3 | Private key stored ONLY in Supabase Secrets (JWK format) | 2026-05-13 |
| P0-4 | Public key updated in repo (`supabase/public-keys/backend-signing-public.jwk.json`) | 2026-05-13 |
| P0-5 | Signing verified working end-to-end (full certificate issued successfully) | 2026-05-13 |
| P0-6 | `.gitignore` updated — `*.pem`, `*private*`, `.tmp/`, `.env`, `supabase/.temp/` | 2026-05-13 |
| P0-7 | LiveChartsCore pinned to exact version `2.0.2` (was `2.*`) | 2026-05-13 |
| P0-8 | Downgrade prevention added to `DatabaseInitializer` (`DatabaseTooNewException`) | 2026-05-13 |
| P0-9 | Corrupted source files recreated (10 files) | 2026-05-13 |
| P0-10 | BOM stripped from XAML/manifest files (XAML compiler fix) | 2026-05-13 |
| P0-11 | Full solution builds and runs successfully | 2026-05-13 |
| P0-12 | `BACKEND_SIGNING_KEY_ID` secret updated to the current signing key UUID in Supabase | 2026-05-14 |
| P0-13 | `generate-keys-index-ts` function deleted from Supabase dashboard | 2026-05-14 |

### ✅ COMPLETED AFTER INITIAL REVIEW

| # | Task | Risk if skipped |
|---|------|-----------------|
| P0-D | ~~Update `BACKEND_SIGNING_KEY_ID` secret to new UUID in Supabase~~ | ~~Previously: confusing key tracking when you rotate keys later~~ |
| P0-E | ~~Delete `generate-keys-index-ts` function from Supabase dashboard~~ | ~~Previously: unnecessary attack surface — anyone can generate keys if JWT is off~~ |

---

## REMAINING ATTACK VECTORS (How Someone Can Crack/Hack Your App)

### Vector 1: Database Theft (EASY — No Encryption)

**Difficulty:** Trivial  
**What attacker does:** Copy `pos.db` from `%LocalAppData%\RetailStorePOS\`  
**What they get:** ALL data — user credentials, sales, products, settings, tax info  
**Then:** Crack PINs in <1 second (10K PBKDF2 iterations + 4-digit PIN = instant)  
**Status:** ❌ NOT FIXED — database is still plaintext, hashing still weak

### Vector 2: Binary Decompilation (EASY — No Obfuscation)

**Difficulty:** Trivial (ILSpy, dnSpy — free tools, 30 seconds)  
**What attacker does:** Open the .exe in a decompiler  
**What they get:** All source code, Supabase anon key, bootstrap admin password, all logic  
**Then:** Understand entire security model, find bypass points, extract embedded keys  
**Status:** ❌ NOT FIXED — no obfuscation, no tamper detection

### Vector 3: License Bypass (TRIVIAL — Not Implemented)

**Difficulty:** Zero effort  
**What attacker does:** Nothing — license validation doesn't exist yet  
**What they get:** Full app access (it's free anyway, but when paid features are added, there's nothing to bypass)  
**Status:** ⏳ EXPECTED — will be built with paid features (P3)

### Vector 4: Brute-Force PINs Locally (EASY — No Lockout)

**Difficulty:** Minutes with a script  
**What attacker does:** Try all 10,000 possible 4-digit PINs at the login screen  
**What they get:** Access to any user account  
**Why it works:** No lockout, no rate limiting, no delay between attempts  
**Status:** ❌ NOT FIXED — no account lockout exists

### Vector 5: Walk-Up to Unattended Terminal (EASY — No Timeout)

**Difficulty:** Walk up and use it  
**What attacker does:** Wait for cashier to walk away  
**What they get:** Full access to whatever the logged-in user can do  
**Why it works:** Sessions never expire, no idle timeout  
**Status:** ❌ NOT FIXED — no session timeout

### Vector 6: Brute-Force License Keys Online (MEDIUM — No Rate Limit)

**Difficulty:** Hours with a script  
**What attacker does:** Send thousands of requests to the license-api trying different keys  
**What they get:** Valid license keys for free access  
**Why it works:** No rate limiting, no JWT required, unlimited attempts  
**Status:** ❌ NOT FIXED — edge function has no rate limiting

### Vector 7: Replay License Activation (MEDIUM — Optional Sequence)

**Difficulty:** Moderate (need to intercept one valid request)  
**What attacker does:** Capture a valid activation request, replay it from another device  
**What they get:** Activation on unauthorized device  
**Why it works:** `requestSequence` can be null (bypasses replay protection)  
**Status:** ❌ NOT FIXED — null sequence still allowed

### Vector 8: Reactivate Revoked License (EASY — No Permanent Revocation)

**Difficulty:** One API call  
**What attacker does:** Call verify endpoint after being revoked  
**What they get:** License reactivated as if nothing happened  
**Why it works:** `saveActivation` clears revocation state  
**Status:** ❌ NOT FIXED — revocation is not permanent

### Vector 9: MITM Telemetry/License Traffic (MEDIUM — No Cert Pinning)

**Difficulty:** Moderate (need trusted root CA on machine)  
**What attacker does:** Install corporate proxy or MITM tool  
**What they get:** Intercept license responses, potentially forge "valid" responses  
**Why it works:** No certificate pinning, app trusts any CA in system store  
**Status:** ❌ NOT FIXED — no certificate pinning

### Vector 10: Clock Manipulation for Expired Certificates (EASY — Bypassable)

**Difficulty:** Set system clock back  
**What attacker does:** Roll back system time to extend expired license certificate  
**What they get:** Continued access past certificate expiry  
**Why it works:** Time validation shows a warning but user can dismiss it  
**Status:** ❌ NOT FIXED — time check is not enforced

### Vector 11: Default Admin Credentials (EASY — Hardcoded)

**Difficulty:** Read the binary or just guess  
**What attacker does:** Try `admin` / `1234` on any installation  
**What they get:** Full admin access if owner never changed the password  
**Why it works:** Hardcoded in binary, no forced change on first login  
**Status:** ❌ NOT FIXED — bootstrap creds still hardcoded, no forced change

### Vector 12: Supabase Anon Key Extraction (TRIVIAL)

**Difficulty:** Decompile binary, read assembly metadata  
**What attacker does:** Extract the Supabase URL + anon key  
**What they get:** Ability to call edge functions directly (license-api has no JWT requirement)  
**Current protection:** RLS denies all direct table access (good), but combined with Vector 6, enables brute-force  
**Status:** ⚠️ ACCEPTABLE RISK — Supabase anon key is designed to be public. Real protection is RLS + rate limiting (rate limiting not yet added)

---

## What This Document Covers

This is a complete security audit of the RetailStorePOS application. It explains every vulnerability found, why it matters, what an attacker could do with it, and exactly what to fix — in priority order.

---

## CRITICAL FINDINGS (Fix Immediately)

### 1. Private Signing Key Has No Git Protection

**What it is:** The file `LicenseKeyGen/backend-signing-private.pem` contains the ECDSA P-256 private key that signs license certificates. This is the root of trust for your entire licensing system.

**The problem:** There is NO `.gitignore` rule preventing this file from being committed. One `git add .` and the key is in version history forever.

**What an attacker can do:** If they get this key, they can forge valid license certificates for any device. Your entire licensing system becomes worthless — they can generate unlimited free licenses.

**The fix:** Add patterns to `.gitignore` and delete the local copy after confirming it's deployed to Supabase secrets.

**Cons of fixing:** None. There is zero downside to protecting this key.

---

### 2. No License Validation Code Exists

**What it is:** The files `Services/Licensing/LicenseValidationService.cs` and `Services/Licensing/PublicLicenseKey.cs` are both 0 bytes — completely empty scaffolds.

**The problem:** The application has NO license enforcement whatsoever. Every feature is accessible to everyone for free.

**What an attacker can do:** Nothing needed — the app is already "cracked" by default since there's nothing to crack.

**The fix:** Implement the license validation service that checks signed certificates offline.

**Cons of fixing:**
- Development time: 2-3 days minimum
- Adds startup latency (~50-100ms for signature verification)
- Requires handling edge cases (expired cert, wrong device, corrupted cert, grace periods)
- Must be done carefully or legitimate users get locked out

---

### 3. Supabase API Key Embedded in Binary

**What it is:** In `Directory.Build.props`, the Supabase URL and anon key are baked into assembly metadata at build time. The app reads them in `LoginRuntime.cs` as a fallback. Anyone with ILSpy or dnSpy can extract them in 30 seconds.

**The problem:** The anon key allows calling your Supabase edge functions and API. Currently safe because RLS denies all direct table access, but if you ever add permissive RLS policies, it becomes a data breach.

**What an attacker can do:**
- Call your license-api edge function directly (already possible since `verify_jwt = false`)
- If RLS policies are added later, potentially read/write data directly
- Enumerate your API structure

**The fix:** Accept that the anon key is semi-public (Supabase's design). Ensure RLS stays restrictive. Never add permissive policies for the anon role. Consider moving the key entirely to DPAPI vault (loaded at first online connection).

**Cons of fixing (moving key to vault only):**
- First-run requires network connectivity to fetch the key
- Adds complexity to the bootstrap flow
- If vault is corrupted, app can't connect until re-initialized

---

### 4. Database Completely Unencrypted

**What it is:** The SQLite database (`pos.db`) stores all data in plaintext. No password, no encryption. Anyone who copies the file can open it with any SQLite tool.

**The problem:** All PII (cashier names, store info), credential hashes, sales data, tax registration numbers, and business intelligence are exposed to anyone with file access.

**What an attacker can do:**
- Copy the database file and read all business data
- Extract password/PIN hashes and crack them offline
- Modify data (change prices, delete sales records, grant admin access)
- Clone the database to another machine

**The fix:** Add SQLCipher encryption. The encryption key is derived from DPAPI (tied to the Windows user account).

**Cons of fixing:**
- Adds ~5% overhead to all database operations
- Requires replacing `Microsoft.Data.Sqlite` with SQLCipher-backed provider
- Migration path needed for existing unencrypted databases
- If DPAPI key is lost (Windows profile corruption), database becomes unreadable — need backup/recovery strategy
- Slightly larger binary size (~2MB for SQLCipher native library)


---

## HIGH SEVERITY FINDINGS

### 5. No Rate Limiting on License API

**What it is:** The Supabase edge function `license-api` has `verify_jwt = false` (no authentication required) and no rate limiting. Anyone can call it as fast as their network allows.

**The problem:** An attacker can brute-force license keys by sending thousands of requests per second. The only defense is the key space size and the pepper hash — but with no rate limit, online brute-force is viable.

**What an attacker can do:**
- Try millions of license key combinations
- Discover valid license keys through timing differences in error responses
- DoS the edge function by flooding it with requests

**The fix:** Add a sliding-window rate limiter (e.g., 5 requests per minute per IP + installId combination) using a database-backed counter.

**Cons of fixing:**
- Adds a database query per request (slight latency increase ~10-20ms)
- Legitimate users on shared IPs (corporate NAT) might hit limits
- Need to handle rate limit state cleanup (cron job or TTL)
- Adds complexity to the edge function

---

### 6. Weak Password/PIN Hashing (PBKDF2 at 10,000 Iterations)

**What it is:** User passwords and PINs are hashed with PBKDF2-SHA256 at 10,000 iterations. OWASP's 2023 recommendation is minimum 600,000 iterations for SHA-256.

**The problem:** PINs are typically 4-6 digits (10,000 to 1,000,000 possible values). With only 10K PBKDF2 iterations, the entire PIN space can be brute-forced in under 1 second on modern hardware.

**What an attacker can do:**
- Steal the database file (unencrypted, see #4)
- Crack ALL user PINs in seconds
- Crack passwords in hours/days depending on complexity

**The fix:** Increase to 600,000 iterations. For PINs specifically, consider Argon2id which is memory-hard and resists GPU attacks.

**Cons of fixing:**
- Login takes ~100-200ms longer (600K iterations vs 10K)
- Existing hashes need re-hashing on next successful login (migration period)
- Argon2id requires an additional NuGet package (`Konscious.Security.Cryptography` or `Isopoh.Cryptography.Argon2`)
- Higher CPU usage during login on low-end POS hardware

---

### 7. Hardcoded Bootstrap Admin Credentials

**What it is:** `LoginRuntime.cs` contains hardcoded constants: `BootstrapAdminPassword = "1234"` and `BootstrapAdminPin = "1234"`. These create the initial admin account on first run.

**The problem:** These values are permanently embedded in the compiled binary. If the user never changes the default password, the system remains accessible with `admin/1234` forever. Anyone who decompiles the app knows the defaults.

**What an attacker can do:**
- Try `admin/1234` on any RetailStorePOS installation
- If the owner didn't change the password, gain full admin access
- Even if changed, the constants reveal the initial credential pattern

**The fix:** Force password change on first login. Generate a random initial password displayed once during setup. Remove hardcoded constants.

**Cons of fixing:**
- Slightly more complex first-run experience
- Need to handle the case where user forgets the generated password before changing it
- Random password must be displayed clearly in the UI (UX challenge)

---

### 8. No Brute-Force Protection on Local Login

**What it is:** `AuthService.cs` has no account lockout mechanism. Failed login attempts are logged but never throttled or blocked.

**The problem:** An attacker with physical access to the POS terminal can try unlimited PIN/password combinations with no consequence.

**What an attacker can do:**
- Try all 10,000 possible 4-digit PINs in minutes (automated)
- Try common passwords indefinitely
- No alert is raised, no account is locked

**The fix:** Implement progressive lockout: after 5 failed attempts, lock the account for 15 minutes. After 10, lock for 1 hour. Log all lockout events.

**Cons of fixing:**
- Legitimate users who forget their PIN get locked out
- In a busy retail environment, lockouts cause operational disruption
- Need an admin unlock mechanism (which itself must be secured)
- Lockout state must survive app restart (store in DB)

---

### 9. No Session Timeout

**What it is:** Once logged in, the session persists indefinitely until explicit logout or app close. No idle timeout exists.

**The problem:** If a cashier walks away from the POS terminal without logging out, anyone can use it. In retail environments, this happens constantly.

**What an attacker can do:**
- Walk up to an unattended terminal and perform transactions
- Access admin functions if an admin left the session open
- View/export sensitive business data

**The fix:** Add configurable idle timeout (default 15 minutes). Lock the session (require PIN re-entry) after timeout.

**Cons of fixing:**
- Cashiers interrupted mid-transaction lose their cart (unless cart persists across lock)
- Frequent re-authentication is annoying in high-volume retail
- Need to balance security vs. operational speed
- Should be configurable per-store (some want 5 min, some want 30 min)

---

### 10. Legacy SHA-256 Hashes Never Auto-Upgraded

**What it is:** `UserRepository.cs` supports old unsalted SHA-256 password hashes for backward compatibility. When a user with a legacy hash logs in successfully, the hash is NOT automatically upgraded to the stronger PBKDF2 format.

**The problem:** Users who haven't changed their password since the old version still have trivially crackable hashes. SHA-256 without salt means rainbow tables work directly.

**What an attacker can do:**
- Use precomputed rainbow tables to crack legacy hashes instantly
- No brute-force needed — just a lookup

**The fix:** On successful login with a legacy hash, transparently rehash with PBKDF2 and update the database.

**Cons of fixing:**
- Minimal — this is a straightforward improvement
- Adds one extra DB write on login for legacy users (one-time per user)
- Need to handle the edge case where the DB write fails after successful auth

---

### 11. Revoked License Activations Can Be Reactivated

**What it is:** In `verify-license.ts`, calling the verify endpoint on a previously revoked activation sets `status: "active"` and clears `revoked_at`/`revoke_reason`.

**The problem:** Revocation is not permanent. A user whose license was revoked (e.g., for chargeback or abuse) can simply call the API again to reactivate.

**What an attacker can do:**
- Get revoked for abuse
- Immediately call the verify endpoint again
- License is reactivated as if nothing happened

**The fix:** Check for revocation state before allowing reactivation. Only allow reactivation through an explicit admin action.

**Cons of fixing:**
- Need a separate "admin reactivate" endpoint
- Edge case: if revocation was accidental, recovery requires admin intervention
- Slightly more complex activation logic

---

### 12. No Binary Obfuscation or Tamper Detection

**What it is:** The compiled .NET application has no obfuscation, no integrity checks, and no anti-tampering measures. It can be decompiled to near-source-code quality with free tools.

**The problem:** An attacker can:
- Decompile the entire app and read all logic
- Find and patch out license checks (once implemented)
- Extract embedded keys and constants
- Redistribute a cracked version

**What an attacker can do:**
- Use ILSpy/dnSpy to read all source code
- Patch the binary to skip license validation
- Create a "crack" that others can use
- Understand the entire security model to find weaknesses

**The fix:** Add .NET obfuscation (Dotfuscator, ConfuserEx) and runtime integrity self-checks.

**Cons of fixing:**
- Obfuscation makes debugging harder (need to keep symbol maps)
- Some obfuscators break reflection-heavy code (WinUI uses reflection)
- ConfuserEx is free but less maintained; Dotfuscator Community is limited
- Professional obfuscators cost $500-2000/year
- Integrity checks add ~200ms to startup
- Determined attackers can still bypass (it raises the cost, doesn't prevent)
- Can cause false positives with antivirus software


---

## MEDIUM SEVERITY FINDINGS

### 13. SHA256(key+pepper) Instead of HMAC-SHA256

**What it is:** The edge function hashes license keys using `SHA256(licenseKey + "::" + pepper)` — simple string concatenation before hashing.

**The problem:** This is vulnerable to length-extension attacks. HMAC is specifically designed to prevent this class of attack by using a two-pass keyed hash construction.

**What an attacker can do:** Theoretically craft inputs that produce valid hashes without knowing the pepper (length-extension). In practice, the risk is low for this specific use case but it's a cryptographic anti-pattern.

**The fix:** Replace with `HMAC-SHA256(pepper, licenseKey)`.

**Cons of fixing:**
- All existing license key hashes in the database become invalid
- Need a migration: rehash all keys with HMAC, or support both formats during transition
- Slightly more complex code (minimal)

---

### 14. Replay Protection is Optional (requestSequence Can Be Null)

**What it is:** In `verify-license.ts`, if `requestSequence` is `null`, the replay protection check is skipped entirely.

**The problem:** A client that never sends a sequence number gets zero replay protection. An attacker can capture a valid request and replay it indefinitely.

**What an attacker can do:**
- Intercept one valid license verification request
- Replay it from a different device
- The server accepts it because there's no sequence to validate

**The fix:** Make `requestSequence` mandatory. Reject requests without it.

**Cons of fixing:**
- Breaks backward compatibility with older app versions that don't send sequence numbers
- Need a version negotiation mechanism or grace period
- Clients must persist sequence state locally (already planned)

---

### 15. Race Condition in max_devices Enforcement

**What it is:** Between checking the activation count and inserting a new activation, concurrent requests from different devices could exceed `max_devices`.

**The problem:** If two devices activate simultaneously, both pass the count check, both insert, and you end up with max_devices + 1 activations.

**What an attacker can do:** Send simultaneous activation requests from multiple devices to exceed the device limit by 1-2 devices.

**The fix:** Use a serializable transaction or a database-level trigger that enforces the count atomically.

**Cons of fixing:**
- Serializable transactions have higher lock contention
- Slightly more complex SQL
- May cause legitimate concurrent activations to fail (retry needed)

---

### 16. Wildcard CORS on License API

**What it is:** The edge function returns `Access-Control-Allow-Origin: *` — any website can call the API.

**The problem:** While desktop apps don't have CORS restrictions, if the API is ever called from a web context, any malicious website can probe it.

**What an attacker can do:**
- Create a webpage that calls your license API from visitors' browsers
- Use visitors' IP addresses to mask brute-force attempts
- Probe for valid license keys through a distributed attack

**The fix:** Remove CORS headers entirely (desktop apps don't need them) or restrict to specific origins.

**Cons of fixing:**
- If you ever build a web admin panel, you'll need to add specific origins
- Minimal development effort

---

### 17. DPAPI Used Without Additional Entropy

**What it is:** `SecureStorageService.cs` calls `ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser)` — the second parameter (additional entropy) is `null`.

**The problem:** Any application running as the same Windows user can decrypt the secrets. DPAPI with `CurrentUser` scope means all apps under that user profile share the same master key.

**What an attacker can do:**
- Run malware as the same Windows user
- Call `ProtectedData.Unprotect` on the secrets file
- Extract the Supabase key, device private key, and license certificate

**The fix:** Add application-specific entropy: `ProtectedData.Protect(data, Encoding.UTF8.GetBytes("RetailStorePOS-v2"), DataProtectionScope.CurrentUser)`.

**Cons of fixing:**
- Existing encrypted data becomes unreadable (migration needed)
- Must store the entropy value in the binary (it's not a secret, just a differentiator)
- If the entropy string changes between versions, secrets are lost

---

### 18. No Certificate Pinning for HTTP Calls

**What it is:** `TelemetryService.cs` uses a standard `HttpClient` that trusts the system certificate store. No pinning to Supabase's specific certificate.

**The problem:** A corporate proxy, government firewall, or MITM attacker with a trusted root CA can intercept all traffic between the app and Supabase.

**What an attacker can do:**
- Install a trusted root CA on the machine (common in corporate environments)
- Intercept telemetry data (install IDs, hardware fingerprints, error details)
- Intercept license verification requests/responses
- Potentially modify responses (e.g., make the server always return "valid")

**The fix:** Pin the Supabase certificate's public key hash in the `HttpClientHandler`.

**Cons of fixing:**
- If Supabase rotates their certificate, the app breaks until updated
- Certificate rotation requires an app update
- Makes development/debugging harder (can't use Fiddler/Charles proxy)
- Need a fallback mechanism for certificate rotation

---

### 19. Floating NuGet Version for LiveChartsCore

**What it is:** The `.csproj` uses `Version="2.*"` for LiveChartsCore — this pulls the latest 2.x version on every restore.

**The problem:** A compromised or malicious package version could be pulled automatically without review. Supply chain attacks via NuGet are a real threat.

**What an attacker can do:**
- If the LiveChartsCore package is compromised, your next build pulls the malicious version
- The malicious code runs with full app permissions
- Could exfiltrate data, inject backdoors, or modify behavior

**The fix:** Pin to an exact version: `Version="2.0.5"` (or whatever the current version is).

**Cons of fixing:**
- Must manually update the version to get bug fixes
- Slightly more maintenance overhead
- Trivial to implement

---

### 20. Supabase Project Ref in Version Control

**What it is:** `supabase/.temp/project-ref` contains your Supabase project ID (`avuzwbmiiavbuaxmnitp`). `supabase/.temp/linked-project.json` contains your organization ID.

**The problem:** These are local development artifacts that reveal your Supabase project structure. Combined with the anon key (extractable from the binary), an attacker has everything needed to target your specific project.

**The fix:** Add `supabase/.temp/` to `.gitignore`.

**Cons of fixing:** None. These are local-only files that should never be shared.

---

### 21. Time Validation is Bypassable

**What it is:** `TimeValidationService.cs` checks if the system clock has been rolled back. But the user can dismiss the warning dialog and continue using the app.

**The problem:** Clock manipulation is a common technique to extend expired licenses or trial periods. If the check is just a warning, it provides no real protection.

**What an attacker can do:**
- Set the system clock back to extend a license certificate's validity
- Dismiss the warning and continue using the app
- Keep the app permanently offline with a rolled-back clock

**The fix:** Make time validation a hard block — if clock rollback is detected, require online verification before allowing continued use.

**Cons of fixing:**
- Legitimate clock drift (dead CMOS battery, timezone changes) could lock out users
- Users in areas with unreliable internet can't resolve the block
- Need a tolerance window (e.g., allow up to 24 hours of drift)
- More complex UX for the error state

---

### 22. Full Stack Traces in Telemetry

**What it is:** `TelemetryService.cs` sends `error.ToString()` which includes full stack traces. These may contain file paths, connection strings, user data, or other sensitive information embedded in exception messages.

**The problem:** Sensitive data could be transmitted to the Supabase backend and stored in telemetry tables.

**What an attacker can do:**
- If they gain access to telemetry data, they see internal file paths, potentially credentials in error messages, and system architecture details
- Aids in planning targeted attacks

**The fix:** Sanitize exception messages before sending. Strip file paths, redact potential credentials, limit stack trace depth.

**Cons of fixing:**
- Harder to debug production issues with sanitized traces
- Need to balance debuggability vs. security
- Regex-based sanitization might miss some patterns or over-redact

---

### 23. No File Permission Hardening on Database

**What it is:** The database directory is created with `Directory.CreateDirectory()` but no explicit ACLs are set. It inherits whatever permissions the parent folder has.

**The problem:** On a shared Windows machine (common in retail — multiple cashiers, one PC), other user accounts or administrators can read the database file.

**What an attacker can do:**
- Log in as a different Windows user on the same machine
- Navigate to `%LocalAppData%\RetailStorePOS\`
- Copy and read the entire database

**The fix:** Set restrictive ACLs on the database directory — only the owning user account gets read/write access.

**Cons of fixing:**
- If the app needs to run under multiple Windows accounts (e.g., different shifts), each gets a separate database
- Admin accounts can still override ACLs
- Adds Windows-specific code (not portable, but you're WinUI-only anyway)
- If ACLs are set wrong, the app itself can't access its own database

---

### 24. Weak Supabase Password Policy

**What it is:** `config.toml` sets `minimum_password_length = 6` and `password_requirements = ""` (no complexity).

**The problem:** If you ever add user-facing Supabase auth (admin panel, web dashboard), accounts can be created with trivially weak passwords.

**What an attacker can do:**
- Create accounts with passwords like "123456"
- Brute-force existing accounts with common passwords

**The fix:** Set minimum 8 characters with `password_requirements = "letters_digits"`.

**Cons of fixing:**
- Users with existing short passwords may need to reset
- Slightly more friction during account creation
- Minimal development effort (config change only)


---

## ARCHITECTURE: CURRENT vs. RECOMMENDED

### Current Architecture (What You Have Now)

```
┌─────────────────────────────────────────────────────┐
│  WinUI 3 App                                        │
│  - No obfuscation (fully decompilable)              │
│  - No tamper detection                              │
│  - Supabase key in binary metadata                  │
│  - Bootstrap admin: admin/1234                      │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │ SQLite Database (PLAINTEXT)                 │    │
│  │ - Credentials: PBKDF2 @ 10K iterations      │    │
│  │ - Some users: unsalted SHA-256 (legacy)     │    │
│  │ - All business data readable by anyone      │    │
│  │ - No file permission restrictions           │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │ DPAPI Vault (no extra entropy)              │    │
│  │ - Supabase anon key                         │    │
│  │ - Any same-user app can decrypt             │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │ License Validation: NOT IMPLEMENTED         │    │
│  │ - Empty scaffold files (0 bytes)            │    │
│  │ - App runs fully unlocked                   │    │
│  └─────────────────────────────────────────────┘    │
│                                                     │
│  ┌─────────────────────────────────────────────┐    │
│  │ Auth: No lockout, no timeout, no limits     │    │
│  └─────────────────────────────────────────────┘    │
└──────────────────────┬──────────────────────────────┘
                       │ HTTPS (no pinning, no enforcement)
                       │
┌──────────────────────▼──────────────────────────────┐
│  Supabase Edge Function                             │
│  - No authentication required (verify_jwt=false)    │
│  - No rate limiting                                 │
│  - Revoked licenses can self-reactivate             │
│  - Replay protection optional                       │
│  - SHA256 concatenation (not HMAC)                  │
└─────────────────────────────────────────────────────┘
```

**Security level:** Very weak. A moderately skilled attacker can bypass everything in under an hour.

---

### Recommended Architecture (Target State)

```
┌─────────────────────────────────────────────────────────┐
│  WinUI 3 App (obfuscated + integrity-checked)           │
│  - .NET obfuscation (control flow, string encryption)   │
│  - Startup self-hash verification                       │
│  - Anti-debug detection (optional)                      │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ SQLCipher Database (AES-256 encrypted)            │  │
│  │ - Key derived from DPAPI + app entropy            │  │
│  │ - Credentials: Argon2id for PINs, PBKDF2-600K    │  │
│  │ - WAL files also encrypted                        │  │
│  │ - Restrictive file ACLs                           │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ DPAPI Vault (with app-specific entropy)           │  │
│  │ - DB encryption key                               │  │
│  │ - Device private key (ECDSA P-256)                │  │
│  │ - License certificate cache                       │  │
│  │ - Isolated from other same-user apps              │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ License Enforcement                               │  │
│  │ - Signed certificate validation (ECDSA P-256)     │  │
│  │ - 30-day offline validity                         │  │
│  │ - Hard clock-rollback enforcement                 │  │
│  │ - Grace period → degraded mode → lockout          │  │
│  │ - Feature gating per permission group             │  │
│  └───────────────────────────────────────────────────┘  │
│                                                         │
│  ┌───────────────────────────────────────────────────┐  │
│  │ Auth Hardening                                    │  │
│  │ - 5-attempt lockout (15 min)                      │  │
│  │ - 15-min idle session timeout                     │  │
│  │ - Forced password change on first run             │  │
│  │ - Auto-rehash legacy credentials                  │  │
│  └───────────────────────────────────────────────────┘  │
└──────────────────────┬──────────────────────────────────┘
                       │ HTTPS (certificate pinning)
                       │
┌──────────────────────▼──────────────────────────────────┐
│  Supabase Edge Function (hardened)                      │
│  - Rate limiting: 5 req/min per IP+installId            │
│  - HMAC-SHA256 for license key hashing                  │
│  - Mandatory request sequence (no null bypass)          │
│  - Permanent revocation (no self-reactivation)          │
│  - Serializable transaction for max_devices             │
│  - Request signing with device key                      │
└─────────────────────────────────────────────────────────┘
```

**Security level:** Strong. Cracking requires significant effort (binary patching + DB decryption + certificate forging). Cost of cracking exceeds cost of a license for 99% of users.

---

## TRADE-OFFS SUMMARY

| What You Gain | What It Costs |
|---------------|---------------|
| Encrypted database → data safe if device stolen | 5% slower DB operations, migration complexity |
| License enforcement → revenue protection | 2-3 days dev, startup latency, edge cases to handle |
| Rate limiting → blocks brute-force | Slight API latency, shared-IP edge cases |
| Session timeout → unattended terminal protection | Cashier friction, cart persistence needed |
| Account lockout → blocks PIN guessing | Legitimate lockouts, admin unlock needed |
| Obfuscation → harder to reverse-engineer | Debugging harder, possible AV false positives, cost |
| Certificate pinning → blocks MITM | Breaks on cert rotation, harder to debug network |
| Stronger hashing → credentials safe if DB stolen | 100-200ms slower login |

### The Fundamental Trade-off

**Security vs. Offline Usability:** Every security check that requires "phone home" breaks offline use. The 30-day signed certificate model is the right balance — verify online when possible, trust the cached certificate offline for up to 30 days.

**Security vs. Development Speed:** P0 and P1 items are essential and relatively quick. P2 items are important but can wait. P3 items are diminishing returns — only invest if you see actual piracy.

**Security vs. User Experience:** Lockouts, timeouts, and forced password changes annoy users. Make them configurable where possible (store owner sets timeout duration). Never sacrifice security for UX on critical paths (license validation, credential storage).

---

## DATABASE MIGRATION & UPGRADE SAFETY

### How Your Current System Works

Your migration system (`Class1.cs` / `DatabaseInitializer`) uses SQLite's `PRAGMA user_version` as a version counter. Each migration step has:
- A **version number** (1-19 currently)
- A **category** (`StartupSafe`, `HeavyBackfill`, `RebuildOrDestructive`)
- An **execute function** that runs the schema change

The flow:
1. App starts → calls `DatabaseInitializer.Initialize(dbPath)`
2. Creates tables if they don't exist (idempotent `CREATE TABLE IF NOT EXISTS`)
3. Runs `StartupSafe` migrations (fast, additive) during splash screen
4. Later, `RunMaintenanceMigrations()` runs heavier backfills
5. Each migration is wrapped in a transaction — if it fails, it rolls back

### What's Good About This System

- Forward-only migrations — version number only goes up
- Idempotent checks — `ColumnExists()` prevents double-applying
- Categorized execution — heavy work deferred to maintenance window
- Transaction isolation — failed migration doesn't corrupt the DB
- Integrity validation — `PRAGMA quick_check` and `foreign_key_check` before/after maintenance

### What's MISSING (Dangerous Gaps for Upgrades)

#### Gap 1: No Downgrade Prevention

**The problem:** If a user installs version 2.0 (which runs migration 19), then downgrades to version 1.3 (which only knows about migration 12), the older app will:
- See `user_version = 19` but only have migrations up to 12
- Skip all migrations (since `currentVersion > all known versions`)
- Try to query columns/tables that the old code doesn't know about
- OR the old code may crash on unexpected schema

**What can go wrong:**
- Old app reads a column that was renamed/removed in a later migration
- Old app writes data that violates constraints added by newer migrations
- Data corruption from schema mismatch between code and database
- User loses all their sales data, products, settings

**The fix:** Add a minimum-version check at startup. If the database is newer than what the app understands, refuse to open and tell the user to update.

#### Gap 2: No Schema Version Stored Separately

**The problem:** `PRAGMA user_version` is a single integer. You have no record of:
- Which app version created the database
- Which migrations were applied vs. skipped (due to category filtering)
- When the last migration ran
- What encryption version the DB uses (important for future SQLCipher)

**The fix:** Add a `schema_metadata` table that stores key-value pairs like `min_app_version`, `created_by_app_version`, `last_migration_at`, `encryption_version`.

#### Gap 3: No Encryption Migration Path Planned

**The problem:** When you add SQLCipher encryption later, existing users have UNENCRYPTED databases. You need to:
1. Detect unencrypted DB on startup
2. Create a new encrypted DB
3. Copy all data from old to new
4. Replace old file with new file
5. Never allow going back to unencrypted

**If you don't plan this now:** You'll either break existing users' data or have to support unencrypted DBs forever.

#### Gap 4: No Backup Before Destructive Migrations

**The problem:** `HeavyBackfill` and `RebuildOrDestructive` migrations modify data. If something goes wrong (power loss, disk full, bug in migration logic), the transaction rollback protects the schema, but WAL file corruption can still occur on power loss.

**The fix:** Before any heavy migration, copy the DB file as a backup. If migration succeeds, delete the backup. If it fails, the backup is the recovery path.

#### Gap 5: Credential Rehashing is Migration-Safe (Good News)

Your hash format is self-describing: `v2:10000:salt:hash`. When you increase iterations to 600K:
- New hashes: `v2:600000:salt:hash`
- Old hashes: still verified correctly because the iteration count is read from the stored hash
- Auto-rehash on login: transparent to user, no schema migration needed
- No downgrade risk: old app versions can still verify old hashes (they just can't create new strong ones)

---

## IMPLEMENTATION ROADMAP (REVISED FOR FREE APP → PAID PREPARATION)

**Context:** The app is currently FREE. No paid features exist yet. The goal is to secure the infrastructure NOW so the paid layer can be added safely later. This is the ideal time — no backward compatibility concerns with paid users.

### P0 — Critical (Do This Week, < 1 Hour Total)

Prevent catastrophic mistakes that would ruin the future paid system.

| # | Task | Time | Status |
|---|------|------|--------|
| 1 | ~~Generate new key pair securely~~ | ~~Done~~ | ✅ DONE |
| 2 | ~~Store private key in Supabase Secrets only~~ | ~~Done~~ | ✅ DONE |
| 3 | ~~Delete local private key from disk~~ | ~~Done~~ | ✅ DONE |
| 4 | ~~Update public key in repo~~ | ~~Done~~ | ✅ DONE |
| 5 | ~~Verify signing works end-to-end~~ | ~~Done~~ | ✅ DONE |
| 6 | ~~Fix `.gitignore`~~ | ~~Done~~ | ✅ DONE |
| 7 | ~~Pin LiveChartsCore version~~ | ~~Done~~ | ✅ DONE |
| 8 | ~~Add downgrade prevention~~ | ~~Done~~ | ✅ DONE |
| 9 | ~~Update `BACKEND_SIGNING_KEY_ID` to new UUID~~ | ~~Done~~ | ✅ DONE (2026-05-14) |
| 10 | ~~Delete `generate-keys-index-ts` from Supabase dashboard~~ | ~~Done~~ | ✅ DONE (2026-05-14) |

**Risk if skipped:** Private key leak = future licensing dead. No downgrade check = users corrupt their data by installing old versions after updating.

---

### P1 — Secure the Foundation (This Sprint, 1-2 Weeks)

Harden auth, protect user data, establish the patterns that paid features will build on.

| # | Task | Time | Status |
|---|------|------|--------|
| 5 | ~~Increase PBKDF2 to 600K + auto-rehash~~ | ~~Done~~ | ✅ DONE — 600K iterations, auto-rehash on login via `TryUpgradeCredentialHash` |
| 6 | ~~Add session timeout~~ | ~~Done~~ | ✅ DONE — 30-min default, configurable 1-480 min, DispatcherQueueTimer, locks on idle |
| 7 | ~~Add account lockout~~ | ~~Done~~ | ✅ DONE — shared per-user counter, 24h reset window, lock on every 5th failure with 1/3/5/10/15 min steps |
| 8 | ~~Auto-rehash legacy SHA-256 hashes~~ | ~~Done~~ | ✅ DONE — included in #5, `NeedsPbkdf2Rehash` triggers upgrade |
| 9 | ~~Force bootstrap password change~~ | ~~Done~~ | ✅ DONE — `must_change_password` flag, migration v21, shell/users-page enforcement, hint suppressed after change |
| 10 | ~~Add `[JsonIgnore]` to credential fields~~ | ~~Done~~ | ✅ DONE — `User.PasswordHash` and `User.PinHash` excluded from JSON serialization |
| 11 | ~~Add `schema_metadata` table~~ | ~~Done~~ | ✅ DONE — migration v22 adds `schema_metadata`; bootstrap now records `created_by_app_version`, `min_app_version`, `last_migration_at`, `last_migration_version`, `current_schema_version`, and `encryption_version` |
| 12 | ~~Add pre-migration backup for heavy migrations~~ | ~~Done~~ | ✅ DONE — maintenance path checkpoints and copies `pos.db` to a versioned `.bak` file before pending `HeavyBackfill`/`RebuildOrDestructive` migrations; backup path is recorded in `schema_metadata` |

**Risk if skipped:** Unattended terminals exploitable (lockout), default creds persist (bootstrap change), no safe upgrade path (schema_metadata).

---

### P2 — Prepare Backend Infrastructure for Paid (Next 2-4 Weeks)

Harden Supabase and local storage. Fix these NOW while no real paid users exist — no backward compatibility headaches.

| # | Task | Time | What to Do |
|---|------|------|------------|
| 13 | Add SQLCipher encryption compatibility mode | 1 day | Encrypt new databases by default with SQLCipher and DPAPI-derived keys. Keep readable legacy plaintext databases in compatibility mode for now, record `encryption_version = 0` for them, and defer automatic conversion until a supported migration path exists. |
| 14 | Add DPAPI entropy | 1 hour | Add app-specific entropy to Protect/Unprotect. Migrate existing secrets file. |
| 15 | Add rate limiting to edge function | 4 hours | Sliding window: 5 req/min per IP+installId. Protects backend BEFORE paid clients connect. |
| 16 | Switch to HMAC-SHA256 in edge function | 2 hours | Fix crypto now — no real license keys exist yet, so no migration needed. |
| 17 | Make revocation permanent | 1 hour | Fix now while no real activations exist — zero backward compatibility concern. |
| 18 | Make requestSequence mandatory | 30 min | Fix now — no old paid clients to break. |
| 19 | Fix max_devices race condition | 2 hours | Serializable transaction. Fix before real activations happen. |
| 20 | Add certificate pinning | 4 hours | Pin Supabase cert for telemetry and future license calls. |
| 21 | Sanitize telemetry stack traces | 2 hours | Strip sensitive data before sending. |
| 22 | Remove wildcard CORS | 30 min | No web clients need this. |
| 23 | Strengthen Supabase password policy | 5 min | Config change: min 8 chars, require letters+digits. |

**P2-13 status (2026-05-15):** SQLCipher integration is now running in compatibility mode. The data layer uses `Microsoft.Data.Sqlite.Core` with `SQLitePCLRaw.bundle_e_sqlcipher`, stores a 32-byte database key with Windows DPAPI plus app-specific entropy, and encrypts new databases by default. Existing readable plaintext databases are intentionally left plaintext for now and open through the legacy compatibility path; `schema_metadata.encryption_version` is recorded as `1` for encrypted databases and `0` for legacy plaintext databases. Automatic plaintext-to-encrypted migration through `ATTACH DATABASE ... KEY` + `sqlcipher_export()` was attempted and deferred because it is not reliable in the current provider stack. Future completion options are a logical-copy migration path inside the app or a move to the official Zetetic-supported SQLCipher stack before reattempting automatic conversion. Manual restore/build/runtime verification is still required because compilation is intentionally reserved for human execution in this workspace.

**P2 install-identity status (2026-05-17):** The earlier design assumption was a UUID-shaped `install_id` in the activation path. That exact approach was **not** what was implemented. The implemented path uses a hardware-derived `install_id` as the client-side canonical source, stores backend `install_id` as `text` instead of forcing `uuid`, migrates old saved IDs once on the client, and temporarily canonicalizes old IDs to the new one on the backend during rollout. The backend path was completed through the `install_id text` migration plus canonicalization/reconciliation logic, not by preserving UUID-only semantics.

**Next install-identity cleanup:** Keep the canonicalization layer only for the mixed-version rollout window. In the current implementation this means the old-to-new install-ID mapping and reconciliation path stays in place until production traffic shows that old IDs are no longer arriving for a sustained period. Only then remove alias resolution from the backend path and drop the temporary migration state.

**Why now is the perfect time:** Every one of these fixes is EASIER now than after paid users exist. No migration headaches, no backward compatibility, no "we need to support the old broken behavior" situations. Fix the foundation before building on it.

---

### P3 — Build Paid Features (When Ready to Launch Paid)

Only after P0-P2 are complete. The infrastructure is now secure and ready.

| # | Task | Time | What to Do |
|---|------|------|------------|
| 24 | Implement LicenseValidationService | 2-3 days | Offline certificate verification. Check signature, expiry, device thumbprint. Feature gating per permission group. |
| 25 | Implement grace period + degraded mode | 1 day | When certificate expires offline: 7-day grace with warnings, then feature lockout (not data lockout — free features still work). |
| 26 | Hard time validation enforcement | 2 hours | Clock rollback → require online verification. Block premium features if offline + rollback. |
| 27 | Binary obfuscation | 1 day | Only needed once there's something worth cracking. |
| 28 | Runtime integrity self-check | 4 hours | Detect binary patching that removes license checks. |
| 29 | Hardware-bound device keys (TPM/CNG) | 2-3 days | Stronger device binding for license certificates. |
| 30 | Restrict DB file ACLs | 2 hours | Prevent other users on shared POS from reading the database. |

**Why last:** These only matter when there's paid functionality to protect. Building them before the paid features exist is wasted effort that might need rework when you see the actual paid feature requirements.

---

## UPGRADE SAFETY RULES

When implementing any security change that modifies the database schema or data format, follow these rules to prevent breaking existing users on update:

### Rule 1: Never Remove or Rename Columns

SQLite doesn't support `DROP COLUMN` cleanly. Instead:
- Add new columns, deprecate old ones
- Keep old columns readable (even if unused)
- Old app versions can still read their expected columns

### Rule 2: New Migrations Must Be Additive

Every new migration version must:
- Only ADD columns, tables, or indexes
- Never DELETE data that old versions wrote
- Use `IF NOT EXISTS` / `ColumnExists()` checks (you already do this — good)

### Rule 3: Downgrade Prevention is Non-Negotiable

After adding the version check (P0 task #4), the rule is:
- App version X knows migrations 1 through N
- If DB has `user_version > N`, refuse to open
- Show clear message: "Please update to the latest version"
- NEVER silently ignore a newer database

### Rule 4: Encryption Migration is One-Way

Once a database is encrypted (P2 task #13):
- Store `encryption_version = 1` in `schema_metadata`
- Old unencrypted app versions cannot open it (they'll get "not a database" error)
- This is intentional — downgrading past the encryption boundary is not supported
- The downgrade prevention check (Rule 3) catches this before the confusing SQLite error

### Rule 5: Credential Format Changes Are Self-Describing

Your current hash format (`v2:iterations:salt:hash`) is already correct:
- New app creates hashes with 600K iterations
- Old app can still verify old 10K hashes (reads iteration count from stored value)
- If user downgrades, their rehashed credential still works (old app reads the format)
- Only NEW credential creation uses the new iteration count

### Rule 6: Always Backup Before Heavy Migrations

Before any `HeavyBackfill` or `RebuildOrDestructive` migration:
- Copy `pos.db` to `pos.db.pre-vN.bak`
- Run migration in transaction
- On success: delete backup (or keep for 7 days)
- On failure: rollback transaction, backup remains as recovery

---

## REALISTIC EXPECTATIONS

**Your situation is ideal for security work:**
- Free app with real users → you're protecting their data (good practice, builds trust)
- No paid features yet → no backward compatibility constraints on backend fixes
- Migration system exists → you have the infrastructure to make safe schema changes
- Supabase edge function has no real activations → you can break/fix the API freely

**What this architecture CANNOT prevent:**
- A determined attacker with physical access WILL eventually crack client-side protection
- .NET apps are inherently easier to decompile than native C++ apps
- If someone steals the unencrypted DB today, that data is already exposed

**What P0+P1+P2 achieves:**
- Protects current free users' data and credentials
- Makes the infrastructure ready for paid features with zero rework
- Establishes upgrade-safe patterns (downgrade prevention, schema metadata, backup)
- Fixes the Supabase backend while there are no real paid users to break
- When you're ready to add paid features, you just implement P3 on top of a solid foundation

**The key advantage of doing this NOW:**
Every fix in P2 (rate limiting, HMAC, revocation, race conditions) would require a migration plan if done AFTER paid users exist. Doing it now = zero migration cost, zero backward compatibility headaches, zero risk of breaking paying customers.
