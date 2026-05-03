# Implementation Plan: Close Register Menu

**Branch**: `005-close-register-menu` | **Date**: 2026-04-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/005-close-register-menu/spec.md`

## Summary

Replace the static title-bar user badge with a native WinUI profile menu that exposes a single
"Close Register" action. The action opens a `ContentDialog` that shows session-based Cash and
Card totals, lets any signed-in user enter counted cash and a closing note, writes the close to
the Sales-owned `register_sessions` row, and only then logs the user out back to `LoginPage`.
Older merchant databases upgrade in place through additive SQLite migration `16`; no historical
register session is backfilled or inferred.

## Technical Context

**Language/Version**: C# on .NET 9+ / WinUI 3  
**Primary Dependencies**: Microsoft.WindowsAppSDK, Microsoft.Data.Sqlite  
**Storage**: Local SQLite (`pos.db`) with versioned `PRAGMA user_version` migrations  
**Testing**: Manual verification only  
**Target Platform**: Windows 10+ desktop  
**Project Type**: Desktop POS application  
**Performance Goals**: Profile menu opens immediately, counted-cash difference recalculates on
each edit, and successful close/logout returns to the login screen within 1 second of confirm  
**Constraints**: Local-first only; additive/idempotent schema change only; no new projects; no
new service/viewmodel shortcuts; logout must happen only after successful close persistence;
upgraded legacy databases start with zero register-session rows until first real open action;
shell/dialog UI must stay theme-aware, keyboard reachable, and visually aligned with the
constitution  
**Scale/Scope**: One title-bar interaction surface, one modal dialog, one Sales-module close
workflow, one legacy-safe migration path, and one local-auth/logout coordination path

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Local-First Data Sovereignty | PASS | Close totals, migration, and session persistence stay in local SQLite only. |
| II. Notion-Grade Visual Design | PASS | UI/UX guidance is constrained to minimal shell/dialog polish; no parallel visual language is introduced. |
| III. Non-Blocking, Concurrent UX | PASS | Summary loading and close persistence will run off the UI thread; cancel/failure flows keep UI responsive. |
| IV. Two-Layer Architecture | PASS | Sales data access remains in `RetailStorePOS.Data`; shell/dialog orchestration remains in `Nexill.RetailStorePOS`. |
| V. Keyboard-First Checkout | PASS | The menu and dialog remain keyboard reachable, and checkout keeps its existing open-register gate. |
| VI. Zero-Cloud Build Dependency | PASS | No network dependency or new runtime service is introduced. |
| VII. Versioned Isolation | PASS | All changes stay inside `V/1.3.3` and the active feature artifacts. |
| VIII. Backward-Compatible Local Schema Evolution | PASS | Existing merchant databases upgrade in place via additive migration and start with no synthetic register history. |
| Modular Ownership Guardrails | PASS | `register_sessions` and close-summary queries stay Sales-owned; logout remains Users/Auth state handled after the Sales write succeeds. |
| Manual Build Restriction | PASS | Plan and documentation only; no build commands are used. |

## Project Structure

### Documentation (this feature)

```text
specs/005-close-register-menu/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── 005-close-register-menu-threat-model.md
├── contracts/
│   └── register-close-contract.md
└── tasks.md
```

### Source Code (repository root)

```text
RetailStorePOS.Data/
├── Class1.cs                                      # MODIFY: additive migration and legacy DB upgrade behavior
├── Modules/
│   ├── Contracts/
│   │   └── CheckoutWorkflowContract.cs            # MODIFY: add close-summary and close command contract entries
│   └── Sales/
│       ├── RegisterSession.cs                     # REVIEW: existing persistent session model remains source of truth
│       ├── RegisterCloseSummary.cs                # NEW: session-scoped close-summary read model
│       └── RegisterSessionRepository.cs           # MODIFY: summary query, close persistence, retry-safe active-session checks

Nexill.RetailStorePOS/
├── MainWindow.xaml                                # MODIFY: replace static badge with profile menu trigger
├── MainWindow.xaml.cs                             # MODIFY: open dialog, handle retry/error state, trigger logout after successful close
├── Services/
│   └── AuthService.cs                             # REFERENCE: existing logout behavior and login-state events
└── Views/
    ├── CloseRegisterDialog.xaml                   # NEW: close-register review dialog
    ├── CloseRegisterDialog.xaml.cs                # NEW: summary load, difference math, cancel/error handling
    ├── CheckoutPage.xaml.cs                       # REFERENCE: existing register-open gate on checkout
    └── OpenRegisterDialog.xaml                    # REFERENCE: dialog behavior baseline only
```

**Structure Decision**: Keep the current two-project split. Extend the Sales module rather than
adding a new service or viewmodel layer, keep the shell interaction in `MainWindow`, and treat
legacy database handling as migration logic in `RetailStorePOS.Data/Class1.cs`.

## Phase 0 Research Output

Phase 0 resolves the remaining implementation choices and risk decisions:

1. Use a native WinUI profile-menu trigger in the title bar rather than a custom popup shell.
2. Compute close-register totals from the active session window, showing Cash and Card only.
3. Upgrade older local databases in place, add the `register_sessions` table, and backfill no
   fake session history.
4. Reuse the existing auth logout event path only after the close write succeeds.
5. Accept the product decision that any signed-in user may close the single active session, but
   document the integrity and audit implications.
6. Keep dialog interaction quality aligned with repo UX rules: visible labels, clear inline error
   state, safe cancel path, keyboard access, and theme-aware contrast.

## Phase 1 Design Output

Phase 1 produces:

- `research.md` with UI, migration, security, and UX decisions
- `data-model.md` defining persistent session fields, zero-row legacy state, and the new
  `RegisterCloseSummary` read model
- `contracts/register-close-contract.md` documenting Sales-owned close-summary and close command
  boundaries
- `quickstart.md` describing manual verification including legacy database upgrade behavior
- `005-close-register-menu-threat-model.md` capturing repo-grounded security assumptions and abuse
  paths for this workflow

## Complexity Tracking

No constitution or ownership violations are required for this feature. The main explicit tradeoff
is product-driven: any signed-in user may close the single active register session, so the design
must keep auditability and clear user intent visible.
