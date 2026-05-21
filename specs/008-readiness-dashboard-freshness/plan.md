# Implementation Plan: Operational Readiness and Dashboard Freshness

**Branch**: `008-readiness-dashboard-freshness` *(feature docs; current git branch remains `006-product-pictures`)* | **Date**: 2026-05-02 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/008-readiness-dashboard-freshness/spec.md`

## Summary

This plan adds a fully automated, local-only readiness verification flow for the current WinUI/SQLite application and fixes dashboard freshness so each dashboard open cycle shows the last successful values immediately, then refreshes them in the background from current business data. The implementation stays inside the existing two-project solution, exercises real repositories and workflow paths against isolated local datasets instead of mocks, verifies the current user-accessible product CSV import/export surface, and emits structured pass/fail results without guided manual steps.

## Technical Context

**Language/Version**: C# on .NET 10.0 / WinUI 3 in the current repository baseline  
**Primary Dependencies**: Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, LiveChartsCore.SkiaSharpView.WinUI, CsvHelper, System.Security.Cryptography.ProtectedData, Windows.Storage.Pickers  
**Storage**: Local SQLite database under `AppDataPaths`; local JSON files for preferences/receipts; this feature adds local readiness artifacts and dashboard snapshot cache files under the same app-data root  
**Testing**: No dedicated automated test project exists; this feature will add a fully automated in-app readiness runner with machine-readable scenario results and deterministic local fixtures  
**Target Platform**: Windows 10+ desktop application, packaged and unpackaged local execution paths  
**Project Type**: Two-project desktop app (`Nexill.RetailStorePOS` WinUI front end + `RetailStorePOS.Data` class library)  
**Performance Goals**: Keep heavy readiness and reporting work off the UI thread; keep the dashboard reopen experience immediately usable with last-known values visible first and refreshed values replacing them within 5 seconds for the representative local dataset  
**Constraints**: Exactly two projects, local-first/offline-first SQLite operation, fully automated readiness with no guided manual steps, no live dashboard updates while a page instance remains open, no build/delete actions in this workflow, no destructive mutation of the operator's live database during readiness verification  
**Scale/Scope**: Existing app spans 18 WinUI XAML pages, 6 current view-model classes, checkout/products/tax/reporting workflows, one current user-accessible product CSV import/export flow, and representative local data volumes expected to scale beyond toy datasets

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Gate

**Gate Status**: PASS.

1. **Local-First Data Sovereignty**: PASS. Readiness and dashboard freshness stay fully local, use SQLite as the source of truth, and avoid any mandatory cloud dependency.
2. **Notion-Grade Visual Design**: PASS. The feature adds state handling and status surfaces, not a new design system or visual language.
3. **Non-Blocking, Concurrent UX**: PASS. Readiness orchestration and dashboard refresh work are planned as background operations with snapshot-first UI behavior.
4. **Two-Layer Architecture**: PASS. The design stays inside `RetailStorePOS.Data` and `Nexill.RetailStorePOS` with no extra projects.
5. **Keyboard-First Checkout**: PASS. Checkout verification runs against current workflow logic without changing the operator interaction model.
6. **Zero-Cloud Build Dependency**: PASS. The feature relies only on local tooling, local fixtures, and the local SQLite store.
7. **Versioned Isolation**: PASS. All feature artifacts remain inside `V/1.3.3`.
8. **Backward-Compatible Local Schema Evolution**: PASS. Any new persistence for readiness artifacts or dashboard snapshots is additive and local; the live business schema is not reset or replaced.

### Post-Design Re-Check

1. **Local-First Data Sovereignty**: PASS. The design uses isolated local readiness workspaces and local dashboard snapshot persistence only.
2. **Notion-Grade Visual Design**: PASS. Dashboard refresh states are limited to existing WinUI surfaces and do not introduce a new UI toolkit.
3. **Non-Blocking, Concurrent UX**: PASS. Readiness execution and dashboard refresh are explicitly background-driven, while immediate snapshot display avoids blank-screen waits.
4. **Two-Layer Architecture**: PASS. Data fixtures, scenario models, and repository-driven checks live in `RetailStorePOS.Data`; orchestration and page behavior live in `Nexill.RetailStorePOS`.
5. **Keyboard-First Checkout**: PASS. The plan verifies checkout flows through the existing local path and does not add mouse-only dependencies.
6. **Zero-Cloud Build Dependency**: PASS. No design element requires cloud build-time services or remote authentication.
7. **Versioned Isolation**: PASS. The plan assumes only the current version folder and local runtime assets.
8. **Backward-Compatible Local Schema Evolution**: PASS. The design prefers snapshot/report files under app data and keeps database interactions additive and testable.

## Project Structure

### Documentation (this feature)

```text
specs/008-readiness-dashboard-freshness/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── readiness-automation.md
│   ├── import-export-roundtrip.md
│   └── dashboard-refresh.md
└── tasks.md
```

### Source Code (repository root)

```text
E:\Projects\Retail_Store\V\1.3.3\
├── RetailStorePOS.Data\
│   ├── AppDataPaths.cs
│   ├── DatabasePaths.cs
│   ├── SqliteConnectionFactory.cs
│   ├── Class1.cs
│   └── Modules\
│       ├── Contracts\
│       ├── Products\
│       │   ├── ProductRepository.cs
│       │   ├── ProductImportService.cs
│       │   ├── ProductExportService.cs
│       │   └── ProductCsvRow.cs
│       └── Sales\
│           └── SaleRepository.cs
├── Nexill.RetailStorePOS\
│   ├── App.xaml.cs
│   ├── LoginRuntime.cs
│   ├── MainWindow.xaml.cs
│   ├── Services\
│   │   ├── AuthService.cs
│   │   ├── ILocalPreferencesService.cs
│   │   └── LocalPreferencesService.cs
│   ├── ViewModels\
│   │   └── CheckoutViewModel.cs
│   └── Views\
│       ├── ProductsPage.xaml.cs
│       ├── ReportsPage.xaml.cs
│       └── ReportsDashboardPage.xaml.cs
└── specs\
    └── 008-readiness-dashboard-freshness\
```

**Structure Decision**: Keep the constitution-mandated two-project solution. Put isolated readiness data models, fixture/workspace handling, and repository-driven verification support in `RetailStorePOS.Data`; keep readiness orchestration, report presentation, and dashboard-open refresh behavior in `Nexill.RetailStorePOS`. Reuse existing contracts, repository types, and local preferences patterns rather than adding assemblies or external harnesses.

## Complexity Tracking

No constitution violations or waivers are required for this plan.

---

## Phase 0: Outline & Research

### Research Findings (`research.md`)

1. Run readiness against isolated local workspaces and cloned datasets instead of the operator's live database.
2. Implement readiness as an in-process automation surface inside the existing two-project app, not as a separate test project or manual checklist.
3. Treat the current user-accessible import/export surface as the product CSV workflow exposed by `ProductsPage`.
4. Persist last successful dashboard values locally and reuse them as the immediate-on-open snapshot before running a new background refresh.
5. Emit structured scenario results with blocking vs non-blocking severity and machine-readable local artifacts.
6. Trigger dashboard freshness on page-open cycles only; do not turn the dashboard into a live-updating surface while the same page instance remains open.

## Phase 1: Design & Contracts

### Data Model (`data-model.md`)

The design introduces operational entities for readiness automation and dashboard freshness:

- `ReadinessRun`
- `VerificationScenario`
- `ScenarioResult`
- `DatasetProfile`
- `ImportExportSample`
- `DashboardSnapshot`
- `DashboardRefreshCycle`

### Interface Contracts (`contracts/`)

- `readiness-automation.md`: Defines scenario scope, dataset isolation, blocking semantics, and readiness result artifacts.
- `import-export-roundtrip.md`: Defines the current product CSV round-trip verification contract, including valid and invalid sample handling.
- `dashboard-refresh.md`: Defines snapshot-first dashboard open behavior, background refresh rules, and stale-state handling.

### Quickstart (`quickstart.md`)

Documents the automated readiness workflow, isolated dataset expectations, dashboard snapshot/refresh behavior, and the expected machine-readable outputs for the first implementation slice.

### Agent Context

Update the Codex agent context after the design artifacts are written so subsequent task generation inherits the current runtime baseline, local-storage patterns, and dashboard freshness requirements.

---

## Phase 2: Execution Plan

### Slice 0 - Readiness Foundation

1. Add scenario/result/dataset models and local artifact paths for readiness outputs.
2. Create isolated readiness workspaces that clone or seed local SQLite data without mutating the operator's active store database.
3. Define blocking vs non-blocking scenario semantics and dataset profiles for minimal and representative runs.

### Slice 1 - Core Workflow Readiness

1. Automate startup/runtime bootstrap verification, local database access checks, and login/auth validation.
2. Automate product/catalog lookup, checkout flow, sale persistence, and tax-calculation verification against real repositories.
3. Emit structured results per scenario with durations, failure summaries, and overall readiness status.

### Slice 2 - Import/Export Integrity

1. Automate the current product CSV export path from representative catalog data.
2. Automate valid CSV round-trip import verification, including supported image-path handling.
3. Automate malformed/invalid import failure scenarios and verify pre-existing data remains unchanged.

### Slice 3 - Dashboard Freshness on Open

1. Persist the last successful dashboard snapshot in local app data.
2. Rework dashboard open behavior so previously cached values can render immediately, then trigger a new background refresh for each open cycle.
3. Replace shown values with fresh values on success, and mark stale/refresh-failed state on failure without forcing live updates during the same page instance.

### Slice 4 - Integration Surface and Reporting

1. Expose the readiness run entry point and local report artifacts through the current app structure without adding a third project.
2. Integrate the dashboard refresh verification scenario into the readiness run, including reopen-after-data-change coverage.
3. Finalize deterministic result outputs for minimal and representative dataset profiles so `/speckit.tasks` can generate implementation work without ambiguity.
