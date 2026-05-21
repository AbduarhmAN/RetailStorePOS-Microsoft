# Feature Specification: app-lifecycle-telemetry

**Feature Branch**: `002-app-lifecycle-telemetry`  
**Created**: 2026-04-20  
**Status**: Draft  
**Input**: Application-level lifecycle telemetry, offline-first sync, production-safe rollout, and startup-time migration policy for an already deployed desktop app.

## Scope

This specification defines a minimal telemetry system for:

- `app_launch`
- `app_close`
- `unclean_exit`
- install/device context
- last-known strong business activity used only to estimate the practical end boundary for an unclean exit

This specification explicitly does **not** cover:

- personal identity tracking
- per-user telemetry tables
- in-app clickstream analytics
- page/session analytics
- destructive schema cleanup

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Record app lifecycle offline (Priority: P1)

As a desktop app installation, I need lifecycle events to be stored locally first, so that usage tracking is not lost when the device is offline.

**Why this priority**: Offline durability is the core requirement.

**Independent Test**: Launch and close the app with no internet connection and verify that local lifecycle rows are recorded and queued.

**Acceptance Scenarios**:

1. **Given** the app starts while offline, **When** startup completes, **Then** an `app_launch` event is written locally and queued for later upload.
2. **Given** the app closes cleanly while offline, **When** shutdown logic runs, **Then** an `app_close` event is written locally and queued for later upload.

---

### User Story 2 - Detect unclean exit safely (Priority: P1)

As the app, I need to detect that the previous run ended without a clean close, so that telemetry reflects crash-like termination correctly instead of inventing a clean close.

**Why this priority**: Crash/unclean-exit visibility is one of the explicit business goals.

**Independent Test**: Start the app, terminate it without a normal close, relaunch it, and verify that an `unclean_exit` is synthesized for the prior run.

**Acceptance Scenarios**:

1. **Given** a prior run has an open run marker and no close event, **When** the app starts again, **Then** the system records `unclean_exit` for the unfinished run.
2. **Given** a strong business activity timestamp exists after the prior launch, **When** `unclean_exit` is created, **Then** the estimation metadata records which timestamp source was used.

---

### User Story 3 - Sync safely when connectivity returns (Priority: P1)

As the app, I need queued lifecycle telemetry to upload later without duplication or loss, so that remote reporting becomes eventually consistent.

**Why this priority**: Local-first without reliable later sync is incomplete.

**Independent Test**: Queue events while offline, restore connectivity, and verify that queued rows are sent and marked as synced exactly once from the local system's point of view.

**Acceptance Scenarios**:

1. **Given** unsent lifecycle rows exist in the outbox, **When** connectivity becomes available, **Then** the app uploads them in FIFO order and marks successful rows as sent.
2. **Given** the same queued row is retried after a partial failure, **When** the remote side receives it again, **Then** deduplication by event id prevents duplicate logical events.

---

### User Story 4 - Keep startup migrations safe (Priority: P1)

As an existing production user, I need startup-time database updates to run only when they are lightweight and safe, so that the app does not hang or corrupt data on weaker machines or larger databases.

**Why this priority**: The app already performs migrations at startup, and unsafe growth of that path will create production risk.

**Independent Test**: Classify pending migrations and verify that only startup-safe migrations run during splash, while heavier ones are blocked or deferred to a safer path.

**Acceptance Scenarios**:

1. **Given** only additive metadata migrations are pending, **When** the app starts, **Then** those migrations may run during splash on a background initialization task.
2. **Given** a heavy backfill or rebuild migration is pending, **When** the app starts, **Then** the app does not execute it automatically in the ordinary splash path.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST record lifecycle telemetry locally before any remote upload attempt.
- **FR-002**: The system MUST track lifecycle at the application/install level, not the person level.
- **FR-003**: The system MUST support `app_launch`, `app_close`, and `unclean_exit`.
- **FR-004**: The system MUST maintain an active run marker so an unfinished prior run can be detected at next startup.
- **FR-005**: The system MUST use only strong business activity timestamps when estimating an unclean-exit boundary.
- **FR-006**: The system MUST queue unsent telemetry locally and retry later when connectivity returns.
- **FR-007**: The system MUST use an idempotent event id so remote retries do not create duplicate logical events.
- **FR-008**: The startup migration pipeline MUST distinguish startup-safe migrations from heavy or destructive migrations.
- **FR-009**: The splash screen MUST communicate validation/update state truthfully.
- **FR-010**: Heavy backfills and destructive/rebuild migrations MUST NOT run automatically in the normal splash startup path.

### Non-Functional Requirements

- **NFR-001**: The design MUST remain additive-first and backward-compatible for mixed app versions.
- **NFR-002**: The design MUST stay small and privacy-aware.
- **NFR-003**: The design MUST tolerate offline operation without data loss in the normal retry model.
- **NFR-004**: The design MUST not require per-user Supabase tables.

## Key Entities

- **Installation**: The device/app installation identified by `install_id`.
- **Lifecycle Event**: An immutable telemetry record such as `app_launch`, `app_close`, or `unclean_exit`.
- **Run**: One execution of the application, correlated by `run_id`.
- **Outbox Record**: A local queued payload awaiting remote upload.
- **Migration Step**: A classified database update operation with explicit safety/cost category.
  - *StartupSafe*: Additive, non-breaking schema changes or metadata updates that can execute within 500ms.
  - *Splash Path*: The critical execution window between app process launch and the `MainWindow` initialization completion where user interaction is unavailable.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The app can record launch and close lifecycle events locally with no internet connection.
- **SC-002**: The app can detect an unfinished prior run and emit one `unclean_exit` for it on the next launch.
- **SC-003**: Queued lifecycle telemetry can be retried and remotely deduplicated by event id.
- **SC-004**: Startup-safe migrations continue to work on mixed-version installs without requiring destructive startup updates.
- **SC-005**: Heavy or rebuilding migrations are blocked from the ordinary splash path unless a dedicated maintenance path is used.

## Assumptions

- The current local database engine is SQLite, as shown by `Microsoft.Data.Sqlite` usage in the data project.
- The app already has local tables `installation_events`, `telemetry_outbox`, and `settings`.
- The exact remote Supabase schema, RLS policy model, and migration tooling require source confirmation.
