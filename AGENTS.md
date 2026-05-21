# 1.3.3 Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-05-02

## Active Technologies
- C# / .NET 9.0, WinUI 3 on Windows App SDK + Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, Windows.Storage.Pickers, Windows.Graphics.Imaging; no new package required (006-product-pictures)
- SQLite local database plus local thumbnail files under `AppDataPaths` (006-product-pictures)
- C# on .NET 10.0 / WinUI 3 in the current repository baseline + Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, LiveChartsCore.SkiaSharpView.WinUI, CsvHelper, System.Security.Cryptography.ProtectedData, Windows.Storage.Pickers (008-readiness-dashboard-freshness)
- Local SQLite database under `AppDataPaths`; local JSON files for preferences/receipts; this feature adds local readiness artifacts and dashboard snapshot cache files under the same app-data root (008-readiness-dashboard-freshness)

- C# on .NET 9+ / WinUI 3 + Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, LiveChartsCore.SkiaSharpView.WinUI, CsvHelper, System.Security.Cryptography.ProtectedData

## Project Structure

```text
RetailStorePOS.Data/
├── Models/
├── Modules/
│   ├── Contracts/          # Cross-module coordination contracts
│   ├── Inventory/          # Stock rules, sale-driven decrement
│   ├── Migrations/         # Schema bootstrap scaffolds
│   ├── Products/           # Product master data, import/export
│   ├── Reporting/          # Read-model scaffolds
│   ├── Sales/              # Sale aggregate, receipt lifecycle
│   ├── Settings/           # Settings scaffolds
│   ├── Sync/               # Sync scaffolds
│   ├── Tax/                # Tax categories, rules, groups, authorities
│   ├── Telemetry/          # Telemetry scaffolds
│   └── UsersAuth/          # User/auth scaffolds
├── Repositories/           # Transitional: UserRepository, TelemetryOutbox, InstallationEvent
├── SqliteConnectionFactory.cs
├── DatabasePaths.cs
├── SettingsRepository.cs   # Settings-owned, namespace: Modules.Settings
├── Class1.cs               # Migrations-owned bootstrap
└── MigrationModel.cs       # Migrations-owned metadata

Nexill.RetailStorePOS/
├── Common/
├── Models/
├── Modules/
│   ├── Products/           # ProductSearchService (migrated from Services/)
│   ├── Inventory/          # Scaffolds
│   ├── Migrations/         # Scaffolds
│   ├── Reporting/          # Scaffolds
│   ├── Sales/              # Scaffolds
│   ├── Settings/           # Scaffolds
│   ├── Sync/               # Scaffolds
│   ├── Tax/                # Scaffolds
│   ├── Telemetry/          # Scaffolds
│   └── UsersAuth/          # Scaffolds
├── Services/               # Transitional: AuthService, TelemetryService, LocalPreferencesService
├── ViewModels/             # Transitional: CheckoutVM, ReportsVM, SettingsVM, UsersPageVM
└── Views/

specs/
└── 003-modular-monolith/   # Feature artifacts for modular-monolith boundaries
```

## Module Ownership

### Business Modules

| Module | Owner Of | Key Files |
|--------|----------|-----------|
| Sales | `sales`, `sale_items`, `receipt_sequence` | `Modules/Sales/SaleRepository.cs` |
| Products | Product master fields | `Modules/Products/ProductRepository.cs` |
| Inventory | Stock-state fields, decrement rules | `Modules/Inventory/InventoryStockRules.cs`, `InventorySaleWriter.cs` |
| Tax | `tax_categories`, `tax_authorities`, `tax_rules`, `tax_groups` | `Modules/Tax/TaxCategoryRepository.cs` |
| Users/Auth | `users`, `sessions` | `Repositories/UserRepository.cs` |
| Reporting | Read models (read-only) | Dashboard/receipt views |

### Platform Modules

| Module | Owner Of | Key Files |
|--------|----------|-----------|
| Telemetry | `telemetry_outbox`, `installation_events` | `Repositories/TelemetryOutboxRepository.cs` |
| Settings | `settings` table, local preferences | `SettingsRepository.cs` |
| Migrations | Schema state, `user_version` | `Class1.cs`, `MigrationModel.cs` |
| Sync | Sync operational state (optional) | `Modules/Contracts/SyncWorkflowContract.cs` |

## Cross-Module Contracts

All cross-module coordination goes through declared contracts in `RetailStorePOS.Data/Modules/Contracts/`:

- `CheckoutWorkflowContract` — Checkout: product lookup, tax resolution, sale completion
- `InventoryWorkflowContract` — Stock adjustments, product maintenance
- `ReportingWorkflowContract` — Dashboard and receipt read models
- `TelemetryPlatformContract` — Lifecycle events, settings state queries
- `SettingsPlatformContract` — Settings reads/writes, local preferences
- `SyncWorkflowContract` — Optional sync push
- `MigrationPlatformContract` — Schema bootstrap and maintenance paths

## Verification Workflow

1. **Ownership lookup**: Use `specs/003-modular-monolith/ownership-map.md` and `file-inventory.md` to identify the owning module for any change request (target: ≤ 5 minutes).
2. **Cross-module review**: Use `specs/003-modular-monolith/review-guardrails.md` to validate no forbidden cross-module access is introduced.
3. **Workflow trace**: Use `specs/003-modular-monolith/workflow-trace.md` to verify coordination paths for checkout, reporting, telemetry, settings, sync, and migrations.
4. **Legacy exceptions**: Check `specs/003-modular-monolith/legacy-exceptions.md` for open exceptions and their exit plans.
5. **Offline verification**: Confirm checkout and related local workflows complete with no network dependency.

## Commands

# Manual verification only for this feature plan; no build commands recorded here.

## Code Style

C# on .NET 9+ / WinUI 3: Follow standard conventions

## Recent Changes
- 008-readiness-dashboard-freshness: Added C# on .NET 10.0 / WinUI 3 in the current repository baseline + Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, LiveChartsCore.SkiaSharpView.WinUI, CsvHelper, System.Security.Cryptography.ProtectedData, Windows.Storage.Pickers
- 006-product-pictures: Added C# / .NET 9.0, WinUI 3 on Windows App SDK + Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite, Windows.Storage.Pickers, Windows.Graphics.Imaging; no new package required

- 003-modular-monolith: Modular-monolith boundary rollout — business/platform module separation, ownership maps, cross-module contracts, legacy exception tracking, offline-critical workflow protection

<!-- MANUAL ADDITIONS START -->
## Absolute Rules

- **NEVER run `dotnet build`, `dotnet restore`, or any full build/compile command.** This project takes too long to build. Verify changes through code inspection only.
<!-- MANUAL ADDITIONS END -->
