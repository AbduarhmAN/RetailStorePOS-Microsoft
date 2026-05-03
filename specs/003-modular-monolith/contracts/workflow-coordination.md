# Workflow Coordination Contract

## Checkout Completion

| Field | Value |
|-------|-------|
| Coordinator | Sales |
| Participating Modules | Products, Inventory, Tax, Users/Auth, Telemetry |
| Offline-Critical | Yes |
| Read Path | Sales reads product, tax, and user data through published owner contracts |
| Write Path | Sales records the sale through owner-approved write paths; Inventory adjusts stock through its owner path; Telemetry records optional lifecycle events asynchronously |
| Network Dependency | Not allowed |

## Product Maintenance

| Field | Value |
|-------|-------|
| Coordinator | Products |
| Participating Modules | Inventory, Tax, Reporting |
| Offline-Critical | No |
| Read Path | Products can consume tax or inventory reference data through published contracts |
| Write Path | Product master data changes remain owned by Products; downstream modules react through explicit outputs |
| Network Dependency | Optional only for Sync, never required for local save |

## Reporting Refresh

| Field | Value |
|-------|-------|
| Coordinator | Reporting |
| Participating Modules | Sales, Inventory, Tax, Telemetry |
| Offline-Critical | No |
| Read Path | Reporting uses published outputs and read models from owner modules |
| Write Path | Reporting must not write source-of-truth sales, product, inventory, or tax data |
| Network Dependency | Not required for local reporting |

## Sync Push

| Field | Value |
|-------|-------|
| Coordinator | Sync |
| Participating Modules | Sales, Products, Inventory, Users/Auth, Tax, Telemetry, Settings |
| Offline-Critical | No |
| Read Path | Sync consumes publishable records or outbox entries from owner modules |
| Write Path | Sync updates only its own operational state and never becomes the owner of source-of-truth business records |
| Network Dependency | Optional runtime-only capability |

## Legacy Transition Review

| Field | Value |
|-------|-------|
| Coordinator | Target business or platform module owner |
| Participating Modules | Any module touched by the current mixed implementation |
| Offline-Critical | Depends on workflow being migrated |
| Read Path | Review current access paths and record which are temporary exceptions |
| Write Path | Replace hidden direct access with explicit owner contracts before closing the exception |
| Network Dependency | Not allowed to increase compared with the original workflow |
