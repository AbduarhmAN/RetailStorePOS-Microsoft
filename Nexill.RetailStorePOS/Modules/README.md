# WinUI Modules

This folder is the UI-side anchor for modular-monolith boundaries inside `Nexill.RetailStorePOS`.

## Purpose

- Make module ownership visible in the WinUI app without adding new projects.
- Move page, code-behind, service, and workflow orchestration toward the owning module.
- Keep one desktop app experience while internal module boundaries become explicit.

## Boundary Rules

- UI remains inside the existing WinUI project.
- Module-facing UI logic should align with the owning business or platform module.
- Cross-module coordination should rely on published contracts from the data layer.
- Transitional `Services/` and `ViewModels/` usage should be tracked as legacy exceptions until rehomed.

## Initial Module Set

### Business Modules

- Sales
- Products
- Inventory
- Users/Auth
- Tax
- Reporting

### Platform Modules

- Telemetry
- Sync
- Settings
- Migrations
