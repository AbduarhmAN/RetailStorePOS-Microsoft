# Quickstart: Register Opening Control

## What This Feature Does

Adds a modal dialog that requires cashiers to enter an opening cash balance before accessing checkout. Tracks register sessions in SQLite.

## Key Files

### Data Layer (RetailStorePOS.Data)

| File | Purpose |
|------|---------|
| `Modules/Sales/RegisterSession.cs` | [NEW] POCO model for register_sessions table |
| `Modules/Sales/RegisterSessionRepository.cs` | [NEW] SQLite CRUD for register sessions |
| `Modules/Contracts/CheckoutWorkflowContract.cs` | [MODIFY] Add RegisterSessionQuery + RegisterOpenCommand |
| `Class1.cs` | [MODIFY] Add migration step 16: create register_sessions table |

### UI Layer (Nexill.RetailStorePOS)

| File | Purpose |
|------|---------|
| `Views/OpenRegisterDialog.xaml` | [NEW] ContentDialog XAML for opening control modal |
| `Views/OpenRegisterDialog.xaml.cs` | [NEW] Code-behind: validation, open/discard logic |
| `LoginRuntime.cs` | [MODIFY] Add RegisterSessionRepository singleton |
| `Views/CheckoutPage.xaml.cs` | [MODIFY] Check active session on navigation; show dialog if none |

## Build & Run

No new NuGet packages. No new projects. Fits Two-Layer Architecture.
