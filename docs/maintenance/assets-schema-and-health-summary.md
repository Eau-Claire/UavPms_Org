# Assets Schema and Health Summary Maintenance

## Purpose

This note records the production compatibility findings and the changes made to the Assets health dashboard. It is an operational reference; it does not replace a reviewed EF Core migration plan.

## Current database ownership

The application currently shares one PostgreSQL database among Operations, AI Inspection, Identity, and Notification services. The following groups are active application data and must not be dropped as database cleanup:

- Identity and access: `Users`, `Roles`, `UserRoles`, `RefreshTokens`, `TrustedDevices`.
- Asset and GIS graph: `Regions`, `ManagementUnits`, `Substations`, `TransmissionLines`, `Towers`, `AssetComponents`, `LineSegments`.
- Inspection and AI: `Missions`, `MissionTargets`, `MissionTargetLines`, `MissionFlightLogs`, `InspectionMedia`, `DefectCategories`, `DetectedAnomalies`, `AIAnalysisRequests`, `OutboxMessages`.
- Operations: `EmergencyAlerts`, `AlertEscalations`, `IncidentReports`, `MaintenanceTickets`, `MaintenanceProofs`, `MaterialLogs`, `Notifications`, `AuditLogs`, `UAVs`, `AssetHealthHistories`.
- System tables: `__EFMigrationsHistory` and PostGIS `spatial_ref_sys`.

`TowerHealthHistories` and `session` are legacy candidates only. Do not drop either until its owner is verified, a backup is taken, and dependent external services are checked. `TowerHealthHistories` currently has a foreign key to `Towers`.

OperationsService is the sole EF migration owner. AI Inspection, Identity, and Notification now ignore `RunMigrations` and log a warning if it is set, preventing multiple services from racing to alter the shared schema.

## Confirmed schema drift

Production has schema changes that are not recorded by the OperationsService EF migration history. Two confirmed compatibility gaps are:

1. `AssetComponents` needs `PowerLineId`, `ManagementUnitId`, and `Location` for the current Assets model.
2. The current code reads `DetectedAnomalies.AssetId`, while production legacy data used `DetectedAnomalies.ComponentId`.

Do not run the broad `20260902033000_AddGisAssetSelection` migration against this production database. It includes unrelated table renames and destructive MissionTargets operations, and its history entry would not accurately represent the existing schema.

## Safe production repair workflows

These manually confirmed, idempotent workflows are on the backend repository `main` branch:

| Workflow | Confirmation value | Effect |
| --- | --- | --- |
| `Repair Production AssetComponents Schema` | `REPAIR_ASSET_COMPONENTS_SCHEMA` | Adds only missing GIS AssetComponents columns/indexes and safely backfills values from Towers. |
| `Repair Production Detected Anomalies Schema` | `REPAIR_DETECTED_ANOMALIES_ASSET_ID` | Adds `DetectedAnomalies.AssetId`, backfills it from legacy `ComponentId`, then adds the index and foreign key. |
| `Diagnose Production Assets API` | `DIAGNOSE_ASSETS_API` | Read-only inventory, required-column validation, migration history, and exception context. |

Run the Detected Anomalies repair before expecting `GET /api/v1/assets` to succeed: the paginated endpoint queries confirmed defects using `AssetId`.

## Health summary behavior

The dashboard KPI must use `GET /api/v1/assets/health-summary`; it must not average the items returned by a paginated `GET /api/v1/assets` request.

The backend implementation on `refactor/backend-cleanup` aggregates all non-deleted assets in PostgreSQL for totals, risk counts, and average health score. It separately loads at most ten critical assets for display. The frontend calls the summary endpoint independently, so changing pages cannot change the overall KPI.

The frontend now also sends risk-level and sort choices to the paginated Assets API. Search text remains page-local until an explicit server-side search API contract is added.

The Assets query validates supported sort fields and sort directions, returning a validation response for unsupported values instead of silently applying a default order.

## Validation completed

- OperationsService API build: succeeded with zero warnings and zero errors.
- Frontend production build: succeeded. Existing stylesheet-budget/CommonJS warnings remain unrelated.
- Frontend unit test runner: 75 tests pass. The Auth interceptor tests now supply the same minimal in-memory Node `localStorage` shim used by the existing Auth tests.

## Next reconciliation phase

Before applying future EF migrations, create one reviewed reconciliation migration from an audited production schema snapshot. It should be additive wherever possible, be tested on a restored production backup, and update migration history only after the exact schema changes succeed. Do not delete legacy tables as part of this phase.
