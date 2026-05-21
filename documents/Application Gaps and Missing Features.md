# RetailStorePOS — What the Application is Missing

**Date:** 2026-05-18
**Scope:** Honest audit of features and capabilities that are absent or incomplete in version 2.0.0, based on reading the schema, source code, existing planning docs, and the current Advanced Analytics implementation work.

This is **not** a wish list of "nice to have" features. It lists things that are either:
- Missing entirely from the codebase
- Stubbed but non-functional
- Documented as needed but not implemented

Where the gap overlaps with an existing planning document (`Security Review and Roadmap.md`, `Future Features Roadmap for RetailStorePOS.md`, `Nexill Account System Design.md`, etc.), this document references those rather than duplicating their content.

---

## How to read this

Each item lists:
- **State** — Missing / Stubbed / Partial
- **Impact** — What you cannot do without it
- **Effort** — Rough sizing (S = day, M = week, L = month+, XL = months)
- **Cross-ref** — Link to the existing planning doc, if any

Items are grouped by area. Within each area, items are roughly ordered by importance.

---

## 1. Identity & Authentication

### 1.1 Online account / Supabase Auth ("Nexill Account")
- **State:** Missing
- **Impact:** Users cannot create an online account, sign in across devices, or recover their password without IT intervention. License system cannot tie a license to a user identity.
- **Effort:** L
- **Cross-ref:** `Nexill Account System Design.md` (full spec exists)

### 1.2 Bootstrap admin credentials still in the binary
- **State:** Partial. `MustChangePassword` flag is enforced, but `admin / 1234` is still hardcoded in `LoginRuntime.cs` constants.
- **Impact:** Anyone who decompiles the app sees the default. If the owner does not change it, the install is wide open.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` P1-9

### 1.3 PIN-as-password fallback for admin
- **State:** Bug
- **Impact:** Admin accounts created without a password are authenticated through the password box using a 4-digit PIN. Lower-than-expected admin assurance.
- **Effort:** S
- **Cross-ref:** `Security and Reliability Audit of RetailStorePOS-Microsoft.md`

### 1.4 Generic auth error messages
- **State:** Missing. Current code says "User not found" vs. "Invalid password" — leaks whether a username exists.
- **Impact:** Easier targeted attacks.
- **Effort:** S

### 1.5 Idle auto-lock
- **State:** Partial. Session timeout exists (30 min default) but the screen is not locked on idle — `Lock()` and `UnlockWithPin()` exist but are not wired to inactivity detection.
- **Impact:** Unattended terminals exploitable.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` P1

### 1.6 Argon2id for PINs
- **State:** Missing. PBKDF2 only.
- **Impact:** A 4-digit PIN keyspace is brute-forceable in seconds if the DB leaks. Argon2id is memory-hard and resists GPU attacks.
- **Effort:** M (NuGet package + migration)
- **Cross-ref:** `Security Review and Roadmap.md`

### 1.7 TPM/CNG-backed key storage
- **State:** Missing. DPAPI only.
- **Impact:** Software-only key protection is weaker against malware and reverse engineering. Acceptable for v1; required for a paid tier on managed enterprise endpoints.
- **Effort:** L
- **Cross-ref:** `Lightweight Hierarchical Verification for RetailStorePOS-Microsoft V2.md`

---

## 2. Licensing & Activation

### 2.1 App-side license activation pipeline
- **State:** Missing. Two empty stub files exist (`PublicLicenseKey.cs`, `LicenseValidationService.cs`, both 0 bytes).
- **Impact:** No way to activate a license, no certificate validation, no feature gating tied to a real license. The Edge Function `license-api` exists on the backend but the app cannot call it.
- **Effort:** L
- **Cross-ref:** `device_certification_protocol_and_versioning_spec.md`, `device_certification start part.md`, `all Encryption needed.md`

### 2.2 `FeatureAccessService` skeleton-only (developer override)
- **State:** Stubbed (added by us). Returns `true` for `AdvancedReports` based on a developer override; does not consult any signed certificate.
- **Impact:** Once licensing lands, every paid feature is allowed. Acceptable for pre-release; must flip to certificate-backed before public release.
- **Effort:** Becomes part of 2.1.

### 2.3 Edge Function hardening
- **State:** Missing/Partial.
- **Impact:** No rate limiting (5 req/min/IP), HMAC-SHA256 instead of plain SHA-256, mandatory `requestSequence`, permanent revocation, `max_devices` race fix, removal of wildcard CORS — all per the security roadmap.
- **Effort:** M
- **Cross-ref:** `Security Review and Roadmap.md` P2 #15-22

### 2.4 Certificate revocation
- **State:** Missing
- **Impact:** A revoked activation can come back if the certificate refreshes before the local cache notices. `revoked_at` is not yet a terminal state.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` P2 #17

### 2.5 `app_versions.version_code`
- **State:** Missing. Only `version_string` exists, which is text-sorted.
- **Impact:** `"2.10.0"` sorts before `"2.9.0"` lexicographically. Mandatory-update gating built on text comparison breaks at version 10.
- **Effort:** S (additive column)

---

## 3. Reliability & Data Integrity

### 3.1 DB backup / repair / rollback path on startup
- **State:** Missing
- **Impact:** If `pos.db` is corrupted (power loss mid-write, antivirus quarantine, disk full), the app cannot boot. Store outage during business hours.
- **Effort:** M
- **Cross-ref:** `reliability fix as a security one because the database is the app's operational root.md`, `Security Review and Roadmap.md` P0

### 3.2 Pre-migration backup for heavy migrations
- **State:** Missing
- **Impact:** A failing schema migration on a real DB can leave the database in a half-migrated state with no rollback artifact.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` P1 #12

### 3.3 `item_cost_cents` added at runtime by `SaleRepository`
- **State:** Working but ugly. The column is added on first sale via `ALTER TABLE` in `SaleRepository.CreateSale`, not via the migration system.
- **Impact:** Race risk if multiple code paths hit the DB before the column exists. Fragile pattern.
- **Effort:** S (formalize as a migration step)

### 3.4 Audit log silent failure
- **State:** Bug
- **Impact:** `AuditLogService` writes can fail (DB locked, disk full, schema mismatch) and the app keeps running with no visible signal. Breaks every other security control because actions cannot be reconstructed after the fact.
- **Effort:** S
- **Cross-ref:** `Security and Reliability Audit of RetailStorePOS-Microsoft.md`

### 3.5 No clock-rollback hard block
- **State:** Soft warning only. `TimeValidationService` shows a dismissible dialog.
- **Impact:** Clock manipulation is the simplest way to extend an expired license or trial period offline.
- **Effort:** M (need tolerance window, online-refresh fallback, UX)
- **Cross-ref:** `Security Review and Roadmap.md` #21, `Lightweight Hierarchical Verification for RetailStorePOS-Microsoft V2.md`

### 3.6 No DB ACLs
- **State:** Missing. Database directory inherits parent permissions.
- **Impact:** On a shared Windows machine (common in retail), other user accounts can read `pos.db` directly.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` #23

### 3.7 SQLCipher encryption
- **State:** Done. `DatabaseEncryptionService` + `SQLitePCLRaw.bundle_e_sqlcipher` are in place.
- **Note:** Listed here for completeness — this one is built. The PowerShell mock-data scripts predate it and only worked on plaintext DBs; the new .NET tool replaces them.

---

## 4. Inventory & Cost Tracking

### 4.1 No supplier / vendor system
- **State:** Missing entirely. No `suppliers` table, no product-to-supplier link, no purchase orders.
- **Impact:** Cost is a single number per product. Cannot track which supplier it came from, compare suppliers, or see cost history when a supplier changes prices.
- **Effort:** L

### 4.2 No purchase order / receiving / batch costing
- **State:** Missing. No PO table, no GRN (goods received note), no per-batch cost. The current `cost_price_cents` field is the latest known cost, period.
- **Impact:** Cannot do FIFO/LIFO accounting. Cost analytics is approximate.
- **Effort:** XL

### 4.3 No inventory turnover / dead stock reports
- **State:** Missing. Stock levels exist, last-sale date exists, but no aggregated turnover analysis.
- **Impact:** Cannot identify slow movers, stale inventory, or reorder suggestions.
- **Effort:** M (Page 2 of advanced analytics — Product Performance — addresses some of this)

### 4.4 No expense tracking
- **State:** Missing. No `expenses` table, no operating-cost categories, no rent/payroll/utilities tracking.
- **Impact:** "Profit" on the dashboard is **gross margin only** (price − COGS). Net profit after running costs cannot be computed inside the app.
- **Effort:** L

### 4.5 No multi-warehouse / multi-store
- **State:** Partial. `quantity_store` and `quantity_warehouse` columns exist (dual-zone) but no concept of multiple stores.
- **Impact:** A chain with 3 locations cannot run separate registers and roll up centrally.
- **Effort:** XL

### 4.6 No customer / loyalty system
- **State:** Missing. No `customers` table.
- **Impact:** Cannot track repeat customers, build a loyalty program, or do "customer of the month" analytics.
- **Effort:** L

---

## 5. Sales & Checkout Features

### 5.1 No basket-level discount flow
- **State:** The `sale_items` schema supports a synthetic discount line (`product_id = 0` with negative `total_cents`), but the checkout UI does not have a "discount the whole basket" button.
- **Impact:** Discounts can only be applied per-line via price override, not as "$5 off the cart" or "10% off everything."
- **Effort:** M

### 5.2 No refund / return UI
- **State:** Schema has `original_sale_item_id` for refund linkage, but UI is missing.
- **Impact:** Returns must be done by manually entering negative quantities. Audit trail is incomplete.
- **Effort:** M

### 5.3 No void / cancel transaction
- **State:** Missing. Once a sale is committed, it stays.
- **Impact:** Cashier mistakes propagate into reports. Common in retail; usually backed by a manager-approved void.
- **Effort:** M

### 5.4 No tip / gratuity handling
- **State:** Missing
- **Impact:** Stores in service-style retail (cafes, salons) cannot capture tips at checkout.
- **Effort:** S

### 5.5 No payment integrations
- **State:** Missing. Payment types are strings ("Cash", "Card", "Check", etc.); there is no terminal integration, no card-reader SDK, no Stripe/Square device support.
- **Impact:** Card payments must be processed externally and the cashier marks "Card" by hand. No reconciliation between the POS and the payment processor.
- **Effort:** XL per processor

### 5.6 No barcode generation / label printing
- **State:** Missing. Products have a `barcode` field for input, but the app cannot generate barcodes or print shelf labels.
- **Impact:** New products imported without barcodes have to be assigned them manually.
- **Effort:** M

---

## 6. Reporting & Analytics

### 6.1 Advanced Analytics Pages 2 and 3 not built
- **State:** Page 1 (Revenue Dashboard) is in. Pages 2 (Product Performance) and 3 (Operations / Cashier) are not started.
- **Impact:** Pro tier is currently a single page.
- **Effort:** M each
- **Cross-ref:** Conversation plan (this session)

### 6.2 No drill-down on Top Products
- **State:** Missing. Clicking a top product row does nothing.
- **Impact:** Cannot click into a product to see its receipts, daily trend, or stock history.
- **Effort:** S

### 6.3 No CSV / Excel export from analytics
- **State:** Partial. Existing reports may export receipts; advanced analytics has no export.
- **Impact:** Cannot share the dashboard data outside the app.
- **Effort:** S (CsvHelper is already a dep)

### 6.4 No PDF export beyond X-Reports
- **State:** Partial. X-Report PDF generation exists; analytics dashboards do not have PDF.
- **Effort:** M

### 6.5 No basket / cross-sell analysis
- **State:** Missing. The "what sells with what" report is mentioned in `Premium Advanced Reports for Retail POS Revenue Intelligence.md` but not built.
- **Impact:** Cannot see purchase patterns ("customers who buy bread also buy butter").
- **Effort:** L

### 6.6 No category-level analytics
- **State:** Missing. Products are flat — no category/department field.
- **Impact:** Cannot do "Beverages vs. Snacks" margin comparisons.
- **Effort:** M (depends on adding a category column first)

### 6.7 Localization on advanced reports
- **State:** Missing. The new Revenue Dashboard is hardcoded English. ar-SA RTL is not supported.
- **Impact:** Arabic users see English strings + LTR layout on the Pro page.
- **Effort:** S (resource keys + `FlowDirection` binding)

### 6.8 Loss-color binding on analytics
- **State:** Bug. Top Products' green "+ profit" text stays green even when profit is negative.
- **Impact:** A product losing money looks like a winner.
- **Effort:** S

### 6.9 Currency / date formatting on chart axes
- **State:** Bug. Y-axis shows raw numbers (`1500`); X-axis shows ISO dates (`2026-05-12`).
- **Effort:** S

### 6.10 No skeleton loading on advanced reports
- **State:** Partial. Free dashboard has skeleton shimmer; the new Pro page does not.
- **Impact:** First open shows `—` placeholders briefly, looks unpolished.
- **Effort:** S

---

## 7. UX & Polish

### 7.1 Theme switching
- **State:** Settings expose dark/light mode preference but the new Pro page hardcodes light colors.
- **Impact:** Dark-mode users see a white dashboard.
- **Effort:** S per page

### 7.2 Accessibility
- **State:** Missing on advanced reports. No `AutomationProperties.Name` on KPI tiles or chart.
- **Impact:** Screen readers cannot label the data.
- **Effort:** S

### 7.3 No "what's new" on update
- **State:** Missing.
- **Impact:** Users do not see release notes after upgrade.
- **Effort:** S

### 7.4 No first-run tutorial
- **State:** `IsFirstRunTutorialCleared` flag exists in settings but no tutorial UI is wired.
- **Impact:** New users have no onboarding.
- **Effort:** M

---

## 8. Cloud / Multi-Device

### 8.1 No cloud backup
- **State:** Missing. Local SQLite only.
- **Impact:** Drive failure = total data loss. No off-site backup strategy.
- **Effort:** L
- **Cross-ref:** `Future Features Roadmap for RetailStorePOS.md`

### 8.2 No multi-device sync
- **State:** Missing. Each install is an island.
- **Impact:** Two terminals in the same store cannot share inventory or sales history live.
- **Effort:** XL

### 8.3 No reporting export to cloud / email
- **State:** Missing.
- **Impact:** Cannot email the daily X-Report to the owner automatically.
- **Effort:** M

---

## 9. Developer & Operational Tooling

### 9.1 No automated tests for the reporting module
- **State:** Missing. Only `Tests/DeviceIdentityTests` and `Tests/MigrationTests` exist.
- **Impact:** Regressions in analytics queries are not caught by CI.
- **Effort:** M

### 9.2 No structured logging
- **State:** `StartupTrace` is a synchronous file write. Verification doc explicitly flags this.
- **Impact:** UI stalls on cold start under disk pressure. No log levels, no rotation, no queryable structure.
- **Effort:** M

### 9.3 Telemetry sends full stack traces
- **State:** Bug. `TelemetryService.cs` sends `error.ToString()` which can include file paths and embedded credentials.
- **Impact:** Sensitive data leaks to backend.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` #22

### 9.4 No service-layer authorization
- **State:** Missing. Permission checks live in UI (visibility, button enabled state). Service/command layer has no `deny-by-default`.
- **Impact:** Anyone who patches the binary or calls services directly bypasses every check.
- **Effort:** M
- **Cross-ref:** `Security and Reliability Audit of RetailStorePOS-Microsoft.md`

### 9.5 Dormant XAML breakage
- **State:** WinUI's XAML compiler can cache and skip re-validation. The `StoreManagementPage` errors caught today were latent — `ViewModel.StoreManagement_*` properties referenced in XAML but never defined in the C# class.
- **Impact:** Clean rebuilds expose a flood of errors at the worst time.
- **Effort:** S per occurrence — but worth a one-time sweep.

### 9.6 `[JsonIgnore]` not on credential fields
- **State:** Bug. User credential fields (password hash, PIN hash) are not annotated.
- **Impact:** Any code path that serializes a user object (telemetry, debug dumps, exports) can leak hashes.
- **Effort:** S
- **Cross-ref:** `Security Review and Roadmap.md` #10

---

## 10. Things the schema has but the app does not yet use

These exist in the database but no code currently reads or writes them:

| Field / table | Status | Notes |
|---|---|---|
| `products.purchased_at` | Wired only on import; never displayed | Could feed inventory aging report |
| `products.cashier_name` | Set on creation; never displayed | Useful in audit trail |
| `users.can_close_day` | Migration added it; no code reads it | Was supposed to gate end-of-day actions |
| `tax_groups.is_auto_managed` | Added; no consumer | Reserved for future auto-tax features |
| `installation_events` | Schema present; no code logs anything important to it | Was meant for licensing security events |
| `error_logs` | Same as above |

---

## What I have NOT verified

Honest about my knowledge limits:

- **Existing X-Report PDFs** in `Documents/Nexill Reports/` — I did not extract the text. They may show real daily volumes that would refine the mock-data target ranges.
- **Other generation scripts** in `scripts/` — read `Generate-MockSales.ps1` and `Nexill-DataForge.py` only. The other 30+ scripts (image downloaders, telemetry analytics, tax seeders, kaggle importers) are catalogued by name but not by behavior.
- **The complete `users` table schema** — I checked enough to know `display_name`, `username`, `is_active`, `must_change_password`, `can_override_price`, `can_close_day` exist. There may be more fields.
- **Tax rule application precedence** — read enough of `CheckoutCartItem.CalculateTaxBreakdown` to understand inclusive vs. exclusive but did not exhaustively map all `calc_type` paths (PERCENTAGE_ON_MARGIN, FIXED_AMOUNT, REVERSE_CHARGE, PER_UNIT_MEASURE).
- **MainWindow lifecycle** — confirmed `MainWindow_Loaded` runs `LoginRuntime.Initialize` off the UI thread, but did not trace every code path through it.

---

## Suggested priority for 2.0.0 release

The minimum to ship publicly:

1. **2.1, 2.3** — Core licensing pipeline + Edge Function hardening
2. **1.1, 1.2** — Nexill Account login + force admin/1234 change
3. **3.1, 3.4** — DB backup/repair + audit log health
4. **9.4** — Service-layer authorization (cheap, high ROI)
5. **6.7, 6.8, 6.9** — Quick polish on the Pro dashboard

Items 4.x (suppliers, expenses), 5.x beyond basics, 8.x (cloud), and 6.5/6.6 (basket/category analytics) are correctly deferred to future releases. They are not blockers.

---

## Summary count

- **Listed:** 60+ items
- **Critical for 2.0.0:** ~15
- **Polish for 2.0.0:** ~10
- **Future releases:** ~35

The app's foundation is in good shape. The visible feature surface (POS, products, basic reports, X-Reports) is solid. Most gaps are in the **supporting layers** — backup, license, identity, analytics depth — not in the core selling experience.
