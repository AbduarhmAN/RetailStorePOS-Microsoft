# Research Findings: Operational Readiness and Dashboard Freshness

## Decision 1: Run readiness against isolated local workspaces

- **Decision**: Execute readiness scenarios against cloned or seeded local SQLite workspaces and feature-specific local artifact folders instead of the operator's active runtime database.
- **Rationale**: The readiness pass must verify real reads, writes, checkout persistence, and import/export behavior, but it must not damage the currently active store dataset while doing so.
- **Alternatives considered**:
  - Run readiness directly against the live database. Rejected because write and failure-path scenarios could alter business data or leave test artifacts in the operator's store.
  - Use mocks instead of SQLite. Rejected because that would not verify the actual repositories, schema behavior, or local file interactions the app depends on.

## Decision 2: Keep readiness automation inside the existing two-project application

- **Decision**: Implement readiness as an in-process automation surface that reuses existing repositories, services, and WinUI entry points inside `RetailStorePOS.Data` and `Nexill.RetailStorePOS`.
- **Rationale**: The repository has no dedicated automated test project, and the clarified feature requires a fully automated readiness pass without turning this into a separate harness architecture.
- **Alternatives considered**:
  - Add a new test or automation project. Rejected because the constitution fixes the solution at two projects.
  - Use a manual checklist. Rejected because the clarified spec explicitly requires full automation.

## Decision 3: Treat the current import/export surface as the product CSV workflow

- **Decision**: Scope import/export verification to the currently user-accessible product CSV import/export flow exposed by `ProductsPage`, including optional thumbnail/image-path round-tripping where supported.
- **Rationale**: Code inspection shows the present desktop import/export surface is the product CSV workflow backed by `ProductImportService` and `ProductExportService`; planning broader undocumented flows would be fabricated scope.
- **Alternatives considered**:
  - Assume additional import/export workflows exist elsewhere. Rejected because the current repo does not expose more user-accessible desktop flows.
  - Limit verification to export only. Rejected because the spec requires round-trip integrity and malformed-import safety.

## Decision 4: Persist last successful dashboard values locally and refresh on every open

- **Decision**: Store the last successful dashboard snapshot in local app data, render it immediately when available, then start a new background refresh for the current open cycle and replace the snapshot with fresh values on success.
- **Rationale**: The current dashboard loads once per page instance and leaves stale values behind. Snapshot-first open behavior satisfies the clarified requirement without forcing a blank wait or introducing live updates.
- **Alternatives considered**:
  - Wait for fresh data before showing anything. Rejected because it conflicts with the requirement to show last values immediately when available.
  - Add live subscriptions while the page remains open. Rejected because the clarified spec explicitly says live updates are not required.

## Decision 5: Emit structured readiness results with blocking and non-blocking severity

- **Decision**: Model each readiness scenario with explicit blocking behavior and write machine-readable result artifacts that record status, duration, summary, and relevant dataset context.
- **Rationale**: The feature must distinguish readiness blockers from non-blocking observations and must produce explicit pass/fail/skipped results for each scenario.
- **Alternatives considered**:
  - Emit only one overall success/failure flag. Rejected because it would hide which workflow failed and would not meet the spec's per-scenario result requirement.
  - Log only text messages. Rejected because tasks and future automation need a deterministic structure.

## Decision 6: Use two dataset profiles: minimal and representative

- **Decision**: Define a minimal/near-empty dataset profile and a representative populated dataset profile, and require the readiness pass to run against both.
- **Rationale**: The spec and constitution both require realistic local verification. Minimal datasets catch empty-state regressions, while representative datasets catch scale-sensitive workflow and dashboard issues.
- **Alternatives considered**:
  - Test only with a small seed dataset. Rejected because it would miss scale-sensitive dashboard and aggregation behavior.
  - Test only with a populated dataset. Rejected because empty-state and first-run style failures would remain uncovered.

## Decision 7: Trigger dashboard freshness on page-open cycles, not background product events

- **Decision**: Tie dashboard refresh to page-open/navigation cycles and explicit refresh triggers, rather than subscribing the open dashboard to continuous product/sale events.
- **Rationale**: The requirement is freshness every time the dashboard is opened, not live mutation while a page instance stays open. This keeps runtime behavior predictable and reduces unnecessary background work.
- **Alternatives considered**:
  - Reuse `ProductsUpdated` to keep the dashboard live. Rejected because it overshoots the requested behavior and can blur the current-open-cycle semantics.
  - Keep the current one-load-per-instance behavior. Rejected because it leaves stale values after underlying data changes.
