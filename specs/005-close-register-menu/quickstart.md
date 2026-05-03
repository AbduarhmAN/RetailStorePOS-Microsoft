# Quickstart: Close Register Menu

## What This Feature Does

Adds a user profile menu to the main shell and a close-register dialog that lets a signed-in
operator review expected Cash and Card totals, enter counted cash, close the current register
session, and return to the login screen immediately after the close succeeds.

## Key Files

### Data Layer (`RetailStorePOS.Data`)

| File | Purpose |
|------|---------|
| `Class1.cs` | Additive migration behavior for `register_sessions` and legacy database upgrades. |
| `Modules/Contracts/CheckoutWorkflowContract.cs` | Extend checkout workflow contracts with close-summary and close-register ownership metadata. |
| `Modules/Sales/RegisterCloseSummary.cs` | New read model for expected close totals shown in the dialog. |
| `Modules/Sales/RegisterSessionRepository.cs` | Query active-session close summary and persist the final session close. |

### UI Layer (`Nexill.RetailStorePOS`)

| File | Purpose |
|------|---------|
| `MainWindow.xaml` | Replace the static user badge with an interactive profile menu trigger. |
| `MainWindow.xaml.cs` | Open the close dialog from the shell, surface retry errors, and reuse the existing logout/login-state path. |
| `Services/AuthService.cs` | Existing logout mechanism used after successful close persistence. |
| `Views/CloseRegisterDialog.xaml` | New modal layout for expected totals, counted cash, note, and disabled Cash In/Out action. |
| `Views/CloseRegisterDialog.xaml.cs` | Load summary data, compute difference, discard values on cancel, and expose retry-safe close payload. |
| `Views/CheckoutPage.xaml.cs` | Existing reference path showing how no active session is handled by the open-register workflow. |

## Manual Verification

1. Sign in with any account that can reach the main shell.
2. Confirm the title bar shows an interactive user profile control instead of a static badge.
3. Open the profile menu and verify that "Close Register" is available.
4. Open the dialog and verify it shows expected Cash and Card totals for the active session.
5. Enter counted cash and verify the difference updates immediately.
6. Cancel or dismiss the dialog and verify unsaved values are discarded while the session stays
   open.
7. Trigger a close-save failure path and verify an error is shown, the dialog stays open, and the
   user remains signed in.
8. Confirm a successful close and verify the shell returns to the login screen immediately
   afterward.
9. Validate an older local database with no `register_sessions` rows:
   - app upgrades the database in place
   - no historical session row is created
   - first real session is created only by the register-open flow

## Notes

- No new projects or external services are introduced.
- Manual verification only; no build commands are part of this planning artifact.
- `Customer Account` is out of scope and not shown in the close dialog.
