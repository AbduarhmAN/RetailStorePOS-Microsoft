# Tasks: Close Register Menu

**Input**: Design documents from `/specs/005-close-register-menu/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/register-close-contract.md`, `quickstart.md`

**Tests**: Not requested. No automated test tasks generated. Validation stays manual through `specs/005-close-register-menu/quickstart.md`.

**Organization**: Tasks are grouped by user story so the shell menu work and the close-register workflow can be implemented and verified in controlled increments.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on incomplete tasks)
- **[Story]**: Which user story this task belongs to (`US1`, `US2`)
- Every task includes an exact file path

---

## Phase 1: Setup (Shared Scaffolding)

**Purpose**: Create the feature-specific files that later phases fill in.

- [X] T001 [P] Create the `RegisterCloseSummary` read model in `RetailStorePOS.Data/Modules/Sales/RegisterCloseSummary.cs`
- [X] T002 [P] Create the `CloseRegisterDialog` XAML shell in `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml`
- [X] T003 [P] Create the `CloseRegisterDialog` code-behind shell in `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml.cs`

**Checkpoint**: Feature-specific file scaffolding exists and later phases can fill in behavior without path churn.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish the Sales contract surface, summary query, and legacy-database safety rules that the UI depends on.

**⚠️ CRITICAL**: No user story should be considered complete until this phase is done.

- [X] T004 [P] Extend close-register contracts and workflow metadata in `RetailStorePOS.Data/Modules/Contracts/CheckoutWorkflowContract.cs`
- [X] T005 Implement the active-session close-summary query in `RetailStorePOS.Data/Modules/Sales/RegisterSessionRepository.cs`
- [X] T006 [P] Preserve additive legacy-upgrade and zero-row fallback behavior in `RetailStorePOS.Data/Class1.cs` and `Nexill.RetailStorePOS/Views/CheckoutPage.xaml.cs`

**Checkpoint**: Close-summary reads are defined, legacy databases remain valid, and the shell can rely on stable Sales-owned behavior.

---

## Phase 3: User Story 1 - User Profile Menu (Priority: P1) 🎯

**Goal**: Replace the generic title-bar badge with a real user profile menu that becomes the shell entry point for register actions.

**Independent Test**: Sign in, inspect the title bar, and confirm the profile control shows the current user identity and opens a menu with `Close Register`.

- [X] T007 [P] Identify the user-badge `Button` in `Nexill.RetailStorePOS/MainWindow.xaml`
- [X] T008 [P] Update `MainWindow.xaml` to replace the user-badge with a `DropDownButton` containing "Close Register" and "Logout" items
- [X] T009 Add the `CloseRegister_Click` and `Logout_Click` event handlers to `MainWindow.xaml.cs` (shell only)

**Checkpoint**: User Story 1 is independently functional and the shell exposes the new profile-menu entry point.

---

## Phase 4: User Story 2 - Close Register Modal (Priority: P2)

**Goal**: Let any signed-in user review session Cash/Card totals, enter counted cash, close the active session safely, and log out only after the save succeeds.

**Independent Test**: Open `Close Register`, confirm expected totals and live difference behavior, then verify cancel, failure, and success each produce the specified session and logout state.

- [X] T010 [P] [US2] Complete the dialog layout for expected Cash/Card totals, counted cash, closing note, disabled `Cash In/Out`, and no `Daily Sales` in `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml`
- [X] T011 [P] [US2] Implement summary loading, real-time difference math, cancel discard, inline retry error, and initial focus behavior in `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml.cs`
- [X] T012 [P] [US2] Implement transaction-safe `CloseRegister(...)` persistence with double-close and no-active-session guardrails in `RetailStorePOS.Data/Modules/Sales/RegisterSessionRepository.cs`
- [X] T013 [US2] Launch `CloseRegisterDialog`, load `RegisterCloseSummary`, and handle retry-safe confirm flow in `Nexill.RetailStorePOS/MainWindow.xaml.cs`
- [X] T014 [US2] Reuse the existing auth state-change path so logout happens only after a successful close save in `Nexill.RetailStorePOS/MainWindow.xaml.cs` and `Nexill.RetailStorePOS/Services/AuthService.cs`

**Checkpoint**: User Story 2 is independently functional, including cancel, retry, success, and logout ordering.

---

## Phase 5: Polish & Cross-Cutting Concerns

**Purpose**: Finish security, accessibility, and manual verification work that spans both stories.

- [X] T015 [P] Apply threat-model guardrails for TM-001 through TM-005 in `RetailStorePOS.Data/Modules/Sales/RegisterSessionRepository.cs`, `Nexill.RetailStorePOS/MainWindow.xaml.cs`, and `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml.cs`
- [X] T016 [P] Refine profile-menu and dialog accessibility, focus order, touch targets, and theme-aware contrast in `Nexill.RetailStorePOS/MainWindow.xaml` and `Nexill.RetailStorePOS/Views/CloseRegisterDialog.xaml`
- [X] T017 Run the manual verification scenarios and record outcomes in `specs/005-close-register-menu/quickstart.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1: Setup** — no dependencies
- **Phase 2: Foundational** — depends on Phase 1 and blocks story completion
- **Phase 3: US1** — depends on Phase 2
- **Phase 4: US2** — depends on Phase 2; final shell wiring depends on the profile-menu handler from US1
- **Phase 5: Polish** — depends on the selected user stories being complete

### User Story Dependencies

- **US1**: Can begin after the foundational Sales/query and legacy-upgrade work is stable
- **US2**: Can begin after the foundation; dialog and repository work can proceed in parallel, but the end-to-end shell path depends on US1 exposing the `Close Register` menu action

### Within Each User Story

- `MainWindow.xaml` should land before final `MainWindow.xaml.cs` menu behavior
- `CloseRegisterDialog.xaml` and `CloseRegisterDialog.xaml.cs` should be in place before shell launch wiring
- `RegisterSessionRepository.CloseRegister(...)` must be complete before save-then-logout wiring is finalized
- Manual verification happens after both stories and cross-cutting hardening are complete

---

## Parallel Opportunities

### Phase 1

```text
T001 Create RegisterCloseSummary read model
T002 Create CloseRegisterDialog XAML shell
T003 Create CloseRegisterDialog code-behind shell
```

### Phase 2

```text
T004 Extend close-register contracts
T006 Preserve additive legacy-upgrade and zero-row fallback behavior
```

### User Story 2

```text
T010 Complete CloseRegisterDialog.xaml layout
T011 Implement CloseRegisterDialog.xaml.cs interaction logic
T012 Implement RegisterSessionRepository.CloseRegister(...) persistence guardrails
```

---

## Implementation Strategy

### MVP First

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational
3. Complete Phase 3: User Story 1
4. Complete Phase 4: User Story 2
5. **STOP and VALIDATE**: Run the manual verification path in `specs/005-close-register-menu/quickstart.md`

### Incremental Delivery

1. Finish Setup + Foundational so Sales/query rules and legacy DB behavior are stable
2. Deliver US1 to land the new shell entry point
3. Deliver US2 to complete the end-to-end close flow
4. Finish Polish for security/order-of-operations and UI quality

### Parallel Team Strategy

1. Team completes Setup + Foundational together
2. After the foundation:
   - Developer A: US1 shell/menu work
   - Developer B: US2 dialog UI work
   - Developer C: US2 repository close-persistence work
3. Merge on `MainWindow.xaml.cs` only after the menu handler, dialog behavior, and repository close path are ready

---

## Notes

- All tasks use the required checklist format with task ID, optional parallel marker, optional story label, and exact file path
- No automated test tasks are included because the feature spec requests manual verification only
- Threat-model and secure-default work is explicitly represented so logout ordering, stale dialog state, and legacy migration mistakes are not left implicit
- Practical MVP is **US1 + US2 together** because the profile menu alone is only a shell checkpoint, not the full register-close value
