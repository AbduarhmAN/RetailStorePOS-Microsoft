# Workflow Trace

This document captures the explicit contract path for the cross-module workflows named in the feature contracts.

## Checkout Completion

| Field | Value |
|---|---|
| Coordinator | Sales |
| Participating Modules | Products, Inventory, Tax, Users/Auth, Telemetry |
| Offline-Critical | Yes |
| Contract Surface | `checkout.products.lookup`, `checkout.tax.resolve`, `checkout.sales.complete`, `inventory.stock-adjust`, `telemetry.lifecycle-event` |
| Final Read Sequence | Checkout UI resolves catalog lookup through `CheckoutWorkflowContract.ProductLookupQuery`, then reads tax categories and tax groups through `CheckoutWorkflowContract.TaxResolutionQuery`. |
| Final Write Sequence | Sales persists the receipt through `CheckoutWorkflowContract.SaleCompletionCommand`, Inventory applies stock movement through `InventoryWorkflowContract.InventoryStockAdjustmentCommand`, and Telemetry queues optional lifecycle events through `TelemetryPlatformContract.LifecycleEventCommand`. |
| Touchpoints | `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs`, `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs`, `RetailStorePOS.Data/Modules/Inventory/InventorySaleWriter.cs`, `Nexill.RetailStorePOS/Services/TelemetryService.cs` |

## Offline-Critical Store Path

| Field | Value |
|---|---|
| Workflow | Offline checkout and receipt creation |
| Launch Path | `App.OnLaunched` creates or reuses one local window, and `SqliteConnectionFactory.OpenConnection` opens only the normalized local SQLite path. |
| Product Lookup Path | `CheckoutPage` resolves `checkout.products.lookup`, and `ProductSearchService` reads the in-memory index built from `ProductRepository.GetAllUnbounded()` with no network dependency. |
| Tax Calculation Path | Checkout resolves `checkout.tax.resolve` through local tax repositories before totals are committed. |
| Sale Persistence Path | `SaleRepository.CreateSale` writes `sales` and `sale_items` in one local SQLite transaction through `checkout.sales.complete`. |
| Inventory Decrement Path | `InventorySaleWriter.ApplySaleDecrement` updates stock inside the same SQLite transaction through `inventory.stock-adjust`. |
| Receipt Creation Path | Receipt number allocation and persisted sale lines happen locally before transaction commit; no remote service is required for receipt creation. |
| Optional Side Effects | Telemetry may queue lifecycle payloads locally in `telemetry_outbox`, but remote delivery stays optional and asynchronous. |
| Failure Tolerance | Missing network connectivity must not block checkout launch, product lookup, tax resolution, receipt creation, sale persistence, or inventory decrement. |
| Touchpoints | `Nexill.RetailStorePOS/App.xaml.cs`, `RetailStorePOS.Data/SqliteConnectionFactory.cs`, `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs`, `Nexill.RetailStorePOS/Modules/Products/ProductSearchService.cs`, `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs`, `RetailStorePOS.Data/Modules/Inventory/InventorySaleWriter.cs`, `Nexill.RetailStorePOS/Services/TelemetryService.cs`, `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs` |

## Product Maintenance

| Field | Value |
|---|---|
| Coordinator | Products |
| Participating Modules | Inventory, Tax, Reporting |
| Offline-Critical | No |
| Contract Surface | `inventory.products.maintain`, `inventory.stock-adjust`, `inventory.tax-reference` |
| Final Read Sequence | Products UI resolves product search and catalog state through `InventoryWorkflowContract.ProductMaintenanceCommand`, then reads tax categories, rules, and groups through `InventoryWorkflowContract.TaxReferenceQuery`. |
| Final Write Sequence | Products writes master data through the Products owner path, Inventory-owned stock rules remain behind `InventoryWorkflowContract.InventoryStockAdjustmentCommand`, and Reporting stays downstream-only. |
| Touchpoints | `Nexill.RetailStorePOS/Views/ProductsPage.xaml.cs`, `RetailStorePOS.Data/Modules/Products/ProductRepository.cs`, `RetailStorePOS.Data/Modules/Inventory/InventoryStockRules.cs` |

## Reporting Refresh

| Field | Value |
|---|---|
| Coordinator | Reporting |
| Participating Modules | Sales, Inventory, Tax, Telemetry |
| Offline-Critical | No |
| Contract Surface | `reporting.dashboard.read-model`, `reporting.receipts.read-model`, `reporting.sales.read-model`, `reporting.inventory.read-model` |
| Final Read Sequence | Reports shell navigates through `ReportingWorkflowContract.DashboardReadModelQuery` or `ReportingWorkflowContract.ReceiptHistoryQuery`, then dashboard views read sales summaries through `ReportingWorkflowContract.SalesReadModelQuery` and stock state through `ReportingWorkflowContract.InventoryReadModelQuery`. |
| Final Write Sequence | Reporting remains read-only for sales, inventory, and tax source-of-truth records. Telemetry remains observer-only and does not own reporting state. |
| Touchpoints | `Nexill.RetailStorePOS/Views/ReportsPage.xaml.cs`, `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs` |

## Telemetry Capture

| Field | Value |
|---|---|
| Coordinator | Telemetry |
| Participating Modules | Settings, Telemetry |
| Offline-Critical | No |
| Contract Surface | `telemetry.lifecycle-event`, `telemetry.settings-state` |
| Final Read Sequence | `TelemetryService` reads runtime state through `TelemetryPlatformContract.SettingsStateQuery` and local preferences through `ILocalPreferencesService.PreferencesContractKey`. |
| Final Write Sequence | `TelemetryService` persists installation events locally, queues outbound payloads in `telemetry_outbox`, and flushes asynchronously using `TelemetryPlatformContract.BuildRequestUri`. |
| Touchpoints | `Nexill.RetailStorePOS/Services/TelemetryService.cs`, `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs`, `RetailStorePOS.Data/Repositories/InstallationEventRepository.cs` |

## Settings and Preferences

| Field | Value |
|---|---|
| Coordinator | Settings |
| Participating Modules | Settings, Telemetry |
| Offline-Critical | Yes |
| Contract Surface | `settings.read`, `settings.write`, `settings.local-preferences` |
| Final Read Sequence | App runtime and Telemetry read persisted settings through `SettingsPlatformContract.SettingsReadQuery` and local preferences through `SettingsPlatformContract.LocalPreferencesFileContract`. |
| Final Write Sequence | `SettingsRepository` owns writes to the `settings` table, and `LocalPreferencesService` owns writes to the local `preferences.json` file via `SettingsPlatformContract.NormalizeLocalPreferencesPath`. |
| Touchpoints | `Nexill.RetailStorePOS/Services/ILocalPreferencesService.cs`, `Nexill.RetailStorePOS/Services/LocalPreferencesService.cs`, `RetailStorePOS.Data/SettingsRepository.cs` |

## Sync Push

| Field | Value |
|---|---|
| Coordinator | Sync |
| Participating Modules | Sales, Products, Inventory, Users/Auth, Tax, Telemetry, Settings |
| Offline-Critical | No |
| Contract Surface | `sync.owner-exports`, `sync.operational-write` |
| Final Read Sequence | Sync consumes owner-published exports and outbox entries only. |
| Final Write Sequence | Sync writes only sync-owned operational state and never owns business source-of-truth data. |
| Touchpoints | `RetailStorePOS.Data/Modules/Contracts/SyncWorkflowContract.cs`, optional runtime sync integration paths |

## Migrations

| Field | Value |
|---|---|
| Coordinator | Migrations |
| Participating Modules | Migrations |
| Offline-Critical | Startup-safe path only |
| Contract Surface | `migrations.startup-safe`, `migrations.maintenance` |
| Final Read Sequence | `DatabaseInitializer` normalizes the local database path through `MigrationPlatformContract.NormalizeDatabasePath` and classifies each migration step through `MigrationPlatformContract.ShouldRunInStartupPath`. |
| Final Write Sequence | Startup-safe migrations run in the launch path, maintenance migrations run in the maintenance path, and integrity validation is enforced for heavy backfill steps through `MigrationPlatformContract.ShouldValidateBeforeCommit`. |
| Touchpoints | `RetailStorePOS.Data/Class1.cs`, `RetailStorePOS.Data/MigrationModel.cs` |

## Legacy Transition Review

| Field | Value |
|---|---|
| Coordinator | Target module owner for current exception |
| Participating Modules | Any module touched by mixed implementation |
| Offline-Critical | Depends on migrated workflow |
| Contract Surface | `specs/003-modular-monolith/legacy-exceptions.md` plus owner contract for the workflow being closed |
| Final Read Sequence | Review current access paths, confirm temporary owner, and map the exit path to an explicit owner contract before code moves. |
| Final Write Sequence | Close the exception only after the hidden cross-module access is replaced by the owning module's declared contract. |
| Touchpoints | `Services/` and `ViewModels/` transitional areas plus owner-specific contracts |
