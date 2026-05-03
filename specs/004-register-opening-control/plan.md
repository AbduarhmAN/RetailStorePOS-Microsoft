# Implementation Plan: Register Opening Control

**Branch**: `004-register-opening-control` | **Date**: 2026-04-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/004-register-opening-control/spec.md`

## Summary

Add a modal dialog that blocks Checkout/Sales page access until the cashier confirms an opening cash balance. 
If user clicks "Discard", automatically open register with $0 and proceed to Checkout.
Persists register sessions in a new `register_sessions` SQLite table owned by the Sales module. 
UI implemented as a WinUI 3 `ContentDialog` with a NumberBox for amount and TextBox for notes.

## Technical Context

**Language/Version**: C# / .NET 9+  
**Primary Dependencies**: Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite  
**Storage**: SQLite (local file, WAL mode)  
**Testing**: Manual verification  
**Target Platform**: Windows 10+ (WinUI 3 desktop)  
**Project Type**: Desktop POS application  

## Constitution Check

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Local-First Data Sovereignty | ✅ PASS | `register_sessions` in local SQLite. |
| II. Notion-Grade Visual Design | ✅ PASS | ContentDialog with minimal styling. |
| III. Non-Blocking, Concurrent UX | ✅ PASS | DB write background. Dialog blocks only Checkout navigation. |
| IV. Two-Layer Architecture | ✅ PASS | Model+Repo in Data layer. Dialog in UI layer. |
| V. Keyboard-First Checkout | ✅ PASS | NumberBox auto-focused. Enter = Open. Esc = Discard (auto-zero). |
| VI. Zero-Cloud Build Dependency | ✅ PASS | No new packages. |
| VII. Versioned Isolation | ✅ PASS | Changes within 1.3.3 directory. |
| Never delete files | ✅ PASS | Only new/modified files. |
| Never auto-compile | ✅ PASS | No build commands. |

## Project Structure

### Documentation

```text
specs/004-register-opening-control/
├── plan.md              
├── spec.md              
├── research.md          
├── data-model.md        
├── quickstart.md        
├── contracts/register-session-contract.md
└── tasks.md             (next step)
```

### Source Code

```text
RetailStorePOS.Data/
├── Modules/
│   ├── Sales/
│   │   ├── RegisterSession.cs           [NEW]
│   │   ├── RegisterSessionRepository.cs [NEW]
│   └── Contracts/
│       └── CheckoutWorkflowContract.cs  [MODIFY]

Nexill.RetailStorePOS/
├── Views/
│   ├── OpenRegisterDialog.xaml          [NEW]
│   ├── OpenRegisterDialog.xaml.cs       [NEW]
│   └── CheckoutPage.xaml.cs             [MODIFY] 
├── LoginRuntime.cs                      [MODIFY] Add RegisterSessions repo
└── Class1.cs → DatabaseInitializer      [MODIFY] Migration step 16
```
