# Tasks: App Lifecycle Telemetry

## Task Group 1 - Stop Known Unsafe Behavior

- [x] Remove startup outbox deletion from `LoginRuntime.Initialize()`
- [x] Confirm remote event deduplication contract by `id`

## Task Group 2 - Add Runtime State Model

- [x] Create a repository/service wrapper for telemetry runtime state in `settings`
- [x] Store active run id and active run start time
- [x] Store last strong activity timestamp and source

## Task Group 3 - Add Lifecycle Orchestration

- [x] Add `run_id` generation for each launch
- [x] Detect unfinished prior run on startup
- [x] Emit `unclean_exit` instead of inventing a clean close
- [x] Record `app_launch`
- [x] Record `app_close`

## Task Group 4 - Wire Strong Activity Signals

- [x] Update last activity state when a sale is committed
- [x] Review/reject audit events as strong activity signals for rollout 1 (Rejected: too noisy)
- [x] Do not use weak denormalized product timestamps as the primary source

## Task Group 5 - Make Local Writes Atomic

- [x] Extend `InstallationEventRepository` to support caller-owned transaction usage
- [x] Extend `TelemetryOutboxRepository` to support caller-owned transaction usage
- [x] Wrap lifecycle event insert + outbox enqueue + state update in one SQLite transaction

## Task Group 6 - Keep Sync Durable

- [x] Keep startup-triggered flush
- [x] Keep connectivity-triggered flush
- [x] Keep periodic flush loop
- [x] Add retry/backoff policy based on existing outbox metadata

## Task Group 7 - Formalize Migration Safety

- [x] Introduce migration step classification (`StartupSafe`, `HeavyBackfill`, `RebuildOrDestructive`)
- [x] Keep only startup-safe steps in the splash path
- [x] Add explicit schema version ledger (`PRAGMA user_version` or equivalent)
- [x] Add validation/checkpoint behavior for heavier migration flows (PRAGMA quick_check, isolation)

## Task Group 8 - Optional Later Hardening

- [x] Add `(install_id, occurred_at)` index if lifecycle querying needs it
- [x] Add first-class `run_id` column if JSON parsing becomes a bottleneck
- [x] Add first-class estimation columns only if query pressure justifies them (populated for unclean_exit)
