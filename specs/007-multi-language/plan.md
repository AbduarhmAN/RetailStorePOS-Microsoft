# Implementation Plan: Multi-Language Support (i18n)

**Branch**: `007-multi-language` | **Date**: 2026-04-28 | **Spec**: [spec.md](file:///e:/Projects/Retail_Store/V/1.3.3/specs/007-multi-language/spec.md)
**Input**: Feature specification from `/specs/007-multi-language/spec.md`

## Summary

Add full internationalization to the Nexill Retail Store POS using the WinUI 3 MRT Core resource pipeline (`.resw` files). All user-facing strings across 13 pages, 3 checkout components, 4 dialogs, and the MainWindow shell will be extracted into `Strings/en/Resources.resw` and wired via `x:Uid` (XAML) + `ResourceLoader` (code-behind). Empty skeleton `.resw` files for Arabic (`ar`) and French (`fr`) will be generated. A language selector will be added to the Store Management page. Arabic activates full RTL layout.

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: WinUI 3 (Windows App SDK), Microsoft.Data.Sqlite, MRT Core (built-in)  
**Storage**: SQLite `settings` table (key-value), `.resw` resource files (compiled to PRI)  
**Testing**: Manual visual verification page by page  
**Target Platform**: Windows 10+ desktop  
**Project Type**: Desktop app (WinUI 3 / MSIX)  
**Performance Goals**: Zero overhead — PRI resources are native binary-indexed  
**Constraints**: Language change requires app restart; offline-capable (no network for translations)  
**Scale/Scope**: ~300-400 extractable strings across 21 files (13 pages + 3 components + 4 dialogs + MainWindow)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Local-First Data Sovereignty | ✅ PASS | Language preference stored in local SQLite. `.resw` files compiled into app binary. Zero network dependency. |
| II. Notion-Grade Visual Design | ✅ PASS | No visual changes — same UI, same styles. Only text source changes from hardcoded to resource. |
| III. Non-Blocking, Concurrent UX | ✅ PASS | `ResourceLoader.GetString()` is synchronous and < 1µs. No async needed. |
| IV. Two-Layer Architecture | ✅ PASS | `SettingsRepository` (Data layer) stores language key. `ResourceLoader` (UI layer) loads strings. Clean separation. |
| V. Keyboard-First Checkout | ✅ PASS | No interaction changes. Keyboard flow unchanged. |
| VI. Zero-Cloud Build Dependency | ✅ PASS | `.resw` files are local XML. PRI compilation is part of standard MSBuild. No external service. |
| VII. Versioned Isolation | ✅ PASS | Changes are additive within 1.3.3. |
| VIII. Backward-Compatible Schema | ✅ PASS | Only adds `app_language` and `store_language` key-value pairs to existing `settings` table. No schema change needed — existing `SetSetting`/`GetSetting` pattern. |

**All gates pass. No violations.**

## Project Structure

### Documentation (this feature)

```text
specs/007-multi-language/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
└── contracts/           # Phase 1 output (UI contracts)
```

### Source Code (repository root)

```text
Nexill.RetailStorePOS/
├── Strings/
│   ├── en/Resources.resw              # English (default) — all extracted strings
│   ├── ar/Resources.resw              # Arabic skeleton — empty values, English comments
│   └── fr/Resources.resw              # French skeleton — empty values, English comments
├── Common/
│   └── LocalizationHelper.cs          # [NEW] Static ResourceLoader wrapper
├── Views/
│   ├── LoginPage.xaml                 # [MODIFY] Add x:Uid to static elements
│   ├── LoginPage.xaml.cs              # [MODIFY] Use ResourceLoader for dynamic strings
│   ├── CheckoutPage.xaml              # [MODIFY] Add x:Uid
│   ├── CheckoutPage.xaml.cs           # [MODIFY] Use ResourceLoader
│   ├── ProductsPage.xaml              # [MODIFY] Add x:Uid
│   ├── ReportsDashboardPage.xaml      # [MODIFY] Add x:Uid
│   ├── ReportsReceiptsPage.xaml       # [MODIFY] Add x:Uid
│   ├── ReportsPage.xaml               # [MODIFY] Add x:Uid
│   ├── UsersPage.xaml                 # [MODIFY] Add x:Uid
│   ├── SettingsPage.xaml              # [MODIFY] Add x:Uid
│   ├── StoreManagementPage.xaml       # [MODIFY] Add x:Uid + language dropdown UI
│   ├── StoreManagementPage.xaml.cs    # [MODIFY] Language selection + restart prompt
│   ├── TaxConfigurationPage.xaml      # [MODIFY] Add x:Uid
│   ├── MyPreferencesPage.xaml         # [MODIFY] Add x:Uid
│   ├── AboutPage.xaml                 # [MODIFY] Add x:Uid
│   ├── PlaceholderPage.xaml           # [MODIFY] Add x:Uid
│   ├── OpenRegisterDialog.xaml        # [MODIFY] Add x:Uid
│   ├── CloseRegisterDialog.xaml       # [MODIFY] Add x:Uid
│   ├── CashInOutDialog.xaml           # [MODIFY] Add x:Uid
│   ├── TimeSyncDialog.xaml            # [MODIFY] Add x:Uid
│   └── Components/
│       ├── CheckoutCartControl.xaml       # [MODIFY] Add x:Uid
│       ├── CheckoutPaymentControl.xaml    # [MODIFY] Add x:Uid
│       └── CheckoutProductsControl.xaml   # [MODIFY] Add x:Uid
├── MainWindow.xaml                    # [MODIFY] Add x:Uid for sidebar/shell strings
├── MainWindow.xaml.cs                 # [MODIFY] RTL FlowDirection logic
└── App.xaml.cs                        # [MODIFY] PrimaryLanguageOverride on startup

RetailStorePOS.Data/
├── SettingsRepository.cs              # [MODIFY] Add GetAppLanguage/SetAppLanguage helpers
└── Modules/Contracts/
    └── SettingsPlatformContract.cs     # [MODIFY] Add language setting key constants
```

**Structure Decision**: Follows existing Two-Layer Architecture. `.resw` files go inside the UI project under `Strings/{locale}/`. No new projects created.

## Complexity Tracking

No Constitution violations — table intentionally empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| — | — | — |
