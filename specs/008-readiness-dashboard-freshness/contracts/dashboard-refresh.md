# Contract: Dashboard Refresh on Open

## Purpose

Define the dashboard freshness behavior required for each dashboard open cycle.

## Open-Cycle Behavior

When the dashboard page opens:

1. Read the last successful local dashboard snapshot, if one exists.
2. Show those last-known values immediately.
3. Start a new background refresh for the current open cycle.
4. Replace the shown values with fresh current-cycle values when refresh succeeds.

## Failure Behavior

If the current open-cycle refresh fails:

- keep the prior values visible when they exist
- mark the dashboard state as stale / refresh failed
- do not label those values as fresh for the current cycle

## No Live-Update Rule

- A successful or failed open-cycle refresh does not imply continuous live updates afterward.
- Underlying product or sales changes that happen while the page remains open do not require immediate mutation of the already open dashboard.
- A new open cycle or explicit refresh starts a new refresh attempt.

## Persistence Contract

- The last successful snapshot is stored locally under the application data root.
- The snapshot must not depend on any remote service.
- Stale state may be represented separately from the raw metric payload so the UI can distinguish "last known" from "fresh current-cycle" values.

## Performance Contract

For the representative local dataset:

- cached values should be visible immediately when present
- refreshed values should replace them within 5 seconds

## Readiness Verification Hooks

The readiness runner must prove:

1. baseline snapshot display on reopen when prior values exist
2. replacement with fresh values after underlying data changes
3. correct stale-state behavior when refresh fails
