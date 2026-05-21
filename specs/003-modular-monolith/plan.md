# Implementation Plan: Modular Monolith Boundaries

**Branch**: `003-modular-monolith` | **Date**: 2026-04-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/003-modular-monolith/spec.md`

## Summary

This plan introduces enforceable modular-monolith boundaries inside the existing two-project Retail Store POS solution without adding new assemblies, network services, or remote dependencies. The implementation first restores constitution compliance for the runtime baseline, then separates business modules from platform modules, assigns explicit ownership for capabilities and important persisted data, defines allowed cross-module coordination paths, and stages migration through documented legacy exceptions while preserving one desktop app, one shared local SQLite database, and offline-critical checkout workflows.

## Technical Context

**Language/Version**: C# on .NET 8 / WinUI 3 in the current repository baseline, with required retargeting to `.NET 9+ / WinUI 3` during implementation  
**Primary Dependencies**: Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, LiveChartsCore.SkiaSharpView.WinUI, CsvHelper, System.Security.Cryptography.ProtectedData  
**Storage**: Local SQLite database in WAL mode as primary store; optional runtime Supabase sync and license validation only  
**Testing**: No dedicated automated test project exists in this version; validation will use manual regression scenarios against realistic local SQLite datasets  
**Target Platform**: Windows 10+ desktop application, x64-first MSIX deployment  
**Project Type**: Two-project desktop app (`Nexill.RetailStorePOS` WinUI front end + `RetailStorePOS.Data` class library)  
**Performance Goals**: Keep checkout and other offline-critical workflows in-process and responsive; keep heavy report and aggregation work off the UI thread; introduce no mandatory network dependency into local execution paths  
**Constraints**: Exactly two projects, one shared local SQLite database, local-first/offline-first behavior, no new service boundary, no new compile-time cloud dependency, no delete/build steps in this workflow, version-isolated release folder  
**Scale/Scope**: Existing app spans checkout, products, reports, tax, users, settings, telemetry, and sync concerns across 2 projects, 10+ WinUI pages, and data volumes sized for stores with 400K+ receipts and 40M+ sold items

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Design Gate

**Gate Status**: CONDITIONAL. The feature is planned, but it is not constitution-complete for implementation until the runtime baseline is retargeted to `.NET 9+ / WinUI 3`.

1. **Local-First Data Sovereignty**: PASS. The design keeps SQLite as the system of record and treats Sync/Telemetry as optional supporting modules only.
2. **Notion-Grade Visual Design**: PASS. This feature is architectural and does not require a design-system change.
3. **Non-Blocking, Concurrent UX**: PASS. Cross-module orchestration remains in-process, and heavy reporting/aggregation work continues to run off the UI thread.
4. **Two-Layer Architecture**: PASS. The design stays inside the existing two projects and uses module-aligned folders and namespaces instead of introducing new assemblies.
5. **Keyboard-First Checkout**: PASS. Checkout remains a protected offline-critical workflow and is not allowed to gain new mouse or network dependencies.
6. **Zero-Cloud Build Dependency**: PASS. No build-time cloud service dependency is introduced; sync remains runtime-optional.
7. **Versioned Isolation**: PASS. All planning artifacts stay inside `V/1.3.3`.
8. **Runtime Baseline Compliance**: OPEN. The current repository targets `net8.0` and `net8.0-windows10.0.19041.0`, while the constitution target table states `.NET 9+ / WinUI 3`. This plan now treats runtime retargeting as mandatory foundational work before user-story implementation proceeds.

### Post-Design Re-Check

1. **Local-First Data Sovereignty**: PASS. Business ownership rules prevent Sync or Telemetry from becoming the source of truth.
2. **Notion-Grade Visual Design**: PASS. No new UI toolkit or visual layer is introduced by the design.
3. **Non-Blocking, Concurrent UX**: PASS. Workflow coordination is defined as in-process orchestration with background execution for heavy operations.
4. **Two-Layer Architecture**: PASS. Module boundaries are documented as namespace/folder conventions inside the current two projects.
5. **Keyboard-First Checkout**: PASS. Offline-critical checkout remains local and protected in the workflow contracts.
6. **Zero-Cloud Build Dependency**: PASS. Contracts and quickstart rely only on local tooling and documentation.
7. **Versioned Isolation**: PASS. Artifacts are version-local and do not assume in-place changes to other release folders.
8. **Runtime Baseline Compliance**: PLANNED. The artifacts now require a retarget of both projects to `.NET 9+ / WinUI 3` before the feature is considered implementation-ready.

## Project Structure

### Documentation (this feature)

```text
specs/003-modular-monolith/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── module-boundaries.md
│   └── workflow-coordination.md
└── tasks.md
```

### Source Code (repository root)

```text
E:\Projects\Retail_Store\V\1.3.3\
├── RetailStorePOS.Data\
│   ├── Models\
│   ├── Repositories\
│   ├── ProductRepository.cs
│   ├── SaleRepository.cs
│   ├── SettingsRepository.cs
│   ├── TaxCategoryRepository.cs
│   └── TaxSettings.cs
├── Nexill.RetailStorePOS\
│   ├── Common\
│   ├── Models\
│   ├── Services\
│   ├── ViewModels\
│   └── Views\
└── specs\
    └── 003-modular-monolith\
```

**Structure Decision**: Keep the constitution-mandated two-project solution and introduce module boundaries as folders, namespaces, ownership documents, and dependency rules inside those two projects. Existing `Services/` and `ViewModels/` folders are treated as transitional implementation areas that will be reduced or rehomed over time; they are not expanded into new architectural layers or new projects.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Current runtime baseline is `.NET 8`, while the constitution target table says `.NET 9+` | Runtime uplift is mandatory constitution-compliance work and must be completed before the modular-boundary rollout is considered implementation-ready | Ignoring the runtime mismatch would leave the plan in explicit conflict with a non-negotiable constitution constraint |

---

## Phase 0: Outline & Research

### Research Findings (`research.md`)

1. Separate business modules from platform modules so ownership rules apply consistently.
2. Enforce module boundaries with folder/namespace conventions and documented contracts inside the existing two projects.
3. Assign exactly one owner to each important business capability and important persisted table or aggregate.
4. Route cross-module reads, commands, and events through owner-published contracts instead of direct internal repository access.
5. Control staged migration with explicit legacy exception records that include a temporary owner and exit plan.
6. Protect offline-critical workflows so the modular split does not introduce network dependencies into checkout or other local store operations.
7. Treat runtime-baseline compliance as mandatory foundational work inside this feature instead of a deferred exception.

## Phase 1: Design & Contracts

### Data Model (`data-model.md`)

The design introduces governance entities rather than new runtime storage tables:

- `BusinessModule`
- `PlatformModule`
- `OwnershipMapEntry`
- `ModuleContract`
- `OwnedDataAsset`
- `WorkflowBoundary`
- `LegacyException`

### Interface Contracts (`contracts/`)

- `module-boundaries.md`: Defines module taxonomy, owners, allowed dependencies, and forbidden direct internal access.
- `workflow-coordination.md`: Defines in-process coordination rules for checkout, reporting, sync, and legacy transition workflows.

### Quickstart (`quickstart.md`)

Documents the first implementation slice, ownership-mapping steps, migration order, and manual verification scenarios for offline-critical behavior.

### Agent Context

Update the Codex agent context after the design artifacts are written so subsequent task generation inherits the current technical stack and module-boundary rules.

---

## Phase 2: Execution Plan

### Slice 0 - Runtime Compliance

1. Retarget `RetailStorePOS.Data` and `Nexill.RetailStorePOS` to the constitution-required `.NET 9+ / WinUI 3` baseline.
2. Reconcile package compatibility and project metadata after the retarget without introducing new projects or cloud build dependencies.
3. Document the manual runtime-compliance verification path in the feature quickstart because build execution remains human-only.

### Slice 1 - Ownership Foundation

1. Create the ownership map for business and platform modules.
2. Identify important in-scope tables/aggregates and assign one owning module to each.
3. Record current legacy exceptions for files, repositories, and pages that do not yet align cleanly.

### Slice 2 - High-Risk Business Flow Alignment

1. Align Sales, Products, Inventory, and Tax around the checkout workflow.
2. Replace direct internal dependencies with owner-published contracts for checkout-critical paths.
3. Make Inventory explicit in the rollout by introducing a concrete stock-ownership scaffold and inventory workflow contract.
4. Verify no network dependency enters local checkout, product lookup, inventory decrement, or receipt creation flows.

### Slice 3 - Remaining Domain and Platform Alignment

1. Align Users/Auth and Reporting against the same ownership and contract rules.
2. Reframe Telemetry, Sync, Settings, and Migrations as platform modules with explicit ownership and published interfaces that support business modules without owning business rules.
3. Reduce transitional `Services/` and `ViewModels/` usages where they currently hide cross-module ownership.

### Slice 4 - Legacy Reduction and Guardrails

1. Shrink the legacy exception list by moving files and responsibilities into declared modules.
2. Add review guardrails so new work cannot introduce direct internal cross-module dependencies.
3. Verify the current `.csproj` files do not introduce extra project or service-host bootstrap concerns and document the result.
4. Prepare task-level verification steps for ownership mapping, workflow tracing, offline-critical scenarios, and runtime-compliance verification.
