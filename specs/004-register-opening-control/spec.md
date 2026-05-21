# Feature Specification: Register Opening Control

**Feature Branch**: `004-register-opening-control`  
**Created**: 2026-04-22  
**Status**: Draft  
**Input**: User description: "<div class=\"modal-content\" ...>...</div> but without the money details popup button"

## Clarifications

### Session 2026-04-22

- Q: Does a closed register block only checkout or the whole app? → A: Blocks only the Checkout/Sales page. Other areas (Settings, Reports) remain accessible.
- Q: What does "Discard" do? → A: Discard skips the explicit cash entry, automatically opens the register with a $0 balance, and proceeds to the Checkout page normally.
- Q: Where is register opening data saved? → A: In a new dedicated `register_sessions` table in the local SQLite database, separate from sales data.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Cash Register Opening (Priority: P1)

As a cashier or store manager, I want to define the opening cash balance and provide a note when opening a register, so that cash operations can be tracked accurately from the start of the shift.

**Why this priority**: Opening the register with an initial balance is a critical preliminary step for any retail point-of-sale system before conducting sales.

**Independent Test**: Can be fully tested by launching the POS, encountering the Opening Control modal if the register is closed, entering an amount, and confirming the register opens with that starting balance.

**Acceptance Scenarios**:

1. **Given** the POS is launched and the register is currently closed, **When** the "Opening Control" modal appears, **Then** the user sees an input for "Opening cash", a textarea for "Opening note", and buttons to "Open Register" or "Discard".
2. **Given** the "Opening Control" modal is visible, **When** the user inputs a valid cash amount, adds a note, and clicks "Open Register", **Then** the system records the opening transaction and unlocks the POS for sales.
3. **Given** the "Opening Control" modal is visible, **When** the user clicks "Discard", **Then** the system automatically records an opening transaction with 0 cash balance and unlocks the POS for sales.

---

### Edge Cases

- What happens when the user enters a negative or invalid non-numeric opening cash amount? (Should show validation error).
- How does the system handle an excessively long opening note? (Should be truncated or wrapped safely within database limits).
- What happens if the register is already marked as open when this modal is invoked?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display an "Opening Control" modal window upon attempting to access the Checkout/Sales page when the register is closed. Other areas (Settings, Reports) remain accessible without an open register.
- **FR-002**: System MUST capture the "Opening cash" amount as a numeric/currency value.
- **FR-003**: System MUST NOT include or display a "money details popup" button next to the cash input.
- **FR-004**: System MUST capture an optional "Opening note" via a text area.
- **FR-005**: System MUST allow the user to confirm the opening via an "Open Register" primary button.
- **FR-006**: System MUST allow the user to bypass the modal via a "Discard" secondary button, which automatically opens the register with a 0 cash balance and proceeds to checkout.
- **FR-007**: System MUST record the opening transaction in the database, associating it with the current user and shift/run.

### Key Entities *(include if feature involves data)*

- **Register Session / Shift**: Represents the operational period of the register, starting with an opening balance and note.
- **Cash Transaction**: The specific financial record of the opening balance added to the drawer.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of new shifts record an opening cash balance (either explicitly entered or auto-zeroed via Discard).
- **SC-002**: Users can complete the register opening process in under 15 seconds.
- **SC-003**: Opening balances are accurately reflected in the day's financial reporting without discrepancies.

## Assumptions

- The frontend UI will align with the provided HTML structure's semantics but be implemented in the native WinUI 3 controls.
- The default opening cash input accepts standard currency formats based on the region settings.
