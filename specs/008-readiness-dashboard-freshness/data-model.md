# Data Model: Operational Readiness and Dashboard Freshness

## Entity: ReadinessRun

| Field | Type | Description |
|-------|------|-------------|
| `RunId` | string | Stable identifier for one readiness execution. |
| `StartedAtUtc` | datetime | UTC timestamp when the run begins. |
| `CompletedAtUtc` | datetime? | UTC timestamp when the run finishes. |
| `DatasetProfileKey` | string | `minimal` or `representative`. |
| `TriggerSource` | enum | `Operator`, `StartupHook`, or `Diagnostics`. |
| `OverallStatus` | enum | `Passed`, `Failed`, or `CompletedWithObservations`. |
| `BlockingFailureCount` | int | Number of failed blocking scenarios. |
| `ObservationCount` | int | Number of non-blocking warnings or skips. |
| `ReportPath` | string | Local machine-readable report artifact path. |

**Validation Rules**

- `RunId` must be unique per execution.
- `OverallStatus` cannot be `Passed` when `BlockingFailureCount > 0`.
- `ReportPath` must resolve to a local path under the application data root.

## Entity: VerificationScenario

| Field | Type | Description |
|-------|------|-------------|
| `ScenarioKey` | string | Stable identifier such as `startup.bootstrap` or `products.import.valid-roundtrip`. |
| `DisplayName` | string | Human-readable scenario name. |
| `Priority` | enum | `P1` or `P2`. |
| `BlockingOnFail` | boolean | Whether a failure blocks readiness. |
| `DatasetProfiles` | list | Dataset profiles the scenario must run against. |
| `ExecutionOrder` | int | Ordering inside a readiness run. |
| `RequiredContracts` | list | Module contracts or workflow boundaries exercised by the scenario. |

**Validation Rules**

- `ScenarioKey` must be unique.
- Every `P1` scenario must produce an explicit result.
- A scenario marked `BlockingOnFail` must map to a blocking readiness failure on `Failed`.

## Entity: ScenarioResult

| Field | Type | Description |
|-------|------|-------------|
| `RunId` | string | Parent readiness run identifier. |
| `ScenarioKey` | string | Scenario being evaluated. |
| `Status` | enum | `Passed`, `Failed`, or `Skipped`. |
| `Severity` | enum | `Blocking` or `Observation`. |
| `DurationMs` | int | Execution time for the scenario. |
| `Summary` | string | Short human-readable outcome. |
| `FailureCode` | string? | Stable failure identifier when the scenario fails. |
| `ArtifactPaths` | list | Supporting local files such as exports, snapshots, or diff reports. |

**Validation Rules**

- `Severity` must match the parent scenario's blocking policy.
- `DurationMs` must be non-negative.
- Failed results should include either `FailureCode` or a non-empty `Summary`.

## Entity: DatasetProfile

| Field | Type | Description |
|-------|------|-------------|
| `ProfileKey` | string | Stable key such as `minimal` or `representative`. |
| `DisplayName` | string | Human-readable name. |
| `WorkingDatabasePath` | string | Local SQLite file used during the run. |
| `SeedSource` | string | Source seed or clone strategy for the dataset. |
| `ImportSampleDirectory` | string | Local folder containing import samples for the profile. |
| `ExpectedScale` | string | Short description of dataset size/intent. |

**Validation Rules**

- `WorkingDatabasePath` and `ImportSampleDirectory` must remain local.
- The readiness runner must not point `WorkingDatabasePath` at the operator's active runtime database.

## Entity: ImportExportSample

| Field | Type | Description |
|-------|------|-------------|
| `WorkflowKey` | string | Current import/export workflow identifier. |
| `SampleType` | enum | `ValidRoundTrip` or `InvalidInput`. |
| `InputPath` | string | Source CSV or generated export path. |
| `ExpectedFieldCoverage` | list | In-scope fields that must survive round-trip validation. |
| `IncludesImageReferences` | boolean | Whether image-path behavior is part of the sample. |
| `ExpectedOutcome` | enum | `ImportSucceeds`, `ImportRejected`, or `ExportSucceeds`. |

**Validation Rules**

- `WorkflowKey` must map to a current user-accessible desktop flow.
- Invalid samples must not require mutation of already stored business rows to pass.

## Entity: DashboardSnapshot

| Field | Type | Description |
|-------|------|-------------|
| `SnapshotKey` | string | Stable identifier for the cached dashboard payload. |
| `CapturedAtUtc` | datetime | UTC time of the successful refresh that produced the snapshot. |
| `DatasetFingerprint` | string | Fingerprint for the source dataset or metric basis. |
| `MetricsPayload` | object | Serialized dashboard card values and chart inputs. |
| `SourceRunId` | string? | Optional readiness run that captured the snapshot. |
| `IsStale` | boolean | Whether the snapshot is currently flagged stale after a failed refresh. |

**Validation Rules**

- `MetricsPayload` must contain only local reporting data needed for the dashboard cards.
- `IsStale` must be `true` after a failed refresh when old values remain visible.

## Entity: DashboardRefreshCycle

| Field | Type | Description |
|-------|------|-------------|
| `CycleId` | string | Stable identifier for one page-open refresh attempt. |
| `OpenedAtUtc` | datetime | When the dashboard open cycle began. |
| `SnapshotShownAtUtc` | datetime? | When the cached snapshot became visible, if any. |
| `FreshDataCompletedAtUtc` | datetime? | When the current refresh completed successfully. |
| `Status` | enum | `Loading`, `Succeeded`, `Failed`, or `SucceededWithStaleFallbackCleared`. |
| `FailureMessage` | string? | Failure summary when refresh fails. |
| `ReplacedSnapshot` | boolean | Whether current-cycle data replaced a prior snapshot. |

**Validation Rules**

- A cycle begins every time the dashboard page opens.
- A cycle may show cached data first, but only fresh current-cycle data can clear stale status.
- A cycle does not imply continuous live updates after completion.

## Relationships

- One `ReadinessRun` has many `ScenarioResult` records.
- One `VerificationScenario` can execute against multiple `DatasetProfile` records.
- One `ReadinessRun` can produce multiple `ImportExportSample` artifacts.
- One `DashboardRefreshCycle` may consume one prior `DashboardSnapshot` and replace it with a newer successful snapshot.
