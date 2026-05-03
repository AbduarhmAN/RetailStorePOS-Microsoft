# Contract: Readiness Automation

## Purpose

Define the automated readiness surface for the current desktop app so startup, data access, checkout, tax, import/export, and dashboard reopen freshness can be evaluated without guided manual steps.

## Scope

The readiness catalog covers:

- runtime bootstrap and usable-shell startup
- local database read/write access
- login/auth flow
- product/catalog lookup
- checkout/sales persistence
- tax calculation
- current user-accessible import/export workflow
- dashboard reopen freshness

## Dataset Profiles

### `minimal`

- Near-empty local SQLite dataset
- Exercises first-run or empty-state behavior
- Uses minimal import samples

### `representative`

- Populated local SQLite dataset
- Exercises realistic catalog, sales, and reporting behavior
- Includes representative export/import samples

## Isolation Rules

- The readiness runner MUST use a working SQLite file under the readiness app-data root.
- The runner MUST NOT mutate the operator's active production/runtime database directly.
- Import/export artifacts generated during readiness MUST stay under the readiness workspace.

## Scenario Result Contract

Every scenario emits:

- `ScenarioKey`
- `Status`: `Passed`, `Failed`, or `Skipped`
- `Severity`: `Blocking` or `Observation`
- `DurationMs`
- `Summary`
- `FailureCode` when applicable
- `ArtifactPaths` when applicable

## Blocking Semantics

- Failed blocking scenarios mark the readiness run as not ready.
- Non-blocking observations remain visible in the report but do not grant false success to failed core workflows.
- Skips are allowed only when explicitly justified by dataset profile or unavailable optional context.

## Output Contract

Each run writes one local machine-readable report containing:

- run metadata
- dataset profile
- per-scenario results
- aggregate blocking and observation counts
- overall readiness status

## Current In-Scope Scenario Families

- `startup.bootstrap`
- `database.local-read-write`
- `auth.login`
- `catalog.lookup`
- `checkout.complete-sale`
- `tax.calculate`
- `products.export.valid`
- `products.import.valid-roundtrip`
- `products.import.invalid-safe-failure`
- `dashboard.reopen-refresh`
