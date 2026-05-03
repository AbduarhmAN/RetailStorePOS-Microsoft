# Tasks: Operational Readiness and Dashboard Freshness

**Input**: Design documents from `/specs/008-readiness-dashboard-freshness/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: No separate TDD or new automated-test-project tasks are included because the feature specification does not request test-first development. The feature itself delivers automated readiness verification through in-app scenario execution and local result artifacts.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently once foundational work is complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (`US1`, `US2`, `US3`)
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the shared readiness/reporting scaffolding that all stories will use.

- [X] T001 Create the readiness artifact path helper in `RetailStorePOS.Data/Modules/Reporting/ReadinessPaths.cs`
- [X] T002 [P] Create the readiness scenario key catalog in `RetailStorePOS.Data/Modules/Reporting/ReadinessScenarioKeys.cs`
- [X] T003 [P] Create the import/export sample catalog scaffold in `RetailStorePOS.Data/Modules/Reporting/ImportExportSampleCatalog.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the data models, local workspace management, and persistence primitives that block all user stories.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T004 Create the readiness run and scenario result models in `RetailStorePOS.Data/Modules/Reporting/ReadinessRun.cs` and `RetailStorePOS.Data/Modules/Reporting/ScenarioResult.cs`
- [X] T005 [P] Create the verification scenario model and blocking-severity definitions in `RetailStorePOS.Data/Modules/Reporting/VerificationScenario.cs`
- [X] T006 [P] Create the dataset profile and import/export sample models in `RetailStorePOS.Data/Modules/Reporting/DatasetProfile.cs` and `RetailStorePOS.Data/Modules/Reporting/ImportExportSample.cs`
- [X] T007 [P] Create the dashboard snapshot and refresh-cycle models in `RetailStorePOS.Data/Modules/Reporting/DashboardSnapshot.cs` and `RetailStorePOS.Data/Modules/Reporting/DashboardRefreshCycle.cs`
- [X] T008 Create isolated readiness workspace management in `RetailStorePOS.Data/Modules/Reporting/ReadinessWorkspaceManager.cs` and `RetailStorePOS.Data/AppDataPaths.cs`
- [X] T009 [P] Create machine-readable readiness report persistence in `RetailStorePOS.Data/Modules/Reporting/ReadinessReportStore.cs`
- [X] T010 [P] Create local dashboard snapshot persistence in `RetailStorePOS.Data/Modules/Reporting/DashboardSnapshotStore.cs`
- [X] T011 Create readiness scenario catalog and dataset-profile registration in `RetailStorePOS.Data/Modules/Reporting/ReadinessScenarioCatalog.cs`
- [X] T012 [P] Extend reporting workflow contracts for readiness and snapshot files in `RetailStorePOS.Data/Modules/Contracts/ReportingWorkflowContract.cs`
- [X] T013 Create the core readiness runner shell in `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`

**Checkpoint**: Foundation is ready. User stories can now begin in dependency order.

---

## Phase 3: User Story 1 - Verify Core Application Readiness (Priority: P1)

**Goal**: Deliver a fully automated readiness pass for startup, auth, local data access, catalog lookup, checkout, tax calculation, and explicit per-scenario reporting.

**Independent Test**: Run the readiness pass against the representative dataset and confirm it produces explicit automated results for startup, login/auth, local database access, product/catalog lookup, checkout/sales persistence, tax calculation, and overall readiness status with no guided manual steps.

- [X] T014 [US1] Implement in-app readiness orchestration in `Nexill.RetailStorePOS/Modules/Reporting/ReadinessService.cs`
- [X] T015 [P] [US1] Implement startup and local database readiness scenarios in `Nexill.RetailStorePOS/LoginRuntime.cs`, `Nexill.RetailStorePOS/MainWindow.xaml.cs`, and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [X] T016 [P] [US1] Implement the login/auth readiness scenario in `Nexill.RetailStorePOS/Services/AuthService.cs` and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [X] T017 [P] [US1] Implement catalog lookup and local data-access readiness scenarios in `Nexill.RetailStorePOS/Modules/Products/ProductSearchService.cs`, `RetailStorePOS.Data/Modules/Products/ProductRepository.cs`, and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [X] T018 [P] [US1] Implement the tax-calculation readiness scenario in `RetailStorePOS.Data/Modules/Tax/TaxCategoryRepository.cs`, `RetailStorePOS.Data/Modules/Tax/TaxRuleRepository.cs`, and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [X] T019 [US1] Implement the checkout and sale-persistence readiness scenario in `Nexill.RetailStorePOS/ViewModels/CheckoutViewModel.cs`, `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs`, and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [X] T020 [US1] Persist per-scenario results and overall readiness status in `RetailStorePOS.Data/Modules/Reporting/ReadinessReportStore.cs` and `Nexill.RetailStorePOS/Modules/Reporting/ReadinessService.cs`
- [X] T021 [US1] Surface the readiness run entry point and summary results in `Nexill.RetailStorePOS/Views/ReportsPage.xaml` and `Nexill.RetailStorePOS/Views/ReportsPage.xaml.cs`

**Checkpoint**: The application can execute a fully automated core readiness pass and report explicit blocking vs non-blocking outcomes.

---

## Phase 4: User Story 2 - Verify Import and Export Integrity (Priority: P1)

**Goal**: Prove the current user-accessible product CSV import/export workflow preserves in-scope data on valid round trips and fails safely on malformed input.

**Independent Test**: Run the readiness pass against representative product samples, confirm export plus valid re-import preserves in-scope fields, then run malformed CSV inputs and confirm previously stored product data remains unchanged.

- [x] T022 [US2] Implement the representative product-export readiness scenario in `RetailStorePOS.Data/Modules/Products/ProductExportService.cs` and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [x] T023 [P] [US2] Implement the valid product-import round-trip scenario and field comparison in `RetailStorePOS.Data/Modules/Products/ProductImportService.cs`, `RetailStorePOS.Data/Modules/Products/ProductCsvRow.cs`, and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [x] T024 [P] [US2] Implement imported-thumbnail and image-reference validation in `Nexill.RetailStorePOS/Services/ProductImageService.cs`, `Nexill.RetailStorePOS/Views/ProductsPage.xaml.cs`, and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [x] T025 [P] [US2] Implement the malformed CSV safe-failure scenario in `RetailStorePOS.Data/Modules/Products/ProductImportService.cs` and `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs`
- [X] T026 [US2] Persist import/export artifacts and round-trip diff summaries in `RetailStorePOS.Data/Modules/Reporting/ImportExportSampleCatalog.cs` and `RetailStorePOS.Data/Modules/Reporting/ReadinessReportStore.cs`
- [X] T027 [US2] Add import/export scenario results to the operator-facing readiness summary in `Nexill.RetailStorePOS/Modules/Reporting/ReadinessService.cs` and `Nexill.RetailStorePOS/Views/ReportsPage.xaml.cs`

**Checkpoint**: The readiness pass verifies the current product CSV import/export surface, including safe failure behavior for invalid input.

---

## Phase 5: User Story 3 - Refresh Dashboard Data on Open (Priority: P2)

**Goal**: Make every dashboard open cycle show the last successful values immediately when available, then refresh them in the background and mark stale state correctly on failure.

**Independent Test**: Change underlying business data, reopen the dashboard, confirm cached values appear immediately when present, confirm fresh values replace them within 5 seconds, and confirm failed refreshes keep prior values visible with stale/error signaling.

- [x] T028 [US3] Load the last successful dashboard snapshot before current-cycle queries in `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs` and `RetailStorePOS.Data/Modules/Reporting/DashboardSnapshotStore.cs`
- [x] T029 [P] [US3] Add cached-value, refreshing, and stale-state UI surfaces in `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml`
- [x] T030 [US3] Persist successful dashboard metric payloads as local snapshots in `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs` and `RetailStorePOS.Data/Modules/Reporting/DashboardSnapshotStore.cs`
- [x] T031 [P] [US3] Replace one-time dashboard load gating with open-cycle refresh coordination in `Nexill.RetailStorePOS/Views/ReportsPage.xaml.cs` and `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs`
- [x] T032 [P] [US3] Mark failed refreshes as stale without relabeling old values as fresh in `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs` and `RetailStorePOS.Data/Modules/Reporting/DashboardRefreshCycle.cs`
- [x] T033 [US3] Add the dashboard reopen-after-data-change readiness scenario and 5-second timing capture in `RetailStorePOS.Data/Modules/Reporting/ReadinessRunner.cs` and `Nexill.RetailStorePOS/Modules/Reporting/ReadinessService.cs`
- [x] T034 [US3] Surface last-refresh metadata and explicit refresh-trigger behavior in `Nexill.RetailStorePOS/Views/ReportsPage.xaml` and `Nexill.RetailStorePOS/Views/ReportsPage.xaml.cs`

**Checkpoint**: The dashboard reopens with immediate cached values, background refresh, and explicit stale-state handling rather than one-time stale loads.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency work across readiness automation, import/export reporting, and dashboard freshness.

- [x] T035 [P] Update the automated readiness invocation steps and expected outputs in `specs/008-readiness-dashboard-freshness/quickstart.md`
- [x] T036 [P] Reconcile readiness and dashboard snapshot terminology in `AGENTS.md`
- [x] T037 Cross-check `specs/008-readiness-dashboard-freshness/spec.md`, `specs/008-readiness-dashboard-freshness/plan.md`, `specs/008-readiness-dashboard-freshness/tasks.md`, `specs/008-readiness-dashboard-freshness/research.md`, `specs/008-readiness-dashboard-freshness/data-model.md`, and `specs/008-readiness-dashboard-freshness/contracts/` for consistent scenario keys and minimal/representative dataset coverage

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** — no dependencies
- **Phase 2: Foundational** — depends on Phase 1 and blocks all user stories
- **Phase 3: US1** — depends on Phase 2
- **Phase 4: US2** — depends on Phase 2 and integrates into the readiness orchestration completed in US1
- **Phase 5: US3** — depends on Phase 2; its dashboard reopen verification scenario integrates into the readiness orchestration from US1
- **Phase 6: Polish** — depends on all selected user stories being complete

### User Story Dependencies

- **US1**: First story because it establishes the readiness orchestration, result persistence, and operator-facing run surface
- **US2**: Builds on the readiness orchestration from US1 to add import/export verification artifacts and reporting
- **US3**: Can begin after the foundation for local snapshot work, but its automated reopen verification plugs into the readiness service from US1

---

## Parallel Opportunities

### Phase 2

```text
T005 Create the verification scenario model and blocking-severity definitions
T006 Create the dataset profile and import/export sample models
T007 Create the dashboard snapshot and refresh-cycle models
T009 Create machine-readable readiness report persistence
T010 Create local dashboard snapshot persistence
T012 Extend reporting workflow contracts for readiness and snapshot files
```

### User Story 1

```text
T015 Implement startup and local database readiness scenarios
T016 Implement the login/auth readiness scenario
T017 Implement catalog lookup and local data-access readiness scenarios
T018 Implement the tax-calculation readiness scenario
```

### User Story 2

```text
T023 Implement the valid product-import round-trip scenario and field comparison
T024 Implement imported-thumbnail and image-reference validation
T025 Implement the malformed CSV safe-failure scenario
```

### User Story 3

```text
T029 Add cached-value, refreshing, and stale-state UI surfaces
T031 Replace one-time dashboard load gating with open-cycle refresh coordination
T032 Mark failed refreshes as stale without relabeling old values as fresh
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Complete Phase 4: User Story 2
5. **STOP and VALIDATE**: Run the readiness pass against both dataset profiles before starting dashboard freshness work

### Incremental Delivery

1. Finish Setup + Foundational to make readiness models, local workspaces, and report persistence available
2. Deliver US1 to create the automated readiness pass for core workflows
3. Deliver US2 to close the product CSV import/export integrity gap inside that pass
4. Deliver US3 to add snapshot-first dashboard freshness and stale-state behavior
5. Finish Polish to align quickstart and agent context with the shipped flow

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. After the foundation:
   - Developer A: US1 orchestration and scenario runner
   - Developer B: US2 import/export scenarios
   - Developer C: US3 dashboard snapshot and UI-state work
3. Rejoin for Polish once scenario reporting and dashboard behavior are both stable

---

## Notes

- All tasks use the required checklist format with task ID, optional parallel marker, optional story label, and exact file path
- No separate TDD tasks are included because the feature scope is the automated readiness flow itself, not a new test-project rollout
- The current import/export scope is intentionally limited to the user-accessible product CSV workflow exposed by `ProductsPage`
- The readiness pass must use isolated local workspaces and must not mutate the operator's active runtime database directly
- The dashboard story preserves the clarified rule: refresh on open, not continuous live updates while the same dashboard page instance remains open
