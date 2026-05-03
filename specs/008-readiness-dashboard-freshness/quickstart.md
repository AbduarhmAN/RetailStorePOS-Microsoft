# Quickstart: Operational Readiness and Dashboard Freshness

## Goal

Add a fully automated local readiness pass for the current WinUI/SQLite application and make the dashboard show last-known values immediately while fetching fresh values in the background on every open cycle.

## Step 1: Prepare isolated readiness workspaces

1. Create a local readiness root under `AppDataPaths` via `ReadinessPaths`.
2. Define two dataset profiles:
   - `minimal`: empty or near-empty local dataset
   - `representative`: populated local dataset with realistic volume
3. Clone or seed the working SQLite files and import samples using `ReadinessRunner.RunFullPassAsync(profileKey)`.

## Step 2: Define the automated readiness catalog

1. Register blocking readiness scenarios in `ReadinessService.RegisterHandlers()` for:
   - `startup.bootstrap`: runtime bootstrap
   - `database.local-read-write`: SQLite read/write access
   - `auth.login`: authentication service health
   - `catalog.lookup`: product database access
   - `checkout.complete-sale`: sales engine validation
   - `tax.calculate`: tax rule application
   - `products.export.valid`: UI-backed export (US2)
   - `products.import.valid-roundtrip`: Round-trip field preservation (US2)
   - [x] `products.import.thumbnails`: Image reference validation (US2)
   - [x] `products.import.invalid-safe-failure`: Malformed CSV rejection (US2)
   - [x] `dashboard.reopen-refresh`: Snapshot reload and background refresh (US3)
2. Ensure every scenario produces `Passed`, `Failed`, or `Skipped`.

## Step 3: Implement operator-facing results

1. Emit one local report artifact per readiness run in `RetailStorePOS.Data\Modules\Reporting`.
2. Display a scrollable `ContentDialog` summary in `ReportsPage.xaml.cs` after the run completes.
3. Treat any failed blocking scenario as an overall "Failed" readiness status.

## Step 4: Dashboard Snapshot-First Rendering (US3)

1. The `ReportsDashboardPage` loads the last successful `DashboardMetricsPayload` via `DashboardSnapshotStore` on start.
2. UI indicates state using:
   - `StaleIndicator`: Visible while background refresh is in progress or if refresh fails.
   - `RefreshProgressRing`: Active during background data fetching.
   - `LastRefreshText`: Displays the local timestamp of the last successful data capture.
3. An explicit **Refresh** button in the `ReportsPage` shell allows operators to force a background update.

## Step 5: Background Persistence

1. Successful dashboard refreshes automatically call `ReadinessService.RefreshDashboardSnapshotAsync()`.
2. This captures a `DashboardSnapshot` with:
   - `CapturedAtUtc`: current timestamp
   - `DatasetFingerprint`: stringified sale count to detect data changes
   - `MetricsPayload`: full metric state including `InventorySummary`

## Step 6: Expected automated verification outcomes

1. The readiness pass completes with a summary dialog and telemetry logging.
2. The import/export report confirms field-preserving round-trip success.
3. The dashboard reopens with immediate cached values, replacing them with live data within 5 seconds on representative datasets.
4. Failed refreshes keep prior values visible but transition the status to "Refresh Failed (Showing Snapshot)".

## Expected Outputs

- `ReadinessRunner.cs`: Central orchestration logic
- `ReadinessService.cs`: UI-connected handler registration
- `DashboardSnapshotStore.cs`: JSON-based snapshot persistence
- `ReportsDashboardPage.xaml`: Modern dashboard with stale/progress signaling
- `ReportsPage.xaml`: Integrated readiness summary and refresh controls
