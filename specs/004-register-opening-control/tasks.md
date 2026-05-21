# Tasks: Register Opening Control

**Input**: Design documents from `/specs/004-register-opening-control/`
**Prerequisites**: plan.md, spec.md, data-model.md, contracts/register-session-contract.md, research.md

**Tests**: Not requested. No test tasks generated.

**Organization**: Single user story (US1). Tasks grouped by phase.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1)
- Exact file paths included

---

## Phase 1: Setup

**Purpose**: No new projects needed. Existing solution structure. Only verify paths.

- [x] T001 Verify specs directory structure exists at `e:\Projects\Retail_Store\V\1.3.3\specs\004-register-opening-control\`

**Checkpoint**: Directory structure confirmed

---

## Phase 2: Foundational (Data Layer)

**Purpose**: Create `register_sessions` table, POCO model, and repository. MUST complete before UI work.

**⚠️ CRITICAL**: No UI tasks can begin until this phase is complete

- [x] T002 Add migration step 16 to create `register_sessions` table with indexes in `e:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Class1.cs`
- [x] T003 [P] Create `RegisterSession` POCO model in `e:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Sales\RegisterSession.cs`
- [x] T004 [P] Create `RegisterSessionRepository` with `GetActiveSession()` and `OpenRegister()` methods in `e:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Sales\RegisterSessionRepository.cs`
- [x] T005 Add `RegisterSessionQuery` and `RegisterOpenCommand` contracts to `e:\Projects\Retail_Store\V\1.3.3\RetailStorePOS.Data\Modules\Contracts\CheckoutWorkflowContract.cs`
- [x] T006 Register `RegisterSessionRepository` singleton in `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\LoginRuntime.cs`

**Checkpoint**: Data layer complete. `RegisterSessionRepository.GetActiveSession()` and `OpenRegister()` callable.

---

## Phase 3: User Story 1 - Cash Register Opening (Priority: P1) 🎯 MVP

**Goal**: Modal dialog blocks Checkout page when no active session. User enters opening cash + optional note. "Open Register" saves session. "Discard" auto-opens with $0.

**Independent Test**: Launch POS → navigate to Checkout → modal appears → enter amount → click "Open Register" → modal closes, checkout accessible. Relaunch → no modal (session active).

### Implementation for User Story 1

- [x] T007 [P] [US1] Create `OpenRegisterDialog.xaml` ContentDialog with NumberBox (opening cash), TextBox (opening note), "Open Register" primary button, "Discard" secondary button in `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\OpenRegisterDialog.xaml`
- [x] T008 [P] [US1] Create `OpenRegisterDialog.xaml.cs` code-behind with validation (amount >= 0), note truncation (500 chars), Open/Discard handlers in `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\OpenRegisterDialog.xaml.cs`
- [x] T009 [US1] Add session gate to `CheckoutPage.xaml.cs`: on `OnNavigatedTo`, check `RegisterSessionRepository.GetActiveSession()`. If null, show `OpenRegisterDialog`. On "Open Register" → call `OpenRegister(userId, amountCents, note)`. On "Discard" → call `OpenRegister(userId, 0, null)`. File: `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml.cs`
- [x] T010 [US1] Handle edge case: if register already open when modal triggered, skip modal silently in `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\CheckoutPage.xaml.cs`

**Checkpoint**: User Story 1 fully functional. Opening control blocks checkout until confirmed or discarded.

---

## Phase 4: Polish & Cross-Cutting Concerns

**Purpose**: Keyboard-first UX, validation edge cases

- [x] T011 Ensure NumberBox gets initial focus when dialog opens, Enter maps to "Open Register", Esc maps to "Discard" in `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\OpenRegisterDialog.xaml.cs`
- [x] T012 Validate negative amount input shows inline error (NumberBox Minimum=0) in `e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Views\OpenRegisterDialog.xaml`
- [x] T013 Manual verification: run quickstart.md scenarios (open with amount, discard with $0, re-navigate skips modal)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all UI work
- **User Story 1 (Phase 3)**: Depends on Phase 2 completion (T002–T006)
- **Polish (Phase 4)**: Depends on Phase 3 completion

### Within Phase 2

- T002 (migration) can run parallel with T003 (model) and T004 (repo)
- T005 (contracts) independent of T002–T004
- T006 (LoginRuntime registration) depends on T004 (repo must exist)

### Within Phase 3

- T007 (XAML) and T008 (code-behind) parallel — different content, same pair
- T009 (session gate) depends on T007+T008 (dialog must exist)
- T010 (edge case) depends on T009

---

## Parallel Example: Foundational Phase

```text
# Parallel batch 1:
Task T002: Migration step 16 in Class1.cs
Task T003: RegisterSession POCO in RegisterSession.cs
Task T004: RegisterSessionRepository in RegisterSessionRepository.cs
Task T005: Contracts in CheckoutWorkflowContract.cs

# Sequential after batch 1:
Task T006: Register repo in LoginRuntime.cs (needs T004)
```

---

## Implementation Strategy

### MVP First (Single Story)

1. Complete Phase 1: Setup (verify)
2. Complete Phase 2: Foundational (data layer)
3. Complete Phase 3: User Story 1 (dialog + gate)
4. **STOP and VALIDATE**: Manual test all 3 acceptance scenarios
5. Complete Phase 4: Polish (keyboard, validation)

---

## Notes

- [P] tasks = different files, no dependencies
- [US1] = maps to User Story 1 (Cash Register Opening)
- No test tasks generated (not requested in spec)
- Single user story → linear execution is fine
- Commit after each phase checkpoint
- FR-003 (no money details popup) satisfied by omission — no such button in XAML
