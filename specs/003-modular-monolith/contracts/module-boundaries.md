# Module Boundary Contract

## Module Taxonomy

| Module | Type | Primary Responsibility | Must Not Own |
|--------|------|------------------------|--------------|
| Sales | Business | Checkout flow, cart completion, receipt-producing sale lifecycle | Product master data, tax rule definitions, sync policy |
| Products | Business | Product catalog, pricing source data, import/export ownership | Sale finalization, telemetry delivery |
| Inventory | Business | Stock position, stock movements, availability rules | Tax logic, user authentication |
| Users/Auth | Business | User identity, staff access, local session authority | Product or sale business rules |
| Tax | Business | Tax authorities, groups, categories, calculation rules | Product catalog ownership, sync state |
| Reporting | Business | Report composition and business-facing read models | Source-of-truth transaction writes |
| Telemetry | Platform | Lifecycle and usage event capture, outbox publishing | Business transaction ownership |
| Sync | Platform | Optional cloud synchronization orchestration | Local source-of-truth business data |
| Settings | Platform | Application and operator preference storage | Sales, product, or tax business rules |
| Migrations | Platform | Schema evolution and startup-safe data transitions | Day-to-day business workflow ownership |

## Allowed Dependency Rules

1. Business modules may depend on their own internal models, repositories, and contracts.
2. Platform modules may expose supporting contracts, but business modules remain the owners of business rules.
3. A module may consume another module's published commands, queries, or events.
4. Reporting may consume published outputs from business modules but may not write source-of-truth business transactions.

## Forbidden Direct Access

1. A module must not directly use another module's private repository implementation.
2. A module must not directly modify another module's owned table or aggregate.
3. A platform module must not absorb ownership of a business rule because it is convenient to host there.
4. New code must not create hidden cross-module shortcuts through generic helper classes, view models, or UI services.

## Ownership Expectations

1. Every important business capability has exactly one primary owner.
2. Every important table or aggregate has exactly one primary owner.
3. Every cross-module write follows the owning module's declared contract.
4. Every legacy exception identifies a temporary owner and a target module.
