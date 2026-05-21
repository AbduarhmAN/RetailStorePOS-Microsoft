# Research Findings: App Lifecycle Telemetry and Startup Migration

## Confirmed Local Architecture

- Local SQLite initialization is performed in `DatabaseInitializer.Initialize(...)` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:7).
- Existing local telemetry tables already exist:
  - `telemetry_outbox` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:84)
  - `installation_events` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:99)
  - `settings` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:79)
- Startup currently runs initialization off the UI thread during splash in [MainWindow.xaml.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/MainWindow.xaml.cs:73).
- Splash UI already exists with:
  - status title in [MainWindow.xaml](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/MainWindow.xaml:172)
  - status detail in [MainWindow.xaml](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/MainWindow.xaml:177)
  - progress bar in [MainWindow.xaml](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/MainWindow.xaml:200)

## Confirmed Current Telemetry Flow

- Launch is logged by `LogAppLaunchAsync()` in [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:282).
- Close is logged by `LogAppClosedAsync()` in [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:256), triggered from window close in [App.xaml.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/App.xaml.cs:79).
- Local installation-event insert uses `INSERT OR IGNORE` in [InstallationEventRepository.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Repositories/InstallationEventRepository.cs:17), which is good for local idempotency by event id.
- Outbox queue processing already exists in [TelemetryOutboxRepository.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Repositories/TelemetryOutboxRepository.cs:44) and [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:507).
- Remote installation-event sync already uses `on_conflict=id` in [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:467).

## Confirmed Weaknesses In Current Telemetry

- Local event insert and outbox enqueue are not one transaction in [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:453).
- Startup currently deletes all queued telemetry in [LoginRuntime.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/LoginRuntime.cs:79). This is incompatible with durable offline-first sync.
- Install state is currently stored in `preferences.json` through `ILocalPreferencesService` in [ILocalPreferencesService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/ILocalPreferencesService.cs:32), not in SQLite.

## Confirmed Activity Signals

- Strong signal:
  - `sales.created_at` from [SaleRepository.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/SaleRepository.cs:46)
- Potential strong signal if action is meaningful:
  - `audit_logs.created_at` from [AuditLogRepository.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Repositories/AuditLogRepository.cs:21)
- Weak or misleading as primary crash-boundary signals:
  - `products.last_sale_at` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:42)
  - `products.purchased_at` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:41)

## Confirmed Startup Migration Behavior

- Migrations are run on every startup by `RunMigrations(connection)` in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:405).
- Current migration style is additive-first using `ColumnExists(...)` checks in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:577).
- Current startup migration path already contains backfill updates:
  - `purchased_at` backfill in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:517)
  - `tax_group_id` backfill in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:550)
- These backfills show that startup migration cost is already nontrivial and should not remain an undifferentiated “run everything on startup” model.

## Confirmed Splash Behavior

- The splash progress bar is currently synthetic, driven by a timer in [MainWindow.xaml.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/MainWindow.xaml.cs:65), not by actual migration progress.
- This means any real migration progress integration must add explicit reporting from the migration engine.

## Capability Probe Findings

- CPU core count is already read in telemetry via `Environment.ProcessorCount` in [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:655).
- The current RAM logic in [TelemetryService.cs](/E:/Projects/Retail_Store/V/1.4.0/Nexill.RetailStorePOS/Services/TelemetryService.cs:634) is explicitly described as an approximation for telemetry bucketing, not a proven migration safety signal.
- Numeric thresholds for “safe to auto-run” were not found in the codebase and require confirmation/benchmarking.

## Decisions Derived From Source

- Lifecycle telemetry should remain install/app scoped, not person scoped.
- Existing local tables should be reused for rollout 1.
- Startup-safe migrations and heavy migrations must be separated by explicit classification.
- Remote schema details and exact Supabase migration mechanics require source confirmation.
