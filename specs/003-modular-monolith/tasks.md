# Tasks: Modular Monolith Boundaries

**Input**: Design documents from `/specs/003-modular-monolith/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`

**Tests**: No automated test tasks are included because the feature spec does not request TDD or new automated coverage. Validation is driven by the manual verification scenarios in `specs/003-modular-monolith/quickstart.md`.

**Organization**: Tasks are grouped by user story so each story can be implemented and verified independently once foundational work is complete.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (`US1`, `US2`, `US3`, `US4`, `US5`, `US6`)
- Every task includes an exact file path

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the shared scaffolding and governance documents that the modular-monolith rollout will use.

- [X] T001 Create the data-layer module scaffold note in `RetailStorePOS.Data/Modules/README.md`
- [X] T002 [P] Create the WinUI module scaffold note in `Nexill.RetailStorePOS/Modules/README.md`
- [X] T003 [P] Create the ownership map template in `specs/003-modular-monolith/ownership-map.md`
- [X] T004 [P] Create the legacy exception register template in `specs/003-modular-monolith/legacy-exceptions.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the runtime-compliance and shared boundary artifacts that block all user stories.

**⚠️ CRITICAL**: No user story work should begin until this phase is complete.

- [X] T005 Update the runtime target in `RetailStorePOS.Data/RetailStorePOS.Data.csproj` to the constitution-required `.NET 9+` baseline
- [X] T006 [P] Update the runtime target and WinUI project metadata in `Nexill.RetailStorePOS/Nexill.RetailStorePOS.csproj` to the constitution-required `.NET 9+ / WinUI 3` baseline
- [X] T007 [P] Record the human runtime-verification handoff in `specs/003-modular-monolith/quickstart.md`
- [X] T008 Create the current file-to-module inventory in `specs/003-modular-monolith/file-inventory.md`
- [X] T009 [P] Create the workflow trace skeleton in `specs/003-modular-monolith/workflow-trace.md`
- [X] T010 [P] Create reviewer guardrails for forbidden cross-module access in `specs/003-modular-monolith/review-guardrails.md`
- [X] T011 Create the shared module contract abstractions in `RetailStorePOS.Data/Modules/Contracts/ModuleContract.cs`
- [X] T012 [P] Create the owned-data asset abstractions in `RetailStorePOS.Data/Modules/Contracts/OwnedDataAsset.cs`
- [X] T013 [P] Create the workflow boundary abstractions in `RetailStorePOS.Data/Modules/Contracts/WorkflowBoundary.cs`
- [X] T014 Create the business and platform module directory scaffold under `RetailStorePOS.Data/Modules/` and `Nexill.RetailStorePOS/Modules/`

**Checkpoint**: Foundation is ready. User stories can now begin in dependency order.

---

## Phase 3: User Story 1 - Isolate business changes by module (Priority: P1) 🎯

**Goal**: Make business ownership visible in the codebase so a change request can be scoped to one owning module plus declared cross-module contracts.

**Independent Test**: Pick a product or tax change request, trace the owning module, and confirm the required edits are limited to the module’s files plus explicit contracts.

- [X] T015 [US1] Record business-module owners for Sales, Products, Inventory, Users/Auth, Tax, and Reporting in `specs/003-modular-monolith/ownership-map.md`
- [X] T016 [P] [US1] Move product domain files from `RetailStorePOS.Data/Product.cs`, `RetailStorePOS.Data/ProductRepository.cs`, `RetailStorePOS.Data/ProductImportService.cs`, `RetailStorePOS.Data/ProductExportService.cs`, and `RetailStorePOS.Data/Models/ProductCsvRow.cs` into `RetailStorePOS.Data/Modules/Products/`
- [X] T017 [P] [US1] Move sales domain files from `RetailStorePOS.Data/Sale.cs`, `RetailStorePOS.Data/SaleItem.cs`, and `RetailStorePOS.Data/SaleRepository.cs` into `RetailStorePOS.Data/Modules/Sales/`
- [X] T018 [P] [US1] Move tax domain files from `RetailStorePOS.Data/TaxCategory.cs`, `RetailStorePOS.Data/TaxSettings.cs`, `RetailStorePOS.Data/Models/TaxAuthority.cs`, `RetailStorePOS.Data/Models/TaxGroup.cs`, `RetailStorePOS.Data/Models/TaxRule.cs`, `RetailStorePOS.Data/Repositories/TaxAuthorityRepository.cs`, `RetailStorePOS.Data/Repositories/TaxGroupRepository.cs`, `RetailStorePOS.Data/Repositories/TaxRuleRepository.cs`, and `RetailStorePOS.Data/TaxCategoryRepository.cs` into `RetailStorePOS.Data/Modules/Tax/`
- [X] T019 [P] [US1] Create the inventory module scaffold and stock-ownership note in `RetailStorePOS.Data/Modules/Inventory/README.md` and `Nexill.RetailStorePOS/Modules/Inventory/README.md`
- [X] T020 [US1] Update product module UI references in `Nexill.RetailStorePOS/Views/ProductsPage.xaml.cs`
- [X] T021 [US1] Update checkout sales and tax module references in `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs`
- [X] T022 [US1] Update tax administration module references in `Nexill.RetailStorePOS/Views/TaxConfigurationPage.xaml.cs`

**Checkpoint**: Business-area changes can be traced to explicit product, sales, tax, and inventory module ownership.

---

## Phase 4: User Story 2 - Assign data ownership explicitly (Priority: P1)

**Goal**: Ensure every important table and aggregate has exactly one owning module and that non-owning modules use published contracts rather than hidden writes.

**Independent Test**: Review the ownership map and confirm each important asset has one owner, with no direct cross-module write shortcut remaining in migrated scope.

- [X] T023 [US2] Document owned tables and aggregates for products, sales, inventory, tax, users, telemetry, settings, migrations, and reporting read models in `specs/003-modular-monolith/ownership-map.md`
- [X] T024 [P] [US2] Align product and sales write ownership in `RetailStorePOS.Data/Modules/Products/ProductRepository.cs` and `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs`
- [X] T025 [P] [US2] Align tax and user write ownership in `RetailStorePOS.Data/Modules/Tax/TaxCategoryRepository.cs` and `RetailStorePOS.Data/Repositories/UserRepository.cs`
- [X] T026 [P] [US2] Extract inventory-owned stock rules from `RetailStorePOS.Data/Modules/Products/Product.cs` and `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs` into `RetailStorePOS.Data/Modules/Inventory/`
- [X] T027 [P] [US2] Align telemetry, settings, and migration ownership in `RetailStorePOS.Data/Repositories/InstallationEventRepository.cs`, `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs`, `RetailStorePOS.Data/SettingsRepository.cs`, `RetailStorePOS.Data/MigrationModel.cs`, and `RetailStorePOS.Data/Class1.cs`
- [X] T028 [US2] Update the final owner-by-asset mapping in `specs/003-modular-monolith/file-inventory.md`

**Checkpoint**: Important persisted assets have one owner and migrated writes follow owner-approved paths.

---

## Phase 5: User Story 3 - Preserve one application experience (Priority: P1)

**Goal**: Keep the product as one WinUI desktop application backed by one local SQLite dataset while the internal boundaries are introduced.

**Independent Test**: Launch the app, navigate existing pages, and confirm core workflows still run inside one windowed desktop app backed by the same local database path.

- [X] T029 [US3] Keep the single-window bootstrap and navigation flow in `Nexill.RetailStorePOS/App.xaml.cs` and `Nexill.RetailStorePOS/MainWindow.xaml.cs`
- [X] T030 [P] [US3] Keep the shared local SQLite boot path in `RetailStorePOS.Data/DatabasePaths.cs` and `RetailStorePOS.Data/SqliteConnectionFactory.cs`
- [X] T031 [P] [US3] Keep local settings and preferences in-process in `Nexill.RetailStorePOS/Services/ILocalPreferencesService.cs` and `Nexill.RetailStorePOS/Services/LocalPreferencesService.cs`
- [X] T032 [US3] Verify no extra project or service-host bootstrap exists in `Nexill.RetailStorePOS/Nexill.RetailStorePOS.csproj` and `RetailStorePOS.Data/RetailStorePOS.Data.csproj`, then document the result in `specs/003-modular-monolith/file-inventory.md`

**Checkpoint**: The app remains one desktop process boundary with one local SQLite source of truth.

---

## Phase 6: User Story 4 - Coordinate cross-module workflows explicitly (Priority: P1)

**Goal**: Replace hidden cross-module access with explicit coordination contracts for checkout, inventory, reporting, and platform-module workflows.

**Independent Test**: Trace a checkout or reporting workflow and confirm every participating module interaction goes through a declared contract or owner-approved sequence.

- [X] T033 [US4] Implement checkout coordination contract definitions in `RetailStorePOS.Data/Modules/Contracts/CheckoutWorkflowContract.cs`
- [X] T034 [P] [US4] Implement reporting coordination contract definitions in `RetailStorePOS.Data/Modules/Contracts/ReportingWorkflowContract.cs`
- [X] T035 [P] [US4] Implement inventory coordination contract definitions in `RetailStorePOS.Data/Modules/Contracts/InventoryWorkflowContract.cs`
- [X] T036 [P] [US4] Implement telemetry platform contract definitions in `RetailStorePOS.Data/Modules/Contracts/TelemetryPlatformContract.cs`
- [X] T037 [P] [US4] Implement settings platform contract definitions in `RetailStorePOS.Data/Modules/Contracts/SettingsPlatformContract.cs`
- [X] T038 [P] [US4] Implement sync handoff contract definitions in `RetailStorePOS.Data/Modules/Contracts/SyncWorkflowContract.cs`
- [X] T039 [P] [US4] Implement migrations platform contract definitions in `RetailStorePOS.Data/Modules/Contracts/MigrationPlatformContract.cs`
- [X] T040 [US4] Route telemetry through explicit platform contracts in `Nexill.RetailStorePOS/Services/TelemetryService.cs` and `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs`
- [X] T041 [US4] Route settings through explicit platform contracts in `Nexill.RetailStorePOS/Services/ILocalPreferencesService.cs`, `Nexill.RetailStorePOS/Services/LocalPreferencesService.cs`, and `RetailStorePOS.Data/SettingsRepository.cs`
- [X] T042 [US4] Route startup and maintenance migrations through explicit platform contracts in `RetailStorePOS.Data/Class1.cs` and `RetailStorePOS.Data/MigrationModel.cs`
- [X] T043 [US4] Route checkout coordination through explicit module contracts in `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs`
- [X] T044 [US4] Route product, inventory, and reporting coordination through explicit module contracts in `Nexill.RetailStorePOS/Views/ProductsPage.xaml.cs`, `Nexill.RetailStorePOS/Views/ReportsPage.xaml.cs`, and `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs`
- [X] T045 [US4] Record the final read and write sequences for checkout, inventory, reporting, telemetry, settings, sync, and migrations in `specs/003-modular-monolith/workflow-trace.md`

**Checkpoint**: Cross-module workflows are explicit, reviewable, and no longer rely on hidden internal access.

---

## Phase 7: User Story 5 - Migrate incrementally without a rewrite (Priority: P2)

**Goal**: Stage the rollout with temporary owners and exit plans so mixed-ownership code can be reduced without blocking the live app.

**Independent Test**: Review the active exception list, verify each open exception has a temporary owner and target module, and confirm the first migration slice can land without rewriting the whole app.

- [X] T046 [US5] Record current mixed-ownership files and temporary owners in `specs/003-modular-monolith/legacy-exceptions.md`
- [X] T047 [P] [US5] Assign target modules and exit plans for `Nexill.RetailStorePOS/Services/AuthService.cs`, `Nexill.RetailStorePOS/Services/TelemetryService.cs`, and `Nexill.RetailStorePOS/Services/LocalPreferencesService.cs` in `specs/003-modular-monolith/legacy-exceptions.md`
- [X] T048 [P] [US5] Assign target modules and exit plans for `Nexill.RetailStorePOS/ViewModels/CheckoutViewModel.cs`, `Nexill.RetailStorePOS/ViewModels/ReportsViewModel.cs`, `Nexill.RetailStorePOS/ViewModels/SettingsViewModel.cs`, and `Nexill.RetailStorePOS/ViewModels/UsersPageViewModel.cs` in `specs/003-modular-monolith/legacy-exceptions.md`
- [X] T049 [US5] Move `Nexill.RetailStorePOS/Services/ProductSearchService.cs` into `Nexill.RetailStorePOS/Modules/Products/`
- [X] T050 [US5] Update the staged migration checkpoints after closing the first exception in `specs/003-modular-monolith/quickstart.md`

**Checkpoint**: The migration is staged, the first exception is closed, and the remaining mixed code is explicitly tracked.

---

## Phase 8: User Story 6 - Protect offline-critical store workflows (Priority: P2)

**Goal**: Preserve fully local checkout and related store workflows so the module split never introduces mandatory network behavior.

**Independent Test**: Run checkout with no network connectivity and confirm product lookup, inventory decrement, tax calculation, receipt creation, and local persistence still complete inside the app.

- [X] T051 [US6] Record offline-critical checkout, product lookup, inventory decrement, tax calculation, and receipt creation paths in `specs/003-modular-monolith/workflow-trace.md`
- [X] T052 [P] [US6] Remove mandatory network dependencies from `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs` and `Nexill.RetailStorePOS/Modules/Products/ProductSearchService.cs`
- [X] T053 [P] [US6] Keep inventory decrement and sale persistence local in `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs` and `RetailStorePOS.Data/Modules/Inventory/`
- [X] T054 [P] [US6] Keep telemetry optional and asynchronous in `Nexill.RetailStorePOS/Services/TelemetryService.cs` and `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs`
- [X] T055 [P] [US6] Keep local launch and database startup independent of network in `Nexill.RetailStorePOS/App.xaml.cs` and `RetailStorePOS.Data/SqliteConnectionFactory.cs`
- [X] T056 [US6] Update offline verification steps and expected results in `specs/003-modular-monolith/quickstart.md`

**Checkpoint**: Offline-critical workflows remain fully local even after the boundary refactor.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Final consistency work across stories.

- [X] T057 [P] Reconcile the final module structure and verification workflow in `AGENTS.md`
- [X] T058 [P] Cross-check `specs/003-modular-monolith/spec.md`, `specs/003-modular-monolith/plan.md`, `specs/003-modular-monolith/tasks.md`, `specs/003-modular-monolith/ownership-map.md`, `specs/003-modular-monolith/legacy-exceptions.md`, and `specs/003-modular-monolith/workflow-trace.md` for consistency
- [X] T059 Run a timed owner-lookup drill using `specs/003-modular-monolith/ownership-map.md`, `specs/003-modular-monolith/file-inventory.md`, and `specs/003-modular-monolith/workflow-trace.md`; record elapsed time and confirm it is `<= 5 minutes` in `specs/003-modular-monolith/quickstart.md`
- [X] T060 Run the manual rollout checklist in `specs/003-modular-monolith/quickstart.md` and capture the results in `specs/003-modular-monolith/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** — no dependencies
- **Phase 2: Foundational** — depends on Phase 1 and blocks all user stories
- **Phase 3: US1** — depends on Phase 2
- **Phase 4: US2** — depends on US1 because repository ownership follows the module moves
- **Phase 5: US3** — depends on Phase 2 and can run in parallel with US1/US2 if staffing allows
- **Phase 6: US4** — depends on US1 and US2 because workflow contracts rely on settled module and data ownership
- **Phase 7: US5** — depends on Phase 2; closing the first exception is easiest after US1 has established module folders
- **Phase 8: US6** — depends on US3, US4, and T049 because offline-critical verification needs the final workflow and migrated product-search path
- **Phase 9: Polish** — depends on all selected user stories being complete

### User Story Dependencies

- **US1**: First story for establishing explicit module ownership in code
- **US2**: Builds on US1 to make data ownership enforceable
- **US3**: Can proceed after the foundation because it protects the app-level runtime shape
- **US4**: Requires the business and data ownership work from US1 and US2
- **US5**: Can begin after the foundation, but its first closed exception should align with the module structure created in US1
- **US6**: Depends on preserving the single-app model from US3 and the explicit workflow contracts from US4

---

## Parallel Opportunities

### Phase 2

```text
T006 Update the WinUI project runtime target to .NET 9+
T007 Record the human runtime-verification handoff
T009 Create the workflow trace skeleton
T010 Create reviewer guardrails
T012 Create the owned-data asset abstractions
T013 Create the workflow boundary abstractions
```

### User Story 1

```text
T016 Move product domain files into RetailStorePOS.Data/Modules/Products/
T017 Move sales domain files into RetailStorePOS.Data/Modules/Sales/
T018 Move tax domain files into RetailStorePOS.Data/Modules/Tax/
T019 Create inventory module scaffolds
```

### User Story 2

```text
T024 Align product and sales write ownership
T025 Align tax and user write ownership
T026 Extract inventory-owned stock rules
T027 Align telemetry, settings, and migration ownership
```

### User Story 4

```text
T034 Implement reporting coordination contract definitions
T035 Implement inventory coordination contract definitions
T036 Implement telemetry platform contract definitions
T037 Implement settings platform contract definitions
T038 Implement sync handoff contract definitions
T039 Implement migrations platform contract definitions
```

### User Story 5

```text
T047 Assign target modules and exit plans for Services/*.cs
T048 Assign target modules and exit plans for ViewModels/*.cs
```

### User Story 6

```text
T052 Remove mandatory network dependencies from checkout and product lookup
T053 Keep inventory decrement and sale persistence local
T054 Keep telemetry optional and asynchronous
T055 Keep local launch and database startup independent of network
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational, including the runtime retarget to `.NET 9+ / WinUI 3`
3. Complete Phase 3: US1
4. Complete Phase 4: US2
5. Stop and validate that ownership and asset mapping are enforceable before expanding the rollout

### Incremental Delivery

1. Finish Setup + Foundational to make the module rollout executable and constitution-aligned
2. Deliver US1 + US2 to establish enforceable ownership
3. Deliver US3 to preserve the existing one-app runtime shape
4. Deliver US4 to make cross-module coordination explicit
5. Deliver US5 to reduce legacy exceptions gradually
6. Deliver US6 to lock down offline-critical behavior

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. After the foundation:
   - Developer A: US1 then US2
   - Developer B: US3 in parallel
   - Developer C: US5 documentation and exception tracking
3. After US1 + US2 land:
   - Developer A/B: US4 contracts and workflow routing
   - Developer C: US6 offline safeguards

---

## Notes

- All tasks use the required checklist format with task ID, optional parallel marker, optional story label, and exact file path
- No automated test tasks are included because the spec does not request TDD or new test suites
- `ownership-map.md`, `legacy-exceptions.md`, `file-inventory.md`, `workflow-trace.md`, and `review-guardrails.md` are intentionally created during implementation because the plan depends on them
- Runtime retargeting is now explicit blocking work rather than a deferred baseline note
- Use the checkpoints to stop and validate each story independently before moving deeper into the rollout
