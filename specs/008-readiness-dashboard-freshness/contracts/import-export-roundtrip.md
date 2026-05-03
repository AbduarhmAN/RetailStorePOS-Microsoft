# Contract: Import/Export Round Trip

## Purpose

Define the current user-accessible import/export verification surface for this feature.

## In-Scope Workflow

The current desktop workflow in scope is the product CSV flow exposed by:

- `ProductsPage` import button
- `ProductsPage` export button
- `ProductImportService`
- `ProductExportService`

## Valid Round-Trip Contract

For a representative product sample:

1. Export the current product catalog to CSV.
2. Re-import a supported CSV sample through the current import path.
3. Compare the in-scope fields after the round trip.

## In-Scope Field Coverage

The round-trip comparison must cover the fields currently represented by `ProductCsvRow`, including:

- product identity and names
- barcode / SKU when present
- unit
- selling and cost prices
- quantities and thresholds represented by the CSV contract
- tax-related assignment values carried by the current CSV shape
- image-path behavior when the sample includes supported image references

## Invalid Input Contract

Malformed or incomplete CSV samples must:

- fail explicitly
- preserve previously stored product rows
- avoid silent partial corruption of existing catalog data

## Artifact Contract

The readiness runner may generate:

- exported CSV output
- valid re-import source CSV
- invalid sample CSV
- comparison/diff artifacts

All such artifacts must remain in the isolated readiness workspace.
