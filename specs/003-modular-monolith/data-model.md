# Data Model: Modular Monolith Boundary Governance

## Entity: BusinessModule

| Field | Type | Description |
|-------|------|-------------|
| `ModuleKey` | string | Stable identifier such as `sales` or `tax`. |
| `DisplayName` | string | Human-readable module name. |
| `OwnedCapabilities` | list | Business capabilities owned by the module. |
| `OwnedDataAssets` | list | Important tables or aggregates owned by the module. |
| `PublishedContracts` | list | Commands, queries, and events that other modules may use. |
| `OfflineCritical` | boolean | Whether the module participates in workflows that must remain local with no network dependency. |

**Validation Rules**

- `ModuleKey` must be unique.
- Every owned capability must map to exactly one business module.
- Every owned data asset must map to exactly one owning module.

## Entity: PlatformModule

| Field | Type | Description |
|-------|------|-------------|
| `ModuleKey` | string | Stable identifier such as `telemetry` or `sync`. |
| `DisplayName` | string | Human-readable module name. |
| `SupportingCapabilities` | list | Cross-cutting capabilities supplied by the module. |
| `PublishedContracts` | list | Explicit contracts other modules may consume. |
| `BusinessRulesOwned` | list | Must remain empty for rules owned by business modules. |

**Validation Rules**

- Platform modules cannot become the owner of another module's business rules.
- Platform modules may own their own operational state, but not a business module's source-of-truth rules.

## Entity: OwnedDataAsset

| Field | Type | Description |
|-------|------|-------------|
| `AssetKey` | string | Stable identifier for a table, aggregate, or reportable record set. |
| `AssetType` | enum | `Table`, `Aggregate`, `ReadModel`, or `OperationalState`. |
| `OwnerModuleKey` | string | Module responsible for write rules and lifecycle. |
| `PrimaryConsumers` | list | Modules that depend on the asset through published contracts. |
| `Notes` | string | Ownership or transition notes. |

**Validation Rules**

- Every important in-scope asset must have exactly one owner.
- Non-owning modules cannot write through internal shortcuts.

## Entity: ModuleContract

| Field | Type | Description |
|-------|------|-------------|
| `ContractKey` | string | Stable identifier for the published module contract. |
| `OwnerModuleKey` | string | Module that publishes and evolves the contract. |
| `ContractType` | enum | `Command`, `Query`, or `Event`. |
| `Purpose` | string | What the contract allows another module to do. |
| `Consumers` | list | Modules allowed to depend on it. |
| `OfflineAllowed` | boolean | Whether the contract participates in offline-critical workflows. |

**Validation Rules**

- Contracts must be published by the owning module.
- Contracts must not expose private repositories or internal entities directly.

## Entity: WorkflowBoundary

| Field | Type | Description |
|-------|------|-------------|
| `WorkflowKey` | string | Stable identifier such as `checkout-complete-sale`. |
| `CoordinatorModuleKey` | string | Module that owns the user-facing workflow. |
| `ParticipatingModules` | list | Modules that contribute through explicit contracts. |
| `OfflineCritical` | boolean | Whether the workflow must complete with no network connectivity. |
| `WriteSequence` | list | Ordered list of owner-approved write steps. |

**Validation Rules**

- A workflow has one coordinator module.
- Cross-module writes must follow the declared sequence.
- Offline-critical workflows must remain executable with local resources only.

## Entity: LegacyException

| Field | Type | Description |
|-------|------|-------------|
| `ExceptionKey` | string | Stable identifier for the temporary exception. |
| `CurrentLocation` | string | Current file, folder, or responsibility location. |
| `TemporaryOwnerModuleKey` | string | Module accountable while the exception exists. |
| `TargetModuleKey` | string | Intended final owner after migration. |
| `Reason` | string | Why the exception exists today. |
| `ExitPlan` | string | How the exception will be removed. |
| `Status` | enum | `Open`, `InMigration`, or `Closed`. |

**Validation Rules**

- Every legacy exception must have both a temporary owner and a target module.
- Open exceptions require an explicit exit plan.

## Relationships

- `BusinessModule` owns many `OwnedDataAsset` records.
- `PlatformModule` publishes many `ModuleContract` records.
- `WorkflowBoundary` references one coordinator and many participating modules.
- `LegacyException` links current implementation placement to target module ownership.
