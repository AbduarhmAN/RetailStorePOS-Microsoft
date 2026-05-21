# Implementation Plan: App Lifecycle Telemetry

**Branch**: `002-app-lifecycle-telemetry` | **Date**: 2026-04-20 | **Spec**: [spec.md](spec.md)  
**Input**: Agreed architecture for install-scoped lifecycle telemetry, durable offline sync, and startup-safe migration policy.

## Summary

This plan implements application-level lifecycle telemetry without turning the system into person tracking or session analytics.

The plan has three tracks:

1. **Lifecycle telemetry**: record `app_launch`, `app_close`, and `unclean_exit` locally first.
2. **Durable sync**: convert the current event insert + outbox flow into a transactional outbox flow.
3. **Migration safety**: stop treating all startup migrations as equal and classify them into startup-safe versus heavy.

## Technical Context

**Language/Version**: C# / .NET desktop app  
**Primary Dependencies**: WinUI 3, Microsoft.Data.Sqlite, HttpClient  
**Storage**: Local SQLite + remote Supabase REST integration  
**Execution Model**: App startup background initialization + in-process background sync loop  
**Current Constraints**:

- no destructive schema changes in rollout 1
- no build/compile step in this planning artifact
- current remote schema details require confirmation

## Constitution Check

1. **Production Safety**: Must remain additive-first and rollback-aware.  
2. **Offline First**: Local write before remote upload.  
3. **Privacy Boundaries**: App/install scoped only.  
4. **Startup Reliability**: Heavy migrations cannot silently live in the splash path.  
5. **Version Drift Tolerance**: Mixed-version installs must remain survivable.  

## Project Structure

### Documentation

```text
specs/002-app-lifecycle-telemetry/
├── spec.md
├── research.md
├── data-model.md
├── plan.md
├── quickstart.md
└── tasks.md
```

### Expected Source Areas

```text
Nexill.RetailStorePOS/
├── App.xaml.cs
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── LoginRuntime.cs
└── Services/
    └── TelemetryService.cs

RetailStorePOS.Data/
├── Class1.cs
└── Repositories/
    ├── InstallationEventRepository.cs
    └── TelemetryOutboxRepository.cs
```

## Phase 0: Stabilize the Current Baseline

- Remove the startup outbox purge in `LoginRuntime.Initialize()`.
- Keep the existing tables.
- Do not add destructive startup migrations.

## Phase 1: Lifecycle Model

- Introduce `run_id` in event payloads.
- On launch:
  - detect unfinished prior run
  - emit `unclean_exit` if required
  - create new active run marker
  - record `app_launch`
- On close:
  - record `app_close`
  - clear active run marker
- On strong business activity:
  - update last activity timestamp/source in `settings`

## Phase 2: Durable Local Write Path

- Refactor repositories so event insert and outbox enqueue can share one SQLite transaction.
- Treat the local event log and local outbox as one atomic unit of work.

## Phase 3: Sync Coordinator

- Keep startup-triggered flush.
- Keep connectivity-triggered flush.
- Keep periodic timer flush.
- Use event id as the remote idempotency key.

## Phase 4: Startup Migration Classification

- Replace implicit “run all migrations at startup” with explicit step categories:
  - `StartupSafe`
  - `HeavyBackfill`
  - `RebuildOrDestructive`
- Allow only `StartupSafe` steps in the ordinary splash path.

## Phase 5: Optional Schema Hardening

- Add explicit schema version tracking.
- Optionally add first-class `run_id` and `(install_id, occurred_at)` indexing if query pressure justifies it.

## Out of Scope For This Feature

- per-user telemetry
- per-user Supabase tables
- clickstream
- feature usage analytics
- remote destructive schema cleanup
