# Research Findings: Modular Monolith Boundaries

## Decision 1: Distinguish business modules from platform modules

- **Decision**: Treat Sales, Products, Inventory, Users/Auth, Tax, and Reporting as business modules, and treat Telemetry, Sync, Settings, and Migrations as platform modules.
- **Rationale**: Business modules own business rules and primary business data. Platform modules provide supporting capabilities and must not become owners of another module's business rules.
- **Alternatives considered**:
  - Use one flat module list for all concerns. Rejected because it hides the difference between domain ownership and supporting infrastructure.

## Decision 2: Keep the modular monolith inside the current two-project solution

- **Decision**: Enforce module boundaries through folders, namespaces, ownership maps, and documented contracts inside `RetailStorePOS.Data` and `Nexill.RetailStorePOS`.
- **Rationale**: The constitution explicitly restricts the solution to two projects. The feature needs enforceable internal boundaries, not new assemblies or services.
- **Alternatives considered**:
  - Add one project per module. Rejected because it violates the Two-Layer Architecture principle.
  - Split modules into separate local services. Rejected because it adds operational complexity and threatens offline guarantees.

## Decision 3: Make ownership explicit for important data assets

- **Decision**: Assign every important in-scope table or aggregate exactly one owning module and require non-owning modules to go through published contracts.
- **Rationale**: A modular monolith is not enforceable if persisted business data can be updated from multiple internal directions without one owner.
- **Alternatives considered**:
  - Allow shared repository ownership for convenience. Rejected because it recreates hidden coupling and bypasses business rules.

## Decision 4: Define allowed cross-module coordination patterns

- **Decision**: Allow cross-module collaboration only through explicit commands, queries, and published events owned by the target module.
- **Rationale**: This preserves in-process performance while making dependencies visible and reviewable.
- **Alternatives considered**:
  - Allow direct repository-to-repository access. Rejected because it makes ownership unenforceable.
  - Require asynchronous messaging for every module interaction. Rejected because the application is local-first, single-process, and must keep checkout fast.

## Decision 5: Keep checkout and other local store workflows offline-critical

- **Decision**: Treat checkout, local product lookup, tax calculation, and receipt creation as offline-critical workflows that must remain fully local.
- **Rationale**: The constitution requires full local capability with zero network connectivity. The module split must not add sync or telemetry dependencies into the local execution path.
- **Alternatives considered**:
  - Allow sync or license checks inline in the checkout path. Rejected because it violates Local-First Data Sovereignty and risks retail downtime.

## Decision 6: Use a documented legacy exception model for staged migration

- **Decision**: Maintain a legacy exception register that records a temporary owner, target module, reason, and exit plan for code not yet moved into its final boundary.
- **Rationale**: The current repository already contains cross-cutting folders like `Services/` and `ViewModels/`. Migration must be staged, but temporary exceptions need explicit control.
- **Alternatives considered**:
  - Leave mixed ownership code undocumented until migration is complete. Rejected because it hides risk and creates permanent exceptions.

## Decision 7: Restore runtime baseline compliance inside this feature

- **Decision**: Retarget the solution to the constitution-required `.NET 9+ / WinUI 3` baseline as mandatory foundational work inside this feature before user-story implementation proceeds.
- **Rationale**: The constitution marks the runtime baseline as non-negotiable, so the plan cannot claim a passing gate while explicitly deferring it. Treating the uplift as foundational compliance work keeps the conflict visible and actionable.
- **Alternatives considered**:
  - Defer the runtime uplift to a separate future platform feature. Rejected because it leaves the feature plan in explicit conflict with a non-negotiable constitution rule.
