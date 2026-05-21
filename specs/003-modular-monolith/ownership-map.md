# Ownership Map

This document maps in-scope modules, capabilities, and important data assets to one owning module.

## Module Classification

| Module | Type | Primary Responsibilities | Published Contracts | Notes |
|---|---|---|---|---|
| Sales | Business | Checkout flow, receipt lifecycle, sale persistence | `CheckoutWorkflowContract`, sale query outputs | Owns `sales`, `sale_items`, and receipt sequencing. |
| Products | Business | Product catalog, pricing source data, import/export ownership | Product maintenance and search entry points | Owns product master data, not stock movement rules. |
| Inventory | Business | Stock position, stock movement rules, availability thresholds | `InventoryWorkflowContract`, stock adjustment path | Owns stock-state columns and sale-driven decrement path. |
| Users/Auth | Business | User identity, staff access, local session authority | Local session and user-management entry points | Owns `users` and related authentication rules. |
| Tax | Business | Tax authorities, groups, categories, calculation rules | Tax administration and lookup entry points | Owns tax-definition data and tax admin writes. |
| Reporting | Business | Report composition and business-facing read models | Reporting refresh/read-model outputs | Consumes published reads only; no source-of-truth writes. |
| Telemetry | Platform | Lifecycle event capture, installation-event persistence, outbox publishing | `TelemetryOutboxContract`, lifecycle capture path | Owns telemetry-specific operational tables only. |
| Sync | Platform | Optional cloud synchronization orchestration | `SyncPushContract` | Consumes owner-published records only. |
| Settings | Platform | Application preferences, store configuration, local operator state | `SettingsStateContract` | Owns `settings` table and exposes settings reads/writes. |
| Migrations | Platform | Schema bootstrap, migration-step registry, startup-safe upgrades | `MigrationBootstrapContract` | Owns schema-transition logic and `user_version` advancement. |

## Capability Ownership

| Capability | Owning Module | Supporting Modules | Published Entry Point | Notes |
|---|---|---|---|---|
| Checkout completion | Sales | Products, Inventory, Tax, Users/Auth, Telemetry | `SaleRepository.CreateSale` via `CheckoutWorkflowContract` | Sales persists receipt; Inventory applies stock decrement through owner path. |
| Product maintenance | Products | Inventory, Tax, Reporting | `ProductRepository` and product search entry points | Products owns master catalog writes. |
| Stock movement and thresholds | Inventory | Products, Sales, Reporting | `InventoryStockRules`, `InventorySaleWriter` | Inventory owns stock-state rules even though data lives in `products`. |
| User and session administration | Users/Auth | Settings, Reporting | `UserRepository`, `AuthService` | Users/Auth owns staff identity and local access rules. |
| Tax administration | Tax | Products, Sales, Reporting | `TaxCategoryRepository`, tax rule/group repositories | Tax owns tax-definition writes. |
| Reporting refresh | Reporting | Sales, Products, Inventory, Tax, Telemetry | Reporting view-model/read-model outputs | Reporting stays read-only. |
| Telemetry capture and delivery | Telemetry | Settings | `InstallationEventRepository`, `TelemetryOutboxRepository` | Telemetry owns outbox and installation-event persistence. |
| Local settings and store preferences | Settings | Telemetry, Users/Auth, Sales | `SettingsRepository` | Settings owns preference storage and exposes safe reads/writes. |
| Schema initialization and upgrades | Migrations | All modules at startup | `DatabaseInitializer`, `MigrationStep` | Migrations owns schema evolution paths only. |
| Optional sync push | Sync | Sales, Products, Inventory, Users/Auth, Tax, Telemetry, Settings | Sync contract only | Sync never becomes source of truth. |

## Important Tables and Aggregates

| Asset | Owning Module | Access Pattern | Cross-Module Consumers | Notes |
|---|---|---|---|---|
| `products.product_master_fields` | Products | `ProductRepository` owner path | Inventory, Sales, Reporting | Covers SKU, name, barcode, unit, price, cost, tax references, and metadata. |
| `products.stock_state_fields` | Inventory | `InventoryStockRules` and `InventorySaleWriter` owner path | Products, Sales, Reporting | Covers quantity, thresholds, purchased-at, and last-sale-at fields. |
| `sales` | Sales | `SaleRepository` owner path | Reporting, Telemetry, Sync | Sale header writes stay owned by Sales. |
| `sale_items` | Sales | `SaleRepository` aggregate write path | Reporting, Inventory, Tax | Item lines belong to the Sales aggregate. |
| `receipt_sequence` | Sales | `SaleRepository` owner path | Reporting | Receipt-number sequencing is sales-owned operational data. |
| `users` | Users/Auth | `UserRepository` owner path | Sales, Settings, Reporting | Staff identity and permissions. |
| `sessions` | Users/Auth | Auth/session owner path | Users/Auth | Session authority remains local. |
| `tax_categories` | Tax | `TaxCategoryRepository` owner path | Products, Sales | Tax category writes are tax-owned only. |
| `tax_authorities` | Tax | `TaxAuthorityRepository` owner path | Tax admin flows | Tax authority definitions. |
| `tax_rules` | Tax | `TaxRuleRepository` owner path | Sales, Products, Reporting | Calculation rules and history stay tax-owned. |
| `tax_groups` and `tax_group_rules` | Tax | `TaxGroupRepository` owner path | Products, Sales, Reporting | Group-to-rule composition stays tax-owned. |
| `settings` | Settings | `SettingsRepository` owner path | Sales, Users/Auth, Telemetry | Shared preference table remains settings-owned. |
| `telemetry_outbox` | Telemetry | `TelemetryOutboxRepository` owner path | Telemetry background sync | Telemetry operational outbox only. |
| `installation_events` | Telemetry | `InstallationEventRepository` owner path | Telemetry background sync | Lifecycle/install event history only. |
| `schema migration state` | Migrations | `DatabaseInitializer` and `MigrationStep` owner path | All modules at startup | Includes `PRAGMA user_version` and migration-step registry. |
| `reporting read models` | Reporting | Reporting helper/view-model outputs | Sales, Products, Inventory, Tax | Local dashboard and report summaries are read-only derivatives. |
