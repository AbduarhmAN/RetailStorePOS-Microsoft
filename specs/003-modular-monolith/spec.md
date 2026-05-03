# Feature Specification: modular-monolith

**Feature Branch**: `003-modular-monolith`  
**Created**: 2026-04-20  
**Status**: Draft  
**Input**: User description: "modular monolith"

## Scope

This specification defines a modular-monolith boundary model for the retail desktop application.

The modular-monolith boundary model distinguishes two kinds of modules:

- **Business modules**: modules that own business capabilities, business rules, and primary business data for a domain.
- **Platform modules**: modules that provide supporting capabilities used by business modules without becoming owners of their business rules.

The initial in-scope business modules are:

- Sales
- Products
- Inventory
- Users/Auth
- Tax
- Reporting

The initial in-scope platform modules are:

- Telemetry
- Sync
- Settings
- Migrations

This specification requires every module to have explicit ownership boundaries. Business modules own their business workflows, rules, aggregates, and externalized contracts. Platform modules own their supporting capabilities and published interfaces, but they do not become owners of another module's business rules.

This specification explicitly preserves:

- one desktop application experience
- one shared local database
- no forced network boundary between modules

## Clarifications

### Session 2026-04-21

- Q: How should post-move module tasks reference relocated files? → A: Use post-move paths under `RetailStorePOS.Data/Modules/Products/` and `RetailStorePOS.Data/Modules/Sales/`.
- Q: How should SC-003 be verified? → A: Run a timed owner-lookup drill using the feature artifacts and confirm the result is `<= 5 minutes`.
- Q: What must Phase 0 research include about runtime baseline compliance? → A: Include the runtime-baseline compliance decision as a mandatory foundational research finding.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Isolate business changes by module (Priority: P1)

As a product maintainer, I need each business area to have a clear owning module so that I can change one area without causing avoidable regressions in unrelated areas.

**Why this priority**: Clear ownership is the primary value of the modular-monolith split and is necessary before deeper refactoring can be done safely.

**Independent Test**: Pick a change request in one business area, identify its owning module, and verify the required updates are scoped to that module plus declared cross-module contracts.

**Acceptance Scenarios**:

1. **Given** a change request for product catalog behavior, **When** a maintainer analyzes the request, **Then** the Products module is identified as the primary owner and unrelated modules do not need direct internal edits.
2. **Given** a defect in tax calculation behavior, **When** the team traces the issue, **Then** the Tax module is identified as the owning boundary for the business rule and exposed interfaces.

---

### User Story 2 - Assign data ownership explicitly (Priority: P1)

As a maintainer, I need every important table and aggregate to have exactly one owning module so that data changes do not bypass the correct business rules.

**Why this priority**: A modular monolith is not enforceable unless important persisted data has one clear owner.

**Independent Test**: Review the ownership map for in-scope data and confirm each important table or aggregate has one owning module and no shared ownership.

**Acceptance Scenarios**:

1. **Given** an important persisted business structure is in scope, **When** the ownership map is reviewed, **Then** exactly one module is identified as its owner.
2. **Given** another module needs to read or update owned data, **When** the interaction is reviewed, **Then** it goes through the owning module's declared contract instead of direct internal access.

---

### User Story 3 - Preserve one application experience (Priority: P1)

As a store operator, I need the system to remain one desktop application with one local dataset so that day-to-day work does not become more complex because of internal restructuring.

**Why this priority**: The architectural change must not degrade the current operational model for end users.

**Independent Test**: Run the existing core store workflows and confirm they still operate inside one installed desktop app with no separate service setup.

**Acceptance Scenarios**:

1. **Given** the modular-monolith boundary model is adopted, **When** a cashier completes a sale, **Then** the workflow still runs inside the same desktop app session.
2. **Given** the modular-monolith boundary model is adopted, **When** the app reads or writes business data, **Then** the workflow still uses the same shared local dataset.

---

### User Story 4 - Coordinate cross-module workflows explicitly (Priority: P1)

As a product maintainer, I need workflows that span multiple business areas to use explicit contracts between modules so that dependencies stay visible and manageable.

**Why this priority**: Many retail flows touch multiple business domains, so the boundary model is only useful if collaboration across modules remains explicit and understandable.

**Independent Test**: Trace a workflow such as completing a sale and verify which modules participate, what each owns, and how information crosses boundaries.

**Acceptance Scenarios**:

1. **Given** a sale affects inventory, tax, and telemetry, **When** the workflow is documented and reviewed, **Then** each participating module's responsibility is explicit and no module depends on another module's hidden internals.
2. **Given** a workflow requires coordinated writes across module boundaries, **When** the workflow is reviewed, **Then** the coordination path is explicit and does not depend on another module's private repositories or private business logic.

---

### User Story 5 - Migrate incrementally without a rewrite (Priority: P2)

As an engineering lead, I need the module split to be adoptable in stages so that the existing application can move toward clearer boundaries without requiring a risky full rewrite.

**Why this priority**: Incremental adoption reduces delivery risk and keeps the architecture change practical for a live product.

**Independent Test**: Select one business area for alignment, apply the ownership rules there first, and confirm the rest of the app can continue functioning while other areas are migrated later.

**Acceptance Scenarios**:

1. **Given** only one business area has been aligned to the new boundary rules, **When** the app continues operating, **Then** unaffected areas can remain temporarily unchanged without blocking the rollout.
2. **Given** a legacy workflow touches mixed ownership code, **When** it is reviewed for migration, **Then** the team can identify the target owning module and a staged path to move it there.

---

### User Story 6 - Protect offline-critical store workflows (Priority: P2)

As a store operator, I need checkout and other core local workflows to remain offline-capable during the module split so that the architecture change does not introduce operational outages at the point of sale.

**Why this priority**: The product is a local retail desktop app, so new architecture boundaries must not introduce avoidable runtime dependency on network availability.

**Independent Test**: Run an offline-critical workflow such as checkout with no network connectivity and verify it still completes inside the local application boundary.

**Acceptance Scenarios**:

1. **Given** the device has no network connectivity, **When** a cashier performs an offline-critical local workflow, **Then** the workflow still completes using local application capabilities.
2. **Given** a platform module participates in an offline-critical workflow, **When** the workflow is reviewed, **Then** it does not force a network dependency into the local execution path.

### Edge Cases

- What happens when one business workflow spans several modules and ownership is disputed?
- How does the system handle shared reference data that is read by multiple modules but owned by one module?
- What happens when a workflow needs coordinated changes across multiple module-owned aggregates in one user action?
- What happens to existing legacy logic that does not yet fit cleanly into one declared module?
- How are temporary legacy exceptions controlled so they do not become permanent cross-module shortcuts?
- What happens if a platform capability attempts to become the owner of business rules that belong to a business module?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST classify in-scope modules as either business modules or platform modules.
- **FR-002**: Business modules MUST be identified separately from platform modules in the ownership map.
- **FR-003**: Each business module MUST have one clear ownership boundary for its business rules, workflows, and important persisted business data.
- **FR-004**: Each platform module MUST have explicit ownership for its supporting capabilities and published interfaces, but it MUST NOT become the owner of another module's business rules.
- **FR-005**: Every important in-scope table or aggregate MUST have exactly one owning module.
- **FR-006**: Each module MUST own the contracts, events, and integration interfaces it publishes to other modules.
- **FR-007**: A module MUST NOT directly use another module's internal repositories, private entities, or internal business logic.
- **FR-008**: Cross-module collaboration MUST occur through explicit contracts, application services, or published events.
- **FR-009**: Cross-module writes MUST go through the owning module's declared coordination path rather than bypassing ownership boundaries.
- **FR-010**: The application MUST remain one desktop application from the end-user perspective.
- **FR-011**: The application MUST continue to operate against one shared local database for normal business workflows.
- **FR-012**: The modular-monolith split MUST NOT require modules to be deployed as separate network services.
- **FR-013**: Existing end-to-end workflows that span multiple modules MUST continue to complete successfully under the new boundary model.
- **FR-014**: The module boundary model MUST support incremental adoption so modules can be aligned in stages instead of through one full rewrite.
- **FR-015**: New business logic MUST be assigned to an owning module before it is added to the system.
- **FR-016**: Legacy code that does not yet fit a clean module boundary MUST have a documented temporary owner and target module.
- **FR-017**: Temporary legacy exceptions MUST include a defined migration path and MUST NOT be treated as permanent architecture.
- **FR-018**: Offline-critical workflows MUST remain local and MUST NOT gain mandatory network dependencies because of the module split.
- **FR-019**: Shared or cross-cutting concerns that do not belong to a business module MUST have explicit documented ownership before new dependencies are introduced.

### Key Entities *(include if feature involves data)*

- **Business Module**: A bounded domain area that owns business capabilities, business rules, and primary business data for that area.
- **Platform Module**: A supporting area that provides reusable capabilities without becoming the owner of another module's business rules.
- **Module Contract**: The declared input, output, and collaboration agreement another module is allowed to rely on.
- **Owned Table or Aggregate**: An important persisted structure assigned to exactly one owning module for business-rule enforcement.
- **Cross-Module Workflow**: A business operation that requires more than one module to participate while preserving explicit ownership boundaries.
- **Ownership Map**: The documented assignment of each business capability and important table or aggregate to exactly one primary module.
- **Legacy Exception**: A temporary documented exception where code remains outside its target boundary during staged migration.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of in-scope business capabilities are mapped to exactly one owning module.
- **SC-002**: 100% of important in-scope tables and aggregates are mapped to exactly one owning module.
- **SC-003**: A maintainer can identify the owning module for a reported defect or requested enhancement within 5 minutes using the feature ownership artifacts.
- **SC-004**: No new direct internal cross-module dependency is introduced within the migrated scope.
- **SC-005**: At least one representative end-to-end workflow spanning Sales, Inventory, Tax, and Telemetry is documented with explicit module responsibilities and no ambiguous ownership.
- **SC-006**: 100% of documented temporary legacy exceptions include a temporary owner and an exit plan.
- **SC-007**: Store staff continue using the product as one desktop application with no additional service setup for core daily workflows.
- **SC-008**: At least one offline-critical local workflow remains fully executable with no network dependency introduced by the boundary model.

## Assumptions

- The existing retail application already contains the listed business and platform areas, even if their current code is not yet cleanly separated.
- The product will remain a single installed desktop application rather than being split into separately deployed services.
- A single shared local SQLite database remains an intentional operational constraint for this application.
- Some legacy code will require transitional ownership decisions before it fully conforms to the new boundary model.
- Reporting is treated as a business-facing module because it serves business use cases even if it depends on data published by other modules.
