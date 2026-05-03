# Legacy Exception Register

This document tracks temporary mixed-ownership areas that do not yet fit the target module boundaries.

## Rules

- Every exception must have a temporary owner.
- Every exception must have a target module.
- Every exception must have an exit plan.
- Exceptions are temporary and must not become permanent shortcuts.

## Active Exceptions

| ID | File or Area | Current Responsibility | Temporary Owner | Target Module | Exit Plan | Status | Notes |
|---|---|---|---|---|---|---|---|
| LE-001 | `Nexill.RetailStorePOS/Modules/Products/ProductSearchService.cs` | Product catalog search index and product lookup orchestration for checkout and catalog flows | Products | Products | Closed by moving the executable type into `Modules/Products/`; keep old `Services/ProductSearchService.cs` only as a non-executing migration marker until final cleanup | Closed | First exception closed in Phase 7 |
| LE-002 | `Nexill.RetailStorePOS/Services/AuthService.cs` | Login, logout, session refresh, bootstrap admin setup handoff, and audit-coupled authentication flow | Users/Auth | Users/Auth | Split session lifecycle and credential validation into a Users/Auth module app service, then remove direct `Services/` ownership | Open | Still coordinates repository + audit concerns from generic service layer |
| LE-003 | `Nexill.RetailStorePOS/Services/TelemetryService.cs` | Lifecycle event orchestration, queue/flush coordination, install tracking, remote delivery, and runtime-state reads | Telemetry | Telemetry | Separate telemetry capture coordinator from transport-specific delivery plumbing, keep outbox/repository ownership inside Telemetry module, then retire generic `Services/` placement | Open | Phase 6 added contracts but service still lives in transitional area |
| LE-004 | `Nexill.RetailStorePOS/Services/LocalPreferencesService.cs` | Local preferences file load/save, event fan-out, and runtime preference synchronization | Settings | Settings | Move file-backed preference persistence behind a Settings module app service and keep only UI-facing abstraction at runtime boundary if still needed | Open | Phase 6 added settings contracts; placement still transitional |
| LE-005 | `Nexill.RetailStorePOS/ViewModels/CheckoutViewModel.cs` | Checkout orchestration across sales completion, product lookup, settings, local preferences, inventory alerts, and receipt output | Sales | Sales | Peel search/catalog coordination and receipt-completion workflow into Sales-owned coordinators, then leave the view model as UI state and command mapping only | Open | Sales is primary owner; file still mixes multiple module interactions |
| LE-006 | `Nexill.RetailStorePOS/ViewModels/ReportsViewModel.cs` | Reporting filters, receipt reprint, X-report generation, and transaction projection state | Reporting | Reporting | Move report-loading and export logic into Reporting module coordinators/read-model services, then reduce view model to presentation state | Open | Reporting owner is clear; implementation still mixed in view-model layer |
| LE-007 | `Nexill.RetailStorePOS/ViewModels/SettingsViewModel.cs` | Store settings, tax options, local preferences, and role-aware settings navigation | Settings | Settings | Extract settings persistence and tax-configuration coordination into Settings-owned module services, then keep VM focused on screen state | Open | Mixes store settings, preferences, and tax management concerns |
| LE-008 | `Nexill.RetailStorePOS/ViewModels/UsersPageViewModel.cs` | User management UI state, unlock flow, permission editing, and repository-backed save/deactivate actions | Users/Auth | Users/Auth | Move user-edit commands and permission policy orchestration into Users/Auth coordinators, then keep VM focused on selection and form state | Open | Still performs direct user-management orchestration in VM |
