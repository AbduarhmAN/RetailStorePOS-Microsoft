# Project: Native AOT Fixes for UI Projects

## Architecture
- 4 remaining UI projects in the modular monolith structure: `RetailStorePOS.UI.Reporting`, `RetailStorePOS.UI.Settings`, `RetailStorePOS.UI.Tax`, `RetailStorePOS.UI.Platform`.
- Goal: Apply AOT fixes: replacing `{Binding}` with `{x:Bind}`, adding `x:DataType` to `DataTemplate`, making UI-bound model classes `partial`, safe SQLite aggregate parsing, and wrapping `async void` with `try-catch`. Ensuring no raw arrays (`T[]`) are bound, or applying item synchronization workarounds.

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| 1 | Reporting Fixes | `RetailStorePOS.UI.Reporting` | none | IN_PROGRESS (0e12d2ef-4ce5-42a4-acf0-f4447535bf1d) |
| 2 | Settings Fixes | `RetailStorePOS.UI.Settings` | none | IN_PROGRESS (02be10f0-730a-4e10-9277-f74b07e76407) |
| 3 | Tax Fixes | `RetailStorePOS.UI.Tax` | none | IN_PROGRESS (4f208da5-312d-4180-89e9-aadffe79ec7b) |
| 4 | Platform Fixes | `RetailStorePOS.UI.Platform` | none | IN_PROGRESS (1b407a05-5659-4129-8bb7-02f09de57c5b) |

## Interface Contracts
N/A (This is an internal refactoring to support Native AOT on existing projects).

## Code Layout
- `RetailStorePOS.UI.Reporting/`
- `RetailStorePOS.UI.Settings/`
- `RetailStorePOS.UI.Tax/`
- `RetailStorePOS.UI.Platform/`
