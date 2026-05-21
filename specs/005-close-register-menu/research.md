# Research: Close Register Menu

## Title-Bar User Menu

- **Decision**: Replace the static `UserBadge` surface in `MainWindow.xaml` with a native WinUI
  `DropDownButton` that opens a built-in `Flyout` containing the current user identity and a
  single "Close Register" action.
- **Rationale**: The feature needs a dropdown interaction in the title bar plus a compact identity
  summary. `DropDownButton` + `Flyout` keeps the shell native, light-dismissable, keyboard
  reachable, and easier to keep theme-aware than a custom popup.
- **Alternatives considered**:
  - `MenuFlyout`: good for action-only menus, but weaker once the surface must also show identity.
  - Custom `Popup`: rejected because it duplicates built-in dismiss, focus, and accessibility
    behavior.

## Close Register Dialog Pattern

- **Decision**: Implement the close flow as a WinUI `ContentDialog` in
  `Views/CloseRegisterDialog.xaml`, modeled after `OpenRegisterDialog` but with richer summary,
  explicit counted-cash input, inline retry error state, and safe cancel behavior.
- **Rationale**: The repository already uses `ContentDialog` for transient modal work. The close
  flow is contextual and should not force navigation away from the current workspace.
- **Alternatives considered**:
  - Separate page navigation: rejected because it adds route churn for a short-lived action.
  - Inline panel inside `MainWindow`: rejected because it complicates shell chrome for one task.

## Expected Totals Source

- **Decision**: Add a Sales-owned close-summary query in `RegisterSessionRepository` that loads
  the active session and derives expected totals from the session window:
  - `expected cash = opening amount + cash sales since opened_at`
  - `expected card = card/debit/credit sales since opened_at`
- **Rationale**: The close dialog needs session-specific numbers, and
  `SaleRepository.GetDailySummary` is date-based rather than session-based. Keeping the query in
  Sales preserves ownership of `register_sessions` and `sales`.
- **Alternatives considered**:
  - Reuse `GetDailySummary`: rejected because sessions can span date boundaries and are not tied
    to `opened_at`.
  - Calculate totals in WinUI code-behind: rejected because it bypasses the Sales boundary.
  - Hardcode all totals to zero: rejected because the clarified spec requires real current-session
    values.

## Legacy Database Upgrade Strategy

- **Decision**: Keep old merchant databases in place, run migration `16` to create
  `register_sessions`, and leave the new table empty on upgraded legacy databases until the first
  real register-open action records a session.
- **Rationale**: `DatabaseInitializer.RunMigrations` already uses versioned, additive SQLite
  migrations, and `Create table if not exists` avoids destructive resets. Fake backfill would
  invent business history that never occurred.
- **Alternatives considered**:
  - Backfill one synthetic session from historical sales: rejected because it invents false
    operational state and can misstate opening/closing cash.
  - Reset or replace the old database: rejected because it violates local data sovereignty and
    upgrade safety.

## Close Permission and Security Tradeoff

- **Decision**: Accept the clarified product rule that any signed-in user may close the single
  active session, but treat it as an integrity-sensitive operation that should keep clear intent,
  confirmation, and audit visibility.
- **Rationale**: The spec deliberately allows any signed-in local operator to close the active
  session. That removes ownership checks, so the design should not pretend stronger authZ than the
  product wants.
- **Alternatives considered**:
  - Opener-only close: rejected by clarified spec.
  - Opener-or-admin close: rejected by clarified spec.

## Close Persistence and Logout

- **Decision**: Persist the close action in `RegisterSessionRepository`, then call
  `LoginRuntime.Auth.Logout()` from the shell/dialog workflow and rely on the existing
  `LoginStateChanged` navigation in `MainWindow.xaml.cs` to return to `LoginPage`.
- **Rationale**: `AuthService.Logout()` already writes audit events and drives shell navigation
  through established behavior. Reusing that path avoids duplicate navigation logic.
- **Alternatives considered**:
  - Navigate directly to `LoginPage`: rejected because it duplicates login-state transition logic.
  - Logout before save: rejected because failed persistence must not strand the UI in logged-out
    state with an open session.

## Threat Model Snapshot

- **Decision**: Treat the feature as a local desktop integrity workflow, not an internet-exposed
  service. Priority threats are: accidental or unauthorized local session closure, logout/save
  ordering errors, legacy migration creating false active state, and local database tampering.
- **Rationale**: Evidence in the repo shows a local SQLite POS app with local auth/session state,
  checkout gating, and versioned migrations rather than a remote API service.
- **Alternatives considered**:
  - Model it as a network/API threat surface: rejected because current evidence does not show a
    remote runtime boundary for this workflow.

## Security Defaults

- **Decision**: Use repo-grounded secure defaults for this feature: additive migration only,
  retry-safe close persistence, logout only after successful save, no synthetic historical
  sessions, and explicit failure handling that keeps the user signed in.
- **Rationale**: The named `security-best-practices` skill has no C#/WinUI-specific reference
  files in this session, so the fallback is repo-grounded secure-by-default design rather than a
  language-specific checklist.
- **Alternatives considered**:
  - Apply a generic web security checklist: rejected because this workflow is a local desktop path,
    not an HTTP server.

## UX Quality Guardrails

- **Decision**: Apply these feature-level UI rules:
  - visible labels for counted cash and note input
  - explicit error message near the close action on save failure
  - touch/click targets large enough for counter hardware use
  - keyboard focus lands on the first editable field
  - cancel/dismiss path stays available because unsaved changes are intentionally discarded
  - light/dark contrast stays theme-aware and no hover-only affordances are required
- **Rationale**: These fit both the repository constitution and the `ui-ux-pro-max` guidance for
  accessibility, touch targets, responsive layout, and clear error recovery.
- **Alternatives considered**:
  - Decorative motion-heavy dialog treatment: rejected because it conflicts with the repo design
    language and adds no workflow value.
