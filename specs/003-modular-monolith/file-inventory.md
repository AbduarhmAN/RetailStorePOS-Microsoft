# File-to-Module Inventory

This inventory captures the current placement of in-scope files after Phase 4 ownership alignment.

| Current Path | Owning Module | Concern Type | Current Location Status | Planned Destination or Role | Notes |
|---|---|---|---|---|---|
| `RetailStorePOS.Data/Modules/Products/Product.cs` | Products | Business model | Module-aligned path | Product master model with Inventory-delegated stock rules | Product namespace is aligned; stock-rule properties delegate to Inventory helpers. |
| `RetailStorePOS.Data/Modules/Products/ProductRepository.cs` | Products | Business repository | Module-aligned path | Product master-data owner path | Uses Inventory-owned helper for stock-state columns. |
| `RetailStorePOS.Data/Modules/Products/ProductImportService.cs` | Products | Business service | Module-aligned path | Import stays owned by Products | Catalog import remains product-owned. |
| `RetailStorePOS.Data/Modules/Products/ProductExportService.cs` | Products | Business service | Module-aligned path | Export stays owned by Products | Catalog export remains product-owned. |
| `RetailStorePOS.Data/Modules/Sales/Sale.cs` | Sales | Business aggregate | Module-aligned path | Sales aggregate root | Receipt and line ownership remain in Sales. |
| `RetailStorePOS.Data/Modules/Sales/SaleItem.cs` | Sales | Business aggregate item | Module-aligned path | Sales aggregate child | Sale line items stay sales-owned. |
| `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs` | Sales | Business repository | Module-aligned path | Sale owner path | Persists sales data and delegates stock decrement to Inventory. |
| `RetailStorePOS.Data/Modules/Inventory/InventoryStockRules.cs` | Inventory | Business contract/helper | New module-aligned path | Inventory stock-state owner path | Owns stock thresholds, totals, and stock-state parameter mapping. |
| `RetailStorePOS.Data/Modules/Inventory/InventorySaleWriter.cs` | Inventory | Business write path | New module-aligned path | Inventory decrement owner path | Owns sale-driven stock reduction. |
| `RetailStorePOS.Data/Modules/Tax/TaxCategory.cs` | Tax | Business model | Module-aligned path | Tax owner model | Tax category definition remains tax-owned. |
| `RetailStorePOS.Data/Modules/Tax/TaxCategoryRepository.cs` | Tax | Business repository | Module-aligned path | Tax owner write path | Tax category writes stay tax-owned. |
| `RetailStorePOS.Data/Modules/Tax/TaxAuthorityRepository.cs` | Tax | Business repository | Module-aligned path | Tax owner write path | Tax authority writes stay tax-owned. |
| `RetailStorePOS.Data/Modules/Tax/TaxRuleRepository.cs` | Tax | Business repository | Module-aligned path | Tax owner write path | Tax rule writes stay tax-owned. |
| `RetailStorePOS.Data/Modules/Tax/TaxGroupRepository.cs` | Tax | Business repository | Module-aligned path | Tax owner write path | Group/rule composition stays tax-owned. |
| `RetailStorePOS.Data/Repositories/UserRepository.cs` | Users/Auth | Business repository | Transitional path, module-aligned namespace | Users/Auth owner path | File remains in `Repositories`, but namespace is `RetailStorePOS.Data.Modules.UsersAuth`. |
| `RetailStorePOS.Data/SettingsRepository.cs` | Settings | Platform repository | Transitional path, module-aligned namespace | Settings owner path | File remains at data-project root, but namespace is `RetailStorePOS.Data.Modules.Settings`. |
| `RetailStorePOS.Data/Repositories/InstallationEventRepository.cs` | Telemetry | Platform repository | Transitional path, module-aligned namespace | Telemetry owner path | Owns `installation_events` persistence. |
| `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs` | Telemetry | Platform repository | Transitional path, module-aligned namespace | Telemetry owner path | Owns `telemetry_outbox` persistence. |
| `RetailStorePOS.Data/MigrationModel.cs` | Migrations | Platform model | Transitional path, module-aligned namespace | Migration owner metadata | File remains at data-project root, but namespace is `RetailStorePOS.Data.Modules.Migrations`. |
| `RetailStorePOS.Data/Class1.cs` | Migrations | Platform bootstrap | Transitional path, module-aligned namespace | Migration/bootstrap owner path | Contains schema bootstrap and migration execution. |
| `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs` | Sales | UI workflow coordinator | View code-behind | Sales module-aligned UI entry point | Checkout consumes owner-published module paths. |
| `Nexill.RetailStorePOS/Views/ProductsPage.xaml.cs` | Products | UI workflow coordinator | View code-behind | Products module-aligned UI entry point | Product maintenance consumes product/tax owner paths. |
| `Nexill.RetailStorePOS/Views/TaxConfigurationPage.xaml.cs` | Tax | UI workflow coordinator | View code-behind | Tax module-aligned UI entry point | Tax admin consumes tax owner paths. |
| `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs` | Reporting | UI workflow coordinator | View code-behind | Reporting module-aligned UI entry point | Reporting remains read-only. |
| `Nexill.RetailStorePOS/Services/TelemetryService.cs` | Telemetry | Platform service | Transitional service | Telemetry platform module | Consumes telemetry and settings owner paths. |
| `Nexill.RetailStorePOS/Services/AuthService.cs` | Users/Auth | Business service | Transitional service | Users/Auth module | Consumes users owner path only. |
| `Nexill.RetailStorePOS/App.xaml.cs` | App shell | Window bootstrap | Single-window bootstrap path | Reuses existing `MainWindow` on relaunch and keeps one window host | No secondary desktop host introduced. |
| `Nexill.RetailStorePOS/MainWindow.xaml.cs` | App shell | Root window and navigation host | Single-window root host | Owns one `RootFrame` and one-time runtime bootstrap | Splash-to-app transition still happens inside same window. |
| `RetailStorePOS.Data/DatabasePaths.cs` | Shared local data path | Local SQLite path policy | Shared path helper | Normalizes one local `pos.db` path under app data | Keeps one local dataset target. |
| `RetailStorePOS.Data/SqliteConnectionFactory.cs` | Shared local data access | SQLite connection bootstrap | Shared connection factory | Opens shared-cache local SQLite connections against normalized path | Preserves single local DB boundary. |
| `Nexill.RetailStorePOS/Services/ILocalPreferencesService.cs` | Settings | In-process preferences contract | Service interface | Keeps preferences on local disk in-process | No network/settings host introduced. |
| `Nexill.RetailStorePOS/Services/LocalPreferencesService.cs` | Settings | In-process preferences implementation | Local JSON service | Uses normalized local `preferences.json` path | Preferences remain local and in-process. |
| `Nexill.RetailStorePOS/ViewModels/CheckoutViewModel.cs` | Sales | Transitional UI logic | View-model exception | Legacy exception until moved | Sales coordinator that consumes owner-approved settings, tax, product, and sale paths. |
| `Nexill.RetailStorePOS/ViewModels/UsersPageViewModel.cs` | Users/Auth | Transitional UI logic | View-model exception | Legacy exception until moved | Consumes users owner path only. |
| `Nexill.RetailStorePOS/ViewModels/ReportsViewModel.cs` | Reporting | Transitional UI logic | View-model exception | Legacy exception until moved | Read-only reporting transition. |
| `Nexill.RetailStorePOS/ViewModels/SettingsViewModel.cs` | Settings | Transitional UI logic | View-model exception | Legacy exception until moved | Uses settings owner path. |

## Owner-by-Asset Mapping

| Asset Key | Owning Module | Primary File Path | Approved Write Path | Notes |
|---|---|---|---|---|
| `products.product_master_fields` | Products | `RetailStorePOS.Data/Modules/Products/ProductRepository.cs` | `ProductRepository.Create` / `ProductRepository.Update` | Product writes delegate stock-state columns to Inventory helper. |
| `products.stock_state_fields` | Inventory | `RetailStorePOS.Data/Modules/Inventory/InventoryStockRules.cs` | `InventoryStockRules.BindOwnedProductWriteParameters`, `InventorySaleWriter.ApplySaleDecrement` | One owner for stock, thresholds, and sale-driven decrement. |
| `sales` and `sale_items` | Sales | `RetailStorePOS.Data/Modules/Sales/SaleRepository.cs` | `SaleRepository.CreateSale` | Sales aggregate owner path. |
| `users` | Users/Auth | `RetailStorePOS.Data/Repositories/UserRepository.cs` | `UserRepository.Create`, `UserRepository.Update`, `UserRepository.Deactivate` | Namespace aligned to `RetailStorePOS.Data.Modules.UsersAuth`. |
| `tax_categories` | Tax | `RetailStorePOS.Data/Modules/Tax/TaxCategoryRepository.cs` | `TaxCategoryRepository.Create`, `TaxCategoryRepository.Update` | Tax-owned definitions only. |
| `settings` | Settings | `RetailStorePOS.Data/SettingsRepository.cs` | `SettingsRepository.Set*` methods | Shared preference store remains settings-owned. |
| `telemetry_outbox` | Telemetry | `RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs` | `TelemetryOutboxRepository.Enqueue`, `MarkSent`, `MarkFailed` | Telemetry-owned operational queue. |
| `installation_events` | Telemetry | `RetailStorePOS.Data/Repositories/InstallationEventRepository.cs` | `InstallationEventRepository.TryInsert` | Telemetry-owned lifecycle history. |
| `schema migration state` | Migrations | `RetailStorePOS.Data/Class1.cs`, `RetailStorePOS.Data/MigrationModel.cs` | `DatabaseInitializer.Initialize`, migration-step execution | Migrations owns schema evolution only. |
| `reporting read models` | Reporting | `Nexill.RetailStorePOS/Views/ReportsDashboardPage.xaml.cs`, `Nexill.RetailStorePOS/ViewModels/ReportsViewModel.cs` | No source-of-truth write path | Reporting stays read-only. |

## Bootstrap Verification

- Current solution shape remains two projects: `RetailStorePOS.Data` and `Nexill.RetailStorePOS`.
- `Nexill.RetailStorePOS/App.xaml.cs` reuses the existing `MainWindow` instead of creating a second app window on relaunch.
- `Nexill.RetailStorePOS/MainWindow.xaml.cs` keeps one `RootFrame` host and guards one-time runtime bootstrap from `RootGrid.Loaded`.
- `RetailStorePOS.Data/DatabasePaths.cs` resolves one normalized local `pos.db` path under app data.
- `RetailStorePOS.Data/SqliteConnectionFactory.cs` opens shared local SQLite connections against the normalized path.
- `Nexill.RetailStorePOS/Services/ILocalPreferencesService.cs` and `LocalPreferencesService.cs` keep preferences on local disk in-process via `preferences.json`.
- `Nexill.RetailStorePOS/Nexill.RetailStorePOS.csproj` contains one WinUI executable and one project reference to `RetailStorePOS.Data`; no extra worker or service-host project exists.
- `RetailStorePOS.Data/RetailStorePOS.Data.csproj` remains a class library only; no extra executable or service-host bootstrap exists.
