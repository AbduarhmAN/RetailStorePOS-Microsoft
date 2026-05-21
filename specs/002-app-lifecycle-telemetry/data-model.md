# Data Model: App Lifecycle Telemetry

## Design Goal

Keep the model small, offline-first, additive-first, and install-scoped.

## Local Tables To Reuse

### `installation_events`

Existing table in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:99)

Current columns:

| Column | Purpose |
|---|---|
| `id` | Stable event id for idempotency |
| `install_id` | Install/device key |
| `event_type` | Lifecycle event name |
| `occurred_at` | Business time of the event |
| `payload_json` | Flexible event metadata |
| `created_at` | Local insert time |

Recommended rollout-1 use:

- keep table shape unchanged
- store new lifecycle metadata inside `payload_json`

Recommended lifecycle event payload keys:

| Key | Required | Purpose |
|---|---|---|
| `run_id` | Yes | Correlates one app execution |
| `app_version` | Yes | Version at time of event |
| `reason` | Optional | Close or failure reason |
| `estimated_from_at` | Only for `unclean_exit` | Strong activity timestamp used for estimate |
| `estimated_from_source` | Only for `unclean_exit` | Which signal produced the estimate |

### `telemetry_outbox`

Existing table in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:84)

Current columns:

| Column | Purpose |
|---|---|
| `id` | Local queue row id |
| `created_at` | Queue insertion time |
| `endpoint` | Remote endpoint |
| `payload_json` | Serialized payload |
| `merge_duplicates` | Upsert/merge intent |
| `attempt_count` | Retry counter |
| `last_attempt_at` | Last retry timestamp |
| `last_error` | Last failure detail |
| `sent_at` | Success marker |

### `settings`

Existing table in [Class1.cs](/E:/Projects/Retail_Store/V/1.4.0/RetailStorePOS.Data/Class1.cs:79)

Recommended telemetry runtime keys:

| Key | Purpose |
|---|---|
| `telemetry_active_run_id` | Current open run |
| `telemetry_active_run_started_at_utc` | Launch time of open run |
| `telemetry_last_activity_at_utc` | Last strong activity timestamp |
| `telemetry_last_activity_source` | Source of last strong activity |

Optional future keys:

| Key | Purpose |
|---|---|
| `telemetry_install_id` | Move install identity from preferences into SQLite later |
| `telemetry_first_run_at_utc` | Durable install-start timestamp |
| `telemetry_launch_count` | Optional aggregate counter |
| `telemetry_unclean_exit_count` | Optional aggregate counter |

## Event Types

| Event Type | Meaning |
|---|---|
| `app_launch` | App execution started |
| `app_close` | App execution ended cleanly |
| `unclean_exit` | Prior run ended without a clean close |
| `setup_completed` | Optional installer/bootstrap lifecycle event already present in the current telemetry flow |

## Recommended Future Additive Columns

These are optional for a later phase, not required for rollout 1.

| Column | Table | Why |
|---|---|---|
| `run_id TEXT NULL` | `installation_events` | Queryable run correlation without parsing JSON |
| `estimated_from_at TEXT NULL` | `installation_events` | First-class crash estimation field |
| `estimated_from_source TEXT NULL` | `installation_events` | First-class crash estimation source |

## Recommended Future Index

| Index | Why |
|---|---|
| `(install_id, occurred_at)` on `installation_events` | Lifecycle reconstruction is time-based on `occurred_at`, not only `created_at` |

## Remote Supabase Shape

Recommended remote logical model:

- one shared `installations` table
- one shared `installation_events` table

Recommended remote event fields:

| Field | Purpose |
|---|---|
| `id` | Idempotency key |
| `install_id` | Install/device key |
| `event_type` | Lifecycle event name |
| `occurred_at` | Business event time |
| `payload_json` | Flexible metadata |
| `created_at` | Local event creation time |

Remote first-class columns for `run_id` or estimation metadata are optional and require schema confirmation.

## Explicit Exclusions

Do not add these for lifecycle telemetry:

- `user_id`
- `username`
- `cashier_name`
- login session analytics tables
- per-user remote tables
