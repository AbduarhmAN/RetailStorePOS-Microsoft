# Quickstart: Agreed Direction

## What This Feature Is

This is **application lifecycle telemetry**:

- app launch
- app close
- unclean exit
- install/device context
- strong business activity only for crash-boundary estimation

## What This Feature Is Not

- person tracking
- login/session analytics
- page click tracking
- per-user remote tables

## Local-First Rule

1. Write lifecycle event locally.
2. Queue remote payload locally.
3. Upload later when online.
4. Mark sent only after remote success.

## Startup Rule

- Startup-safe migrations may run during splash.
- Heavy backfills and destructive/rebuild migrations do not belong in the ordinary splash path.

## Minimum Rollout-1 Changes

- reuse `installation_events`
- reuse `telemetry_outbox`
- reuse `settings`
- add active-run state in `settings`
- add `run_id` and crash-estimation metadata inside `payload_json`
- remove startup outbox deletion
- make event insert + outbox enqueue transactional

## Strong Activity Signals

- `sales.created_at`
- meaningful `audit_logs.created_at`

## Weak Signals To Avoid As Primary Crash Boundary

- `products.last_sale_at`
- `products.purchased_at`

## Remote Storage Shape

- one shared `installations` table
- one shared `installation_events` table
- deduplicate by event `id`

## Requires Confirmation

- remote Supabase DDL
- remote RLS/security model
- any numeric device-capability threshold for startup migration gating
