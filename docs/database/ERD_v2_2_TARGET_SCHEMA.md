# ERD v2.2 – Target physical schema (code contract)

Source design: `docs/reports/report3/report3_diagrams/ERD_Full_v2.mmd`.
Migration: `Services/OperationsService/UavPms.OperationsService.Infrastructure/Migrations/*_AlignSchemaWithErdV2_2.cs`.

Every service (Operations, AIInspection, Notification, Identity) maps the **same shared PostgreSQL database**.
Only OperationsService owns migrations. The other services must map tables/columns exactly as below
(they may map a subset of columns, never a column that does not exist).

Physical names are PascalCase (EF default). "Audit" = `CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt`.

## Dropped tables

`AssetComponents` (entity `Asset`), `AssetHealthHistories`, `TowerConditionHistories`, `LineSegments`,
`MissionTargetLines`, `TransmissionLines`, `Substations`, `ManagementUnits`.
`PreMissionAssessmentAssets` is replaced by `PreMissionAssessmentTowers`.
`AIAnalysisRequests` is renamed to `AIAnalysisRuns`.

## Changed tables

| Table | Change |
| :-- | :-- |
| `Users` | unchanged (`PasswordHash` only mapped by IdentityService). Unique `IX_Users_Email (Email)` and `IX_Users_Phone (Phone) WHERE Phone <> ''` (migration `AddUsersEmailPhoneUniqueIndexes`, run `scripts/db/cleanup_duplicate_users.sql` first) |
| `UserGeographicScopes` | Drop `SubstationId`, `TransmissionLineId`, `ManagementUnitId`. `RegionId` is **NOT NULL**, FK → `Regions`. Unique `(UserId, RegionId)` where `NOT IsDeleted` |
| `Regions` | Rename `RegionName` → `Name` |
| `Towers` | Drop `LineAssetId`, `LineCode`, `Voltage`, `RiskLevel`, `ActiveDefectsCount`, `LastInspectedAt`. `RegionId` **NOT NULL** |
| `UAVs` | Rename `Status` → `FlightStatus` (string enum `DroneStatus`). Drop `AssignedRegionName`, `TechnicalHealthUpdatedAt`. FK `AssignedRegionId` → `Regions`, FK `LastTechnicalInspectionId` → `DroneTechnicalInspections` |
| `DroneFlightControllers` | Drop `IsActive` (active = `UnpairedAt IS NULL`) |
| `DroneTechnicalInspections` | Rename `TechnicianUserId` → `TechnicianId` |
| `DroneTechnicalMetrics` | Drop `Critical` (use `Severity = 'Critical'`) |
| `InspectionSchedules` | Drop `CreatedByUserId` (use audit `CreatedBy`). CHECK `CK_InspectionSchedules_RecurrenceRule` (non-blank RRULE) |
| `PreMissionAssessments` | Drop `ConsumedByMissionId` (use `Missions.PreMissionAssessmentId`) |
| `PreMissionAssessmentTowers` (new) | `Id, AssessmentId, TowerId, Sequence` + audit. Unique `(AssessmentId, TowerId)` |
| `PreMissionAssessmentPersonnel` / `PreMissionAssessmentDrones` | Drop `IsEligible` |
| `Missions` | Drop `InspectorId`, `ScheduledStartAt`, `EmergencyReason`, `PriorityDefectsJson`, `IsOverdueNotified`. Add `OverdueNotifiedAt timestamptz NULL` |
| `MissionPriorityDefects` (new) | PK `(MissionId, CategoryId)`, no audit columns |
| `MissionTargets` | `AssetId` → `TowerId` (FK `Towers`). Unique `(MissionId, TowerId)` |
| `MissionAssignments` | Rename `AssignedByUserId` → `AssignedById` (FK `Users`) |
| `MissionCommunicationLogs` | Drop `SenderName`, `SenderRole` (derive from `SenderId → Users`; `SenderId IS NULL` = system) |
| `IncidentReports` | Drop `AssetId`, `FileUrl`. Keep `TowerId` (nullable) |
| `InspectionMedia` | Drop `AssetId`, `AiSource`. Keep `TowerId` (nullable). Add `Checksum varchar(64)` (unique when not null), `FileSizeBytes bigint`, `WidthPx int`, `HeightPx int`, `DurationSeconds double`, `Fps double` |
| `AIAnalysisRuns` (was `AIAnalysisRequests`) | Columns: `Id, MediaId (NOT NULL), ExecutionLocation ('Edge'/'Cloud'), UavId (NULL, FK UAVs), RequestedBy (NULL, FK Users; was UploadedBy), AnalysisType (string), ModelName, ModelVersion, Status (string: Pending/Processing/Completed/Failed), SourceEventId (unique), BatchId, ErrorMessage (was Notes), StartedAt, CompletedAt` + audit. Dropped: `FileUrl, MediaType, MissionId, AssetId, Result` |
| `DetectedAnomalies` | Add `AnalysisRunId uuid NULL` (FK `AIAnalysisRuns`, NULL = manual), `SupersededById uuid NULL` (self FK). `TowerId` becomes **uuid NULL FK `Towers`** (was varchar). Rename `Timestamp` → `FrameTimeSeconds`. `Gps jsonb` → `Location geometry(Point,4326)`. Drop `AssetId`, `AiSource`, `VideoDuration`, `VideoFps`, `VideoWidth`, `VideoHeight`. Unique `(AnalysisRunId, AiDetectionId)`. CHECK `ValidationStatus <> 'Confirmed' OR TowerId IS NOT NULL` (NOT VALID for legacy rows) |
| `EmergencyAlerts` | Drop `AssetId`, `TowerId`, `MissionId`, `DeliveryLatencySeconds`. Rename `ReviewedByUserId` → `ReviewedById`. Partial unique `UX_EmergencyAlerts_ActivePerAnomaly (AnomalyId) WHERE Status = 'Active' AND NOT IsDeleted` |
| `AlertEscalations` | CHECK `CK_AlertEscalations_DifferentUsers` (`EscalatedBy <> EscalatedTo`, NOT VALID if legacy rows violate it) |
| `MaintenanceTickets` | Drop `AssetId`, `TowerId` (tower = `Anomaly.TowerId`). `TechnicianId` **NULL** = not assigned yet (migration `MakeMaintenanceTicketTechnicianNullable`) |
| `MaintenanceProofs` | Drop `AfterRepairImageUrl` (one row per file, `ProofType` Before/After/Document) |
| `Notifications` | Drop `IsRead`, `IsPushed` (use `ReadAt`, `PushedAt`) |
| `Reports` | Drop `PdfFileUrl`, `PdfFileSize`, `ExcelFileUrl`, `ExcelFileSize`, `DefectCount`, `SubstationId`, `TransmissionLineId`. Add `RegionId uuid NULL` FK `Regions`, `Snapshot jsonb NULL` (frozen metadata + missions + anomalies upon approval) |
| `ReportMissions` | Rename `AssignedAt` → `AddedAt` |
| `OutboxMessages` | Drop audit columns (`CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsDeleted, DeletedAt`) |

Column lengths: the 48 server-controlled columns listed in `Persistence/Configurations/ErdColumnLengths.cs` are `varchar(n)` as in the ERD (migration `AddErdV2_2ConstraintsAndColumnLengths`); free-input columns stay `text` until the API validates their length.

## Final table list (42)

Users, Roles, UserRoles, UserGeographicScopes, RefreshTokens, TrustedDevices, Regions, Towers, UAVs,
DroneFlightControllers, DroneTechnicalInspections, DroneTechnicalMetrics, InspectionSchedules,
PreMissionAssessments, PreMissionAssessmentTowers, PreMissionAssessmentPersonnel, PreMissionAssessmentDrones,
Missions, MissionPriorityDefects, MissionTargets, MissionAssignments, ResourceBookings, MissionCommunicationLogs,
MissionCheckIns, DroneHandovers, MissionFlightLogs, IncidentReports, InspectionMedia, AIAnalysisRuns,
DefectCategories, DetectedAnomalies, EmergencyAlerts, AlertEscalations, MaintenanceTickets, MaintenanceProofs,
MaterialLogs, Reports, ReportMissions, Notifications, AuditLogs, SystemSettings, OutboxMessages.

## Behaviour rules

- Edge AI result (`VisionEdge`/MQTT): create one `AIAnalysisRuns` row with `ExecutionLocation = 'Edge'`,
  `UavId` = mission UAV, `Status = Completed`, then anomalies with `AnalysisRunId` = that run and
  `AiDetectionId` = edge TrackId.
- Cloud AI: create run `Pending` (`ExecutionLocation = 'Cloud'`, `RequestedBy` = user or NULL), set
  `StartedAt` when processing, `CompletedAt` + `Status` on callback, anomalies get `AnalysisRunId`.
- "AI source" shown in APIs is derived: `run.ExecutionLocation`/`run.ModelName`, or `"Manual"` when `AnalysisRunId IS NULL`.
- Anomaly must have `TowerId` before it can be `Confirmed`.
- Maintenance ticket tower = `ticket.Anomaly.TowerId`.
- Tickets auto-created on anomaly approval get the mission's active technician, or `TechnicianId = NULL`
  and `AssignedAt = NULL`. An unassigned ticket stays `Open`; it cannot move to `InProgress`.
- Reports have no stored files; PDF/Excel are generated on download from `DateFrom/DateTo/RegionId/ReportMissions` (or read from `Snapshot` if status is `Approved` for audit immutability).
- Incident reports have no file attachment.
- `InspectionMedia.FileUrl`, `MaintenanceProofs.FileUrl`, `DetectedAnomalies.ImageUrl/CropUrl` store a storage key (`bucket/object`) for files in MinIO; services rebuild the path-style public URL `{Minio:PublicBaseUrl}/{bucket}/{object}` on read (`UavPms.Shared.Contracts.Settings.StorageKeys` + EF value converter). Local paths (`/images/...`) and URLs of other hosts are stored as-is. Any service mapping these columns must apply the same converter.
