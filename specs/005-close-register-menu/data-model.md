# Data Model: Close Register Menu

## Entities

### `RegisterSession`

Existing persistent Sales-owned row in `register_sessions`.

**Relevant Fields**

- `id` (INTEGER PRIMARY KEY)
- `user_id` (INTEGER, required)
- `opening_amount_cents` (INTEGER, required)
- `opening_note` (TEXT, nullable)
- `opened_at` (TEXT ISO8601, required)
- `closed_at` (TEXT ISO8601, nullable)
- `closing_amount_cents` (INTEGER, nullable)
- `closing_note` (TEXT, nullable)

**Feature Usage**

- Identifies the single active session for close-review and close-write workflows.
- Supplies the opening cash baseline used in expected-cash calculation.
- Stores final counted cash, close note, and close timestamp when confirmation succeeds.

**Legacy Upgrade Rule**

- Upgraded older databases may contain zero `register_sessions` rows.
- Zero rows is a valid post-upgrade state and means "no active session yet", not corrupted data.
- Historical `sales` rows do not create synthetic `RegisterSession` rows.

### `RegisterCloseSummary`

New Sales-owned read model returned to the close dialog. This is not a new table.

**Fields**

- `session_id` (long, required)
- `user_id` (long, required)
- `opened_at` (DateTime/string, required)
- `opening_amount_cents` (long, required)
- `expected_cash_cents` (long, required)
- `expected_card_cents` (long, required)
- `counted_cash_cents` (long, transient UI input)
- `difference_cents` (long, derived as `counted_cash_cents - expected_cash_cents`)
- `closing_note` (string?, transient until confirmed)

**Derivation Rules**

- `expected_cash_cents = opening_amount_cents + sum(total_cents where payment_type = 'Cash' and created_at >= opened_at)`
- `expected_card_cents = sum(total_cents where payment_type in ('Card', 'Credit', 'Debit') and created_at >= opened_at)`
- `difference_cents = counted_cash_cents - expected_cash_cents`

## State Transitions

### `RegisterSession`

- **Legacy / No Session** → **Open**
  - Trigger: First real register-open action records a session.
  - Pre-condition: Zero active session rows.
  - Result: New `register_sessions` row is created by the opening-control workflow.

- **Open** → **Close Review**
  - Trigger: Any signed-in user selects "Close Register" from the profile menu.
  - Pre-condition: Active session exists (`closed_at IS NULL`).
  - Result: `RegisterCloseSummary` is loaded for display; no persistence yet.

- **Close Review** → **Open**
  - Trigger: User cancels or dismisses the dialog, or save fails.
  - Action: Discard unsaved typed values; show retry error only when save failed.
  - Result: Active session remains open and user stays signed in.

- **Close Review** → **Closed**
  - Trigger: User confirms and persistence succeeds.
  - Action:
    - Persist `closed_at = DateTime.UtcNow`
    - Persist `closing_amount_cents = counted_cash_cents`
    - Persist `closing_note = closing_note`
  - Post-condition: Session row is closed and the UI logs out the current user.

## Validation Rules

- An active session must exist before the close dialog can confirm.
- Zero session rows after legacy upgrade is valid and must not be treated as an active session.
- `counted_cash_cents` must be explicitly entered or defaulted to a valid numeric value before the
  close command executes.
- `difference_cents` is derived only from expected cash versus counted cash.
- Only one active session may exist at a time; the repository must reject a second close attempt
  after `closed_at` is set.
- Any signed-in user may invoke close on the single active session per clarified product rule.
- The dialog must expose a non-functional "Cash In/Out" control without mutating any data.
