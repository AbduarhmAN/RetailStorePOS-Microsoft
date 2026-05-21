# Data Modules

This folder is the data-layer anchor for modular-monolith boundaries inside `RetailStorePOS.Data`.

## Purpose

- Keep business and platform modules explicit inside the existing data project.
- Group repositories, models, contracts, and supporting logic under the owning module.
- Make ownership, published interfaces, and cross-module boundaries reviewable.

## Boundary Rules

- Business modules own business rules, workflows, and important persisted data.
- Platform modules own supporting capabilities and published interfaces only.
- Cross-module access must go through published contracts, not another module's private repositories or internal entities.
- The solution remains two projects only: `RetailStorePOS.Data` and `Nexill.RetailStorePOS`.

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
