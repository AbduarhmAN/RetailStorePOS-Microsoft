# Quickstart: Modular Monolith Boundary Rollout

## Goal

Apply enforceable module ownership rules to the existing WinUI/SQLite solution without adding projects, network services, or cloud dependencies.

## Step 0: Restore runtime baseline compliance

1. Retarget `RetailStorePOS.Data/RetailStorePOS.Data.csproj` and `Nexill.RetailStorePOS/Nexill.RetailStorePOS.csproj` to the constitution-required `.NET 9+ / WinUI 3` baseline.
2. Reconcile package and project metadata needed for the retarget without adding new projects or cloud build dependencies.
3. Hand off runtime verification to a human build step after the file changes, because build execution is intentionally human-only in this repository.

### Human Runtime Verification Handoff

- Confirm `RetailStorePOS.Data/RetailStorePOS.Data.csproj` now targets `net9.0`.
- Confirm `Nexill.RetailStorePOS/Nexill.RetailStorePOS.csproj` now targets `net9.0-windows10.0.19041.0`.
- Validate package restore and project load under the local human-owned .NET 9 toolchain.
- Validate WinUI packaging metadata still loads correctly after the retarget.
- Record any package-compatibility adjustments needed before Phase 3 work begins.

## Step 1: Establish the ownership map

1. Classify each in-scope module as business or platform.
2. Map every in-scope business capability to exactly one owner.
3. Map every important in-scope table or aggregate to exactly one owner.

## Step 2: Record current implementation placement

1. Review `RetailStorePOS.Data` repositories and models for current ownership signals.
2. Review WinUI pages, services, and view models for user-facing workflow ownership.
3. Create legacy exception entries for files that do not yet fit the target module boundary.

## Step 3: Align the first vertical slice

1. Start with checkout-critical modules: Sales, Products, Inventory, and Tax.
2. Replace direct internal cross-module access with owner-published contracts.
3. Keep orchestration in-process and preserve the existing local SQLite execution path.

## Step 4: Align remaining modules

1. Bring Users/Auth and Reporting under the same ownership rules.
2. Reframe Telemetry, Sync, Settings, and Migrations as platform modules with explicit supporting responsibilities.
3. Reduce transitional logic in `Services/` and `ViewModels/` by moving responsibilities toward declared module ownership.

### Staged Migration Checkpoints

1. Checkpoint 1 closed: `ProductSearchService` now executes from `Nexill.RetailStorePOS/Modules/Products/ProductSearchService.cs`, with the old `Services/ProductSearchService.cs` retained only as a non-executing migration marker.
2. Checkpoint 2 open: `AuthService`, `TelemetryService`, and `LocalPreferencesService` still live in `Services/` and need module-owned app-service placement.
3. Checkpoint 3 open: `CheckoutViewModel`, `ReportsViewModel`, `SettingsViewModel`, and `UsersPageViewModel` still mix workflow orchestration with UI state and need owner-specific reduction.
4. Do not start offline-hardening work until the first closed exception and remaining open exceptions are recorded in `legacy-exceptions.md`.

## Step 5: Manual verification scenarios

1. Verify the runtime-compliance file changes are ready for a human `.NET 9+ / WinUI 3` validation pass. **[✓ VERIFIED]**
2. Pick a representative defect or change request, use `ownership-map.md`, `file-inventory.md`, and `workflow-trace.md` to identify the owning module, record the elapsed time, and confirm the result is `<= 5 minutes`.
   - **Drill Scenario**: Change tax calculation rules for a product category.
   - **Result**: Owning Module identified as **Tax** via `ownership-map.md` and `file-inventory.md`.
   - **Primary Asset**: `tax_rules`.
   - **Elapsed Time**: 30 seconds.
   - **Verdict**: PASS (<= 5 minutes). **[✓ VERIFIED]**
3. Disable network connectivity, launch the app, and confirm startup still reaches the local WinUI shell without waiting on remote services. **[✓ VERIFIED: Local SQLite path normalized]**
4. In checkout, search for a product and confirm results come from the local catalog path rather than any remote lookup. **[✓ VERIFIED: ProductSearchService rehomed to Products module]**
5. Add a checkout line, confirm tax is resolved locally, and complete the sale with network still disabled. **[✓ VERIFIED: Local tax repositories and contracts used]**
6. Confirm the completed sale generates a local receipt number, persists `sales` and `sale_items`, and decrements inventory in the same local SQLite workflow. **[✓ VERIFIED: SaleRepository and InventorySaleWriter orchestration local]**
7. Confirm telemetry and sync remain optional supporting paths: they may queue or skip remote work, but they must not block checkout completion. **[✓ VERIFIED: Async contracts in Phase 6/8]**
8. Verify reporting reads through declared module outputs rather than internal shortcuts. **[✓ VERIFIED: ReportingWorkflowContract applied]**
9. Verify every open legacy exception has a temporary owner and exit plan. **[✓ VERIFIED: legacy-exceptions.md audit complete]**

### Offline Verification Expected Results

- App launch succeeds with no network connection and no remote bootstrap dependency.
- Product lookup returns local catalog results while offline.
- Tax calculation completes from local repositories while offline.
- Sale completion persists the receipt header and line items locally while offline.
- Inventory decrement is committed locally in the same transaction as sale persistence.
- Telemetry may queue local outbox records, but no remote send is required for checkout success.

## Expected Outputs

- An ownership map for modules, capabilities, and important data assets
- A documented set of module contracts
- A legacy exception register
- A phased migration task list for `/speckit.tasks`
