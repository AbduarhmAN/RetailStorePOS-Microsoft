# Data Model: Register Opening Control

## Entity: RegisterSession

Represents one operational period of the cash register, from opening to closing.

### Fields

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| id | INTEGER | PK, AUTOINCREMENT | Unique session identifier |
| user_id | INTEGER | FK → users(id), NOT NULL | Cashier who opened the register |
| opening_amount_cents | INTEGER | NOT NULL | Opening cash balance in cents |
| opening_note | TEXT | NULLABLE | Optional note at opening |
| opened_at | TEXT | NOT NULL | UTC timestamp of register open |
| closed_at | TEXT | NULLABLE | UTC timestamp of register close (NULL = still open) |
| closing_amount_cents | INTEGER | NULLABLE | Cash counted at close (future) |
| closing_note | TEXT | NULLABLE | Note at close (future) |

### Indexes

- `idx_register_sessions_open` on `(closed_at)` — fast check for active session (WHERE closed_at IS NULL)
- `idx_register_sessions_user` on `(user_id, opened_at)` — user shift history

### Relationships

- `RegisterSession` 1 ← N `sales` (conceptual; sales occur within a session period)
- `RegisterSession` N → 1 `users` (who opened)

### State Transitions

```
[No Session] --Open Register--> [Active Session]
[Active Session] --Close Day--> [Closed Session]
```

### Validation Rules

- `opening_amount_cents` ≥ 0
- Only one session with `closed_at IS NULL` at a time
- `opening_note` max 500 characters (soft limit, truncated on save)

### SQLite DDL

```sql
CREATE TABLE IF NOT EXISTS register_sessions (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    user_id INTEGER NOT NULL,
    opening_amount_cents INTEGER NOT NULL,
    opening_note TEXT,
    opened_at TEXT NOT NULL,
    closed_at TEXT,
    closing_amount_cents INTEGER,
    closing_note TEXT,
    FOREIGN KEY (user_id) REFERENCES users(id)
);

CREATE INDEX IF NOT EXISTS idx_register_sessions_open
    ON register_sessions (closed_at);

CREATE INDEX IF NOT EXISTS idx_register_sessions_user
    ON register_sessions (user_id, opened_at);
```
