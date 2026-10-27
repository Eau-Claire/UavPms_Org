using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <summary>
    /// Aligns the shared database with ERD v2.2 (docs/reports/report3/report3_diagrams/ERD_Full_v2.mmd,
    /// docs/database/ERD_v2_2_TARGET_SCHEMA.md).
    ///
    /// The scaffolded operations would have dropped and recreated renamed tables/columns, losing data,
    /// so the Up() is hand-written SQL that runs in one transaction in three phases:
    /// expand (new columns/tables) → migrate (backfill from the old structures) → contract (drop the old ones).
    /// Constraint and index names match the EF model snapshot so later migrations keep working.
    ///
    /// Down() is intentionally not supported: the contract phase drops data (asset/line/substation tables,
    /// health history, stored report files). Restore the pg_dump taken by the
    /// "Migrate Database (ERD v2.2)" workflow instead.
    /// </summary>
    public partial class AlignSchemaWithErdV2_2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // 0. Objects created outside EF (scripts/migrations/001_*.sql) that depend on dropped tables
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF to_regclass('public.towers') IS NOT NULL THEN
                        EXECUTE 'DROP TRIGGER IF EXISTS trg_sync_towers_assets ON public.towers';
                    END IF;
                END $$;
                DROP FUNCTION IF EXISTS trg_sync_tower_to_asset();
                DROP VIEW IF EXISTS v_assets_compat;
                DROP VIEW IF EXISTS v_substations_compat;
                DROP VIEW IF EXISTS v_transmission_lines_compat;
                """);

            // ---------------------------------------------------------------
            // 1. Regions: RegionName -> Name
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Regions" RENAME COLUMN "RegionName" TO "Name";
                ALTER TABLE "Regions" ALTER COLUMN "Name" TYPE character varying(150) USING left("Name", 150);
                """);

            // ---------------------------------------------------------------
            // 2. Towers: every tower belongs directly to a region (RegionId NOT NULL)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                -- 2a. region of the old line's substation
                UPDATE "Towers" t
                SET "RegionId" = s."RegionAssetId"
                FROM "TransmissionLines" l
                JOIN "Substations" s ON s."Id" = l."SubstationAssetId"
                WHERE t."RegionId" IS NULL AND t."LineAssetId" = l."Id";

                -- 2b. smallest region polygon that contains the tower
                UPDATE "Towers" t
                SET "RegionId" = (
                    SELECT r."Id" FROM "Regions" r
                    WHERE NOT r."IsDeleted" AND r."Geom" IS NOT NULL AND t."Geom" IS NOT NULL
                      AND ST_Contains(r."Geom", ST_SetSRID(t."Geom", ST_SRID(r."Geom")))
                    ORDER BY ST_Area(r."Geom") ASC
                    LIMIT 1)
                WHERE t."RegionId" IS NULL;

                -- 2c. anything left goes to an explicit "unassigned" region so it can be fixed in the UI
                INSERT INTO "Regions" ("Id", "Code", "Name", "Type", "CreatedAt", "IsDeleted")
                SELECT gen_random_uuid(), 'UNASSIGNED', 'Chưa phân khu vực', 'Area', now(), false
                WHERE EXISTS (SELECT 1 FROM "Towers" WHERE "RegionId" IS NULL)
                  AND NOT EXISTS (SELECT 1 FROM "Regions" WHERE "Code" = 'UNASSIGNED');

                UPDATE "Towers"
                SET "RegionId" = (SELECT "Id" FROM "Regions" WHERE "Code" = 'UNASSIGNED' LIMIT 1)
                WHERE "RegionId" IS NULL;

                ALTER TABLE "Towers" DROP CONSTRAINT IF EXISTS "FK_Towers_Regions_RegionId";
                ALTER TABLE "Towers" ALTER COLUMN "RegionId" SET NOT NULL;
                ALTER TABLE "Towers" ADD CONSTRAINT "FK_Towers_Regions_RegionId"
                    FOREIGN KEY ("RegionId") REFERENCES "Regions" ("Id") ON DELETE RESTRICT;
                """);

            // ---------------------------------------------------------------
            // 3. asset_id -> tower_id everywhere (before AssetComponents is dropped)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                -- 3a. MissionTargets.AssetId -> TowerId
                ALTER TABLE "MissionTargets" DROP CONSTRAINT IF EXISTS "FK_MissionTargets_AssetComponents_AssetId";
                DROP INDEX IF EXISTS "IX_MissionTargets_MissionId_AssetId";
                DROP INDEX IF EXISTS "IX_MissionTargets_AssetId";
                UPDATE "MissionTargets" mt SET "AssetId" = a."TowerId"
                FROM "AssetComponents" a WHERE mt."AssetId" = a."Id";
                DELETE FROM "MissionTargets" mt
                WHERE NOT EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = mt."AssetId");
                DELETE FROM "MissionTargets" mt
                USING (
                    SELECT "Id", row_number() OVER (PARTITION BY "MissionId", "AssetId" ORDER BY "Sequence", "CreatedAt") AS rn
                    FROM "MissionTargets") d
                WHERE mt."Id" = d."Id" AND d.rn > 1;
                ALTER TABLE "MissionTargets" RENAME COLUMN "AssetId" TO "TowerId";
                CREATE UNIQUE INDEX "IX_MissionTargets_MissionId_TowerId" ON "MissionTargets" ("MissionId", "TowerId");
                CREATE INDEX "IX_MissionTargets_TowerId" ON "MissionTargets" ("TowerId");
                ALTER TABLE "MissionTargets" ADD CONSTRAINT "FK_MissionTargets_Towers_TowerId"
                    FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE RESTRICT;

                -- 3b. PreMissionAssessmentAssets -> PreMissionAssessmentTowers
                CREATE TABLE "PreMissionAssessmentTowers" (
                    "Id" uuid NOT NULL,
                    "AssessmentId" uuid NOT NULL,
                    "TowerId" uuid NOT NULL,
                    "Sequence" integer NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    "CreatedBy" uuid NULL,
                    "UpdatedBy" uuid NULL,
                    "IsDeleted" boolean NOT NULL,
                    "DeletedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_PreMissionAssessmentTowers" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_PreMissionAssessmentTowers_PreMissionAssessments_Assessment~"
                        FOREIGN KEY ("AssessmentId") REFERENCES "PreMissionAssessments" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_PreMissionAssessmentTowers_Towers_TowerId"
                        FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE RESTRICT
                );
                INSERT INTO "PreMissionAssessmentTowers"
                    ("Id", "AssessmentId", "TowerId", "Sequence", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt")
                SELECT DISTINCT ON (x."AssessmentId", x.tower_id)
                    x."Id", x."AssessmentId", x.tower_id, x."Sequence", x."CreatedAt", x."UpdatedAt", x."CreatedBy", x."UpdatedBy", x."IsDeleted", x."DeletedAt"
                FROM (
                    SELECT pa.*, COALESCE(a."TowerId", t."Id") AS tower_id
                    FROM "PreMissionAssessmentAssets" pa
                    LEFT JOIN "AssetComponents" a ON a."Id" = pa."AssetId"
                    LEFT JOIN "Towers" t ON t."Id" = pa."AssetId") x
                WHERE x.tower_id IS NOT NULL
                ORDER BY x."AssessmentId", x.tower_id, x."Sequence";
                CREATE UNIQUE INDEX "IX_PreMissionAssessmentTowers_AssessmentId_TowerId" ON "PreMissionAssessmentTowers" ("AssessmentId", "TowerId");
                CREATE INDEX "IX_PreMissionAssessmentTowers_TowerId" ON "PreMissionAssessmentTowers" ("TowerId");

                -- 3c. InspectionMedia / IncidentReports: keep TowerId, fill it from the old asset
                UPDATE "InspectionMedia" m SET "TowerId" = a."TowerId"
                FROM "AssetComponents" a WHERE m."TowerId" IS NULL AND m."AssetId" = a."Id";
                UPDATE "IncidentReports" i SET "TowerId" = a."TowerId"
                FROM "AssetComponents" a WHERE i."TowerId" IS NULL AND i."AssetId" = a."Id";

                -- 3d. DetectedAnomalies.TowerId: varchar(128) -> uuid FK
                ALTER TABLE "DetectedAnomalies" ADD COLUMN "TowerIdNew" uuid NULL;
                UPDATE "DetectedAnomalies" d SET "TowerIdNew" = a."TowerId"
                FROM "AssetComponents" a WHERE d."AssetId" = a."Id";
                UPDATE "DetectedAnomalies" d SET "TowerIdNew" = t."Id"
                FROM "Towers" t
                WHERE d."TowerIdNew" IS NULL AND d."TowerId" IS NOT NULL
                  AND (t."TowerCode" = d."TowerId" OR t."Id"::text = lower(d."TowerId"));
                UPDATE "DetectedAnomalies" d SET "TowerIdNew" = mt."TowerId"
                FROM "MaintenanceTickets" mt
                WHERE d."TowerIdNew" IS NULL AND mt."AnomalyId" = d."Id" AND mt."TowerId" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = mt."TowerId");
                UPDATE "DetectedAnomalies" d SET "TowerIdNew" = ea."TowerId"
                FROM "EmergencyAlerts" ea
                WHERE d."TowerIdNew" IS NULL AND ea."AnomalyId" = d."Id" AND ea."TowerId" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = ea."TowerId");
                UPDATE "DetectedAnomalies" d SET "TowerIdNew" = m."TowerId"
                FROM "InspectionMedia" m
                WHERE d."TowerIdNew" IS NULL AND d."MediaId" = m."Id" AND m."TowerId" IS NOT NULL;
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "TowerId";
                ALTER TABLE "DetectedAnomalies" RENAME COLUMN "TowerIdNew" TO "TowerId";
                """);

            // ---------------------------------------------------------------
            // 4. UserGeographicScopes: region only
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "UserGeographicScopes" DROP CONSTRAINT IF EXISTS "CK_UserGeographicScopes_HasScope";

                UPDATE "UserGeographicScopes" g SET "RegionId" = s."RegionAssetId"
                FROM "Substations" s WHERE g."RegionId" IS NULL AND g."SubstationId" = s."Id";
                UPDATE "UserGeographicScopes" g SET "RegionId" = s."RegionAssetId"
                FROM "TransmissionLines" l JOIN "Substations" s ON s."Id" = l."SubstationAssetId"
                WHERE g."RegionId" IS NULL AND g."TransmissionLineId" = l."Id";
                -- a management-unit scope becomes one region scope per region its lines' towers are in
                INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "CreatedBy", "IsDeleted")
                SELECT gen_random_uuid(), x."UserId", x."RegionId", now(), x."CreatedBy", false
                FROM (
                    SELECT DISTINCT g."UserId", t."RegionId", g."CreatedBy"
                    FROM "UserGeographicScopes" g
                    JOIN "TransmissionLines" l ON l."ManagementUnitId" = g."ManagementUnitId"
                    JOIN "Towers" t ON t."LineAssetId" = l."Id"
                    WHERE g."RegionId" IS NULL AND g."ManagementUnitId" IS NOT NULL AND NOT g."IsDeleted") x;
                DELETE FROM "UserGeographicScopes" WHERE "RegionId" IS NULL;
                DELETE FROM "UserGeographicScopes" g
                USING (
                    SELECT "Id", row_number() OVER (PARTITION BY "UserId", "RegionId" ORDER BY "IsDeleted", "CreatedAt") AS rn
                    FROM "UserGeographicScopes") d
                WHERE g."Id" = d."Id" AND d.rn > 1;

                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_RegionId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_SubstationId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_TransmissionLineId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_ManagementUnitId";
                ALTER TABLE "UserGeographicScopes" DROP COLUMN "SubstationId";
                ALTER TABLE "UserGeographicScopes" DROP COLUMN "TransmissionLineId";
                ALTER TABLE "UserGeographicScopes" DROP COLUMN "ManagementUnitId";
                ALTER TABLE "UserGeographicScopes" ALTER COLUMN "RegionId" SET NOT NULL;
                CREATE INDEX "IX_UserGeographicScopes_RegionId" ON "UserGeographicScopes" ("RegionId");
                CREATE UNIQUE INDEX "IX_UserGeographicScopes_UserId_RegionId" ON "UserGeographicScopes" ("UserId", "RegionId") WHERE NOT "IsDeleted";
                ALTER TABLE "UserGeographicScopes" ADD CONSTRAINT "FK_UserGeographicScopes_Regions_RegionId"
                    FOREIGN KEY ("RegionId") REFERENCES "Regions" ("Id") ON DELETE CASCADE;
                """);

            // ---------------------------------------------------------------
            // 5. Reports: region scope, no stored files, no frozen defect count
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Reports" ADD COLUMN "RegionId" uuid NULL;
                UPDATE "Reports" r SET "RegionId" = s."RegionAssetId"
                FROM "Substations" s WHERE r."SubstationId" = s."Id";
                UPDATE "Reports" r SET "RegionId" = s."RegionAssetId"
                FROM "TransmissionLines" l JOIN "Substations" s ON s."Id" = l."SubstationAssetId"
                WHERE r."RegionId" IS NULL AND r."TransmissionLineId" = l."Id";
                ALTER TABLE "Reports" DROP COLUMN "SubstationId";
                ALTER TABLE "Reports" DROP COLUMN "TransmissionLineId";
                ALTER TABLE "Reports" DROP COLUMN "PdfFileUrl";
                ALTER TABLE "Reports" DROP COLUMN "PdfFileSize";
                ALTER TABLE "Reports" DROP COLUMN "ExcelFileUrl";
                ALTER TABLE "Reports" DROP COLUMN "ExcelFileSize";
                ALTER TABLE "Reports" DROP COLUMN "DefectCount";
                CREATE INDEX "IX_Reports_RegionId" ON "Reports" ("RegionId");
                ALTER TABLE "Reports" ADD CONSTRAINT "FK_Reports_Regions_RegionId"
                    FOREIGN KEY ("RegionId") REFERENCES "Regions" ("Id") ON DELETE SET NULL;

                ALTER TABLE "ReportMissions" RENAME COLUMN "AssignedAt" TO "AddedAt";
                """);

            // ---------------------------------------------------------------
            // 6. Missions: inspector -> assignments, priority defects -> junction, flags -> timestamps
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Missions" ADD COLUMN "OverdueNotifiedAt" timestamp with time zone NULL;
                UPDATE "Missions" SET "OverdueNotifiedAt" = COALESCE("UpdatedAt", "CreatedAt") WHERE "IsOverdueNotified";
                UPDATE "Missions" SET "PlannedStart" = "ScheduledStartAt" WHERE "PlannedStart" IS NULL AND "ScheduledStartAt" IS NOT NULL;
                UPDATE "Missions" SET "TriggerReason" = "EmergencyReason"
                WHERE ("TriggerReason" IS NULL OR btrim("TriggerReason") = '') AND "EmergencyReason" IS NOT NULL;

                -- inspector that is not yet an assignment becomes an INSPECTOR assignment
                INSERT INTO "MissionAssignments"
                    ("Id", "MissionId", "UserId", "AssignmentRole", "Status", "ResponseStatus", "IsRequired",
                     "AssignedAt", "AssignedByUserId", "Version", "CreatedAt", "CreatedBy", "IsDeleted")
                SELECT gen_random_uuid(), m."Id", m."InspectorId", 'INSPECTOR', 'Active',
                       CASE WHEN m."AcceptedAt" IS NOT NULL THEN 'Accepted' ELSE 'Pending' END,
                       true, m."CreatedAt", m."ManagerId", 1, now(), m."ManagerId", false
                FROM "Missions" m
                WHERE m."InspectorId" IS NOT NULL AND NOT m."IsDeleted"
                  AND EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = m."InspectorId")
                  AND NOT EXISTS (SELECT 1 FROM "MissionAssignments" a
                                  WHERE a."MissionId" = m."Id" AND a."UserId" = m."InspectorId" AND NOT a."IsDeleted");

                CREATE TABLE "MissionPriorityDefects" (
                    "MissionId" uuid NOT NULL,
                    "CategoryId" integer NOT NULL,
                    CONSTRAINT "PK_MissionPriorityDefects" PRIMARY KEY ("MissionId", "CategoryId"),
                    CONSTRAINT "FK_MissionPriorityDefects_DefectCategories_CategoryId"
                        FOREIGN KEY ("CategoryId") REFERENCES "DefectCategories" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_MissionPriorityDefects_Missions_MissionId"
                        FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX "IX_MissionPriorityDefects_CategoryId" ON "MissionPriorityDefects" ("CategoryId");

                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN SELECT "Id", "PriorityDefectsJson" FROM "Missions"
                             WHERE "PriorityDefectsJson" IS NOT NULL AND btrim("PriorityDefectsJson") NOT IN ('', '[]')
                    LOOP
                        BEGIN
                            INSERT INTO "MissionPriorityDefects" ("MissionId", "CategoryId")
                            SELECT DISTINCT r."Id", c."Id"
                            FROM jsonb_array_elements_text(r."PriorityDefectsJson"::jsonb) AS e(v)
                            JOIN "DefectCategories" c
                              ON lower(c."CategoryCode") = lower(btrim(e.v)) OR lower(c."CategoryName") = lower(btrim(e.v))
                            ON CONFLICT DO NOTHING;
                        EXCEPTION WHEN others THEN
                            RAISE NOTICE 'Skipping malformed PriorityDefectsJson for mission %', r."Id";
                        END;
                    END LOOP;
                END $$;

                ALTER TABLE "Missions" DROP COLUMN "InspectorId";
                ALTER TABLE "Missions" DROP COLUMN "ScheduledStartAt";
                ALTER TABLE "Missions" DROP COLUMN "EmergencyReason";
                ALTER TABLE "Missions" DROP COLUMN "PriorityDefectsJson";
                ALTER TABLE "Missions" DROP COLUMN "IsOverdueNotified";

                -- assessment -> mission link is one-directional (Missions.PreMissionAssessmentId)
                UPDATE "Missions" m SET "PreMissionAssessmentId" = p."Id"
                FROM "PreMissionAssessments" p
                WHERE p."ConsumedByMissionId" = m."Id" AND m."PreMissionAssessmentId" IS NULL
                  AND NOT EXISTS (SELECT 1 FROM "Missions" o WHERE o."PreMissionAssessmentId" = p."Id" AND NOT o."IsDeleted");
                ALTER TABLE "PreMissionAssessments" DROP COLUMN "ConsumedByMissionId";
                ALTER TABLE "PreMissionAssessmentPersonnel" DROP COLUMN "IsEligible";
                ALTER TABLE "PreMissionAssessmentDrones" DROP COLUMN "IsEligible";

                -- MissionAssignments.AssignedByUserId -> AssignedById (FK to Users)
                ALTER TABLE "MissionAssignments" RENAME COLUMN "AssignedByUserId" TO "AssignedById";
                UPDATE "MissionAssignments" a SET "AssignedById" = m."ManagerId"
                FROM "Missions" m
                WHERE a."MissionId" = m."Id" AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = a."AssignedById");
                CREATE INDEX "IX_MissionAssignments_AssignedById" ON "MissionAssignments" ("AssignedById");
                ALTER TABLE "MissionAssignments" ADD CONSTRAINT "FK_MissionAssignments_Users_AssignedById"
                    FOREIGN KEY ("AssignedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT;

                -- sender name/role are derived from SenderId -> Users
                ALTER TABLE "MissionCommunicationLogs" DROP COLUMN "SenderName";
                ALTER TABLE "MissionCommunicationLogs" DROP COLUMN "SenderRole";

                UPDATE "InspectionSchedules" SET "CreatedBy" = "CreatedByUserId" WHERE "CreatedBy" IS NULL;
                ALTER TABLE "InspectionSchedules" DROP COLUMN "CreatedByUserId";
                """);

            // ---------------------------------------------------------------
            // 7. UAV fleet
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "UAVs" RENAME COLUMN "Status" TO "FlightStatus";
                ALTER TABLE "UAVs" DROP COLUMN "AssignedRegionName";
                ALTER TABLE "UAVs" DROP COLUMN "TechnicalHealthUpdatedAt";
                UPDATE "UAVs" u SET "AssignedRegionId" = NULL
                WHERE "AssignedRegionId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Regions" r WHERE r."Id" = u."AssignedRegionId");
                UPDATE "UAVs" u SET "LastTechnicalInspectionId" = NULL
                WHERE "LastTechnicalInspectionId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "DroneTechnicalInspections" i WHERE i."Id" = u."LastTechnicalInspectionId");
                CREATE INDEX "IX_UAVs_AssignedRegionId" ON "UAVs" ("AssignedRegionId");
                CREATE INDEX "IX_UAVs_LastTechnicalInspectionId" ON "UAVs" ("LastTechnicalInspectionId");
                ALTER TABLE "UAVs" ADD CONSTRAINT "FK_UAVs_Regions_AssignedRegionId"
                    FOREIGN KEY ("AssignedRegionId") REFERENCES "Regions" ("Id") ON DELETE SET NULL;
                ALTER TABLE "UAVs" ADD CONSTRAINT "FK_UAVs_DroneTechnicalInspections_LastTechnicalInspectionId"
                    FOREIGN KEY ("LastTechnicalInspectionId") REFERENCES "DroneTechnicalInspections" ("Id") ON DELETE SET NULL;

                -- active pairing = UnpairedAt IS NULL, at most one per drone and per controller UID
                UPDATE "DroneFlightControllers" SET "UnpairedAt" = COALESCE("UpdatedAt", now())
                WHERE NOT "IsActive" AND "UnpairedAt" IS NULL;
                UPDATE "DroneFlightControllers" f SET "UnpairedAt" = now()
                FROM (SELECT "Id", row_number() OVER (PARTITION BY "DroneId" ORDER BY "PairedAt" DESC) AS rn
                      FROM "DroneFlightControllers" WHERE "UnpairedAt" IS NULL AND NOT "IsDeleted") d
                WHERE f."Id" = d."Id" AND d.rn > 1;
                UPDATE "DroneFlightControllers" f SET "UnpairedAt" = now()
                FROM (SELECT "Id", row_number() OVER (PARTITION BY "FlightControllerUid" ORDER BY "PairedAt" DESC) AS rn
                      FROM "DroneFlightControllers" WHERE "UnpairedAt" IS NULL AND NOT "IsDeleted") d
                WHERE f."Id" = d."Id" AND d.rn > 1;
                DROP INDEX IF EXISTS "IX_DroneFlightControllers_DroneId_IsActive";
                DROP INDEX IF EXISTS "IX_DroneFlightControllers_FlightControllerUid_IsActive";
                ALTER TABLE "DroneFlightControllers" DROP COLUMN "IsActive";
                CREATE UNIQUE INDEX "IX_DroneFlightControllers_DroneId" ON "DroneFlightControllers" ("DroneId")
                    WHERE "UnpairedAt" IS NULL AND NOT "IsDeleted";
                CREATE UNIQUE INDEX "IX_DroneFlightControllers_FlightControllerUid" ON "DroneFlightControllers" ("FlightControllerUid")
                    WHERE "UnpairedAt" IS NULL AND NOT "IsDeleted";

                ALTER TABLE "DroneTechnicalInspections" DROP CONSTRAINT IF EXISTS "FK_DroneTechnicalInspections_Users_TechnicianUserId";
                ALTER TABLE "DroneTechnicalInspections" RENAME COLUMN "TechnicianUserId" TO "TechnicianId";
                ALTER INDEX IF EXISTS "IX_DroneTechnicalInspections_TechnicianUserId" RENAME TO "IX_DroneTechnicalInspections_TechnicianId";
                ALTER TABLE "DroneTechnicalInspections" ADD CONSTRAINT "FK_DroneTechnicalInspections_Users_TechnicianId"
                    FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT;

                UPDATE "DroneTechnicalMetrics" SET "Severity" = 'Critical'
                WHERE "Critical" AND ("Severity" IS NULL OR btrim("Severity") = '');
                ALTER TABLE "DroneTechnicalMetrics" DROP COLUMN "Critical";
                """);

            // ---------------------------------------------------------------
            // 8. Inspection media metadata (video metadata moves from anomalies to media)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "InspectionMedia" ADD COLUMN "Checksum" character varying(64) NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN "FileSizeBytes" bigint NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN "WidthPx" integer NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN "HeightPx" integer NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN "DurationSeconds" double precision NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN "Fps" double precision NULL;
                UPDATE "InspectionMedia" m SET
                    "DurationSeconds" = v.duration, "Fps" = v.fps, "WidthPx" = v.width, "HeightPx" = v.height
                FROM (SELECT "MediaId", max("VideoDuration") AS duration, max("VideoFps") AS fps,
                             max("VideoWidth") AS width, max("VideoHeight") AS height
                      FROM "DetectedAnomalies" GROUP BY "MediaId") v
                WHERE v."MediaId" = m."Id";
                CREATE UNIQUE INDEX "IX_InspectionMedia_Checksum" ON "InspectionMedia" ("Checksum")
                    WHERE "Checksum" IS NOT NULL AND NOT "IsDeleted";

                UPDATE "InspectionMedia" m SET "TowerId" = NULL
                WHERE "TowerId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = m."TowerId");
                ALTER TABLE "InspectionMedia" DROP CONSTRAINT IF EXISTS "FK_InspectionMedia_Towers_TowerId";
                CREATE INDEX IF NOT EXISTS "IX_InspectionMedia_TowerId" ON "InspectionMedia" ("TowerId");
                ALTER TABLE "InspectionMedia" ADD CONSTRAINT "FK_InspectionMedia_Towers_TowerId"
                    FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE SET NULL;
                """);

            // ---------------------------------------------------------------
            // 9. AI analysis runs (edge + cloud) and anomaly lineage
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                CREATE TABLE "AIAnalysisRuns" (
                    "Id" uuid NOT NULL,
                    "MediaId" uuid NOT NULL,
                    "ExecutionLocation" character varying(10) NOT NULL,
                    "UavId" uuid NULL,
                    "RequestedBy" uuid NULL,
                    "AnalysisType" character varying(40) NOT NULL,
                    "ModelName" character varying(100) NOT NULL,
                    "ModelVersion" character varying(50) NULL,
                    "Status" character varying(20) NOT NULL,
                    "SourceEventId" uuid NULL,
                    "BatchId" uuid NULL,
                    "ErrorMessage" text NULL,
                    "StartedAt" timestamp with time zone NULL,
                    "CompletedAt" timestamp with time zone NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NULL,
                    "CreatedBy" uuid NULL,
                    "UpdatedBy" uuid NULL,
                    "IsDeleted" boolean NOT NULL,
                    "DeletedAt" timestamp with time zone NULL,
                    CONSTRAINT "PK_AIAnalysisRuns" PRIMARY KEY ("Id"),
                    CONSTRAINT "CK_AIAnalysisRuns_ExecutionLocation" CHECK ("ExecutionLocation" IN ('Edge', 'Cloud')),
                    CONSTRAINT "FK_AIAnalysisRuns_InspectionMedia_MediaId"
                        FOREIGN KEY ("MediaId") REFERENCES "InspectionMedia" ("Id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_AIAnalysisRuns_UAVs_UavId"
                        FOREIGN KEY ("UavId") REFERENCES "UAVs" ("Id") ON DELETE SET NULL,
                    CONSTRAINT "FK_AIAnalysisRuns_Users_RequestedBy"
                        FOREIGN KEY ("RequestedBy") REFERENCES "Users" ("Id") ON DELETE RESTRICT
                );

                -- cloud requests become cloud runs (requests without media cannot be represented and are dropped)
                INSERT INTO "AIAnalysisRuns"
                    ("Id", "MediaId", "ExecutionLocation", "UavId", "RequestedBy", "AnalysisType", "ModelName", "ModelVersion",
                     "Status", "SourceEventId", "BatchId", "ErrorMessage", "StartedAt", "CompletedAt",
                     "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt")
                SELECT q."Id", q."MediaId", 'Cloud', NULL,
                       CASE WHEN EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = q."UploadedBy") THEN q."UploadedBy" END,
                       CASE q."AnalysisType" WHEN 0 THEN 'DefectDetection' WHEN 1 THEN 'HumanMotionDetection'
                            WHEN 2 THEN 'ObjectClassification' WHEN 3 THEN 'AssetConditionAssessment' ELSE 'General' END,
                       left(COALESCE(NULLIF(btrim(q."ModelName"), ''), 'SERVER'), 100), NULL,
                       CASE q."Status" WHEN 0 THEN 'Pending' WHEN 1 THEN 'Processing' WHEN 2 THEN 'Completed' ELSE 'Failed' END,
                       q."SourceEventId", q."BatchId", q."Notes", NULL, q."CompletedAt",
                       q."CreatedAt", q."UpdatedAt", q."CreatedBy", q."UpdatedBy", q."IsDeleted", q."DeletedAt"
                FROM "AIAnalysisRequests" q
                WHERE q."MediaId" IS NOT NULL AND EXISTS (SELECT 1 FROM "InspectionMedia" m WHERE m."Id" = q."MediaId");

                ALTER TABLE "DetectedAnomalies" ADD COLUMN "AnalysisRunId" uuid NULL;
                ALTER TABLE "DetectedAnomalies" ADD COLUMN "SupersededById" uuid NULL;

                -- link AI anomalies to the latest cloud run of the same media and model
                UPDATE "DetectedAnomalies" d SET "AnalysisRunId" = (
                    SELECT r."Id" FROM "AIAnalysisRuns" r
                    WHERE r."MediaId" = d."MediaId" AND r."ModelName" = d."AiSource"
                    ORDER BY r."CompletedAt" DESC NULLS LAST, r."CreatedAt" DESC
                    LIMIT 1)
                WHERE COALESCE(btrim(d."AiSource"), '') NOT IN ('', 'Manual');

                -- remaining AI anomalies (edge results had no request row): one completed run per media + source
                INSERT INTO "AIAnalysisRuns"
                    ("Id", "MediaId", "ExecutionLocation", "UavId", "RequestedBy", "AnalysisType", "ModelName", "ModelVersion",
                     "Status", "SourceEventId", "BatchId", "ErrorMessage", "StartedAt", "CompletedAt",
                     "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt")
                SELECT md5(d."MediaId"::text || '|' || d."AiSource")::uuid, d."MediaId",
                       CASE WHEN d."AiSource" IN ('VisionEdge', 'EdgeAI', 'Edge') THEN 'Edge' ELSE 'Cloud' END,
                       CASE WHEN d."AiSource" IN ('VisionEdge', 'EdgeAI', 'Edge') THEN max(mi."UavId"::text)::uuid END,
                       NULL, 'DefectDetection', left(d."AiSource", 100), NULL, 'Completed', NULL, NULL, NULL,
                       min(d."CreatedAt"), max(d."CreatedAt"), min(d."CreatedAt"), NULL, NULL, NULL, false, NULL
                FROM "DetectedAnomalies" d
                JOIN "InspectionMedia" m ON m."Id" = d."MediaId"
                LEFT JOIN "Missions" mi ON mi."Id" = m."MissionId"
                WHERE d."AnalysisRunId" IS NULL AND COALESCE(btrim(d."AiSource"), '') NOT IN ('', 'Manual')
                GROUP BY d."MediaId", d."AiSource";
                UPDATE "DetectedAnomalies" d SET "AnalysisRunId" = md5(d."MediaId"::text || '|' || d."AiSource")::uuid
                WHERE d."AnalysisRunId" IS NULL AND COALESCE(btrim(d."AiSource"), '') NOT IN ('', 'Manual')
                  AND EXISTS (SELECT 1 FROM "AIAnalysisRuns" r WHERE r."Id" = md5(d."MediaId"::text || '|' || d."AiSource")::uuid);

                CREATE UNIQUE INDEX "IX_AIAnalysisRuns_ActiveLogicalAnalysis" ON "AIAnalysisRuns" ("MediaId", "AnalysisType", "ModelName")
                    WHERE "IsDeleted" = false AND "Status" IN ('Pending', 'Processing');
                CREATE INDEX "IX_AIAnalysisRuns_RequestedBy" ON "AIAnalysisRuns" ("RequestedBy");
                CREATE UNIQUE INDEX "IX_AIAnalysisRuns_SourceEventId" ON "AIAnalysisRuns" ("SourceEventId") WHERE "SourceEventId" IS NOT NULL;
                CREATE INDEX "IX_AIAnalysisRuns_UavId" ON "AIAnalysisRuns" ("UavId");
                -- target of the composite FK below (run and anomaly must point at the same media)
                ALTER TABLE "AIAnalysisRuns" ADD CONSTRAINT "AK_AIAnalysisRuns_Id_MediaId" UNIQUE ("Id", "MediaId");

                DROP TABLE "AIAnalysisRequests";
                """);

            // ---------------------------------------------------------------
            // 10. DetectedAnomalies contract
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "DetectedAnomalies" ADD COLUMN "Location" geometry(Point,4326) NULL;
                UPDATE "DetectedAnomalies"
                SET "Location" = ST_SetSRID(ST_MakePoint(
                        COALESCE("Gps"->>'lng', "Gps"->>'lon', "Gps"->>'longitude')::double precision,
                        COALESCE("Gps"->>'lat', "Gps"->>'latitude')::double precision), 4326)
                WHERE "Gps" IS NOT NULL AND jsonb_typeof("Gps") = 'object'
                  AND COALESCE("Gps"->>'lng', "Gps"->>'lon', "Gps"->>'longitude') ~ '^-?[0-9]+(\.[0-9]+)?$'
                  AND COALESCE("Gps"->>'lat', "Gps"->>'latitude') ~ '^-?[0-9]+(\.[0-9]+)?$';
                ALTER TABLE "DetectedAnomalies" RENAME COLUMN "Timestamp" TO "FrameTimeSeconds";

                DROP INDEX IF EXISTS "IX_DetectedAnomalies_MediaId_AiDetectionId";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "AiSource";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "AssetId";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "Gps";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "VideoDuration";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "VideoFps";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "VideoWidth";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN "VideoHeight";
                ALTER TABLE "DetectedAnomalies" ALTER COLUMN "ValidationStatus" TYPE character varying(20) USING left("ValidationStatus", 20);

                CREATE INDEX IF NOT EXISTS "IX_DetectedAnomalies_MediaId" ON "DetectedAnomalies" ("MediaId");
                CREATE INDEX "IX_DetectedAnomalies_TowerId" ON "DetectedAnomalies" ("TowerId");
                CREATE INDEX "IX_DetectedAnomalies_SupersededById" ON "DetectedAnomalies" ("SupersededById");
                CREATE UNIQUE INDEX "IX_DetectedAnomalies_AnalysisRunId_AiDetectionId" ON "DetectedAnomalies" ("AnalysisRunId", "AiDetectionId")
                    WHERE "AiDetectionId" IS NOT NULL AND "IsDeleted" = false;
                ALTER TABLE "DetectedAnomalies" ADD CONSTRAINT "FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId"
                    FOREIGN KEY ("AnalysisRunId") REFERENCES "AIAnalysisRuns" ("Id") ON DELETE RESTRICT;
                ALTER TABLE "DetectedAnomalies" ADD CONSTRAINT "FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId_MediaId"
                    FOREIGN KEY ("AnalysisRunId", "MediaId") REFERENCES "AIAnalysisRuns" ("Id", "MediaId");
                ALTER TABLE "DetectedAnomalies" ADD CONSTRAINT "FK_DetectedAnomalies_DetectedAnomalies_SupersededById"
                    FOREIGN KEY ("SupersededById") REFERENCES "DetectedAnomalies" ("Id") ON DELETE SET NULL;
                ALTER TABLE "DetectedAnomalies" ADD CONSTRAINT "FK_DetectedAnomalies_Towers_TowerId"
                    FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE RESTRICT;
                -- NOT VALID: legacy confirmed anomalies without a tower are kept; every new/updated row is checked
                ALTER TABLE "DetectedAnomalies" ADD CONSTRAINT "CK_DetectedAnomalies_ConfirmedRequiresTower"
                    CHECK ("ValidationStatus" <> 'Confirmed' OR "TowerId" IS NOT NULL) NOT VALID;
                """);

            // ---------------------------------------------------------------
            // 11. Alerts, maintenance, incidents, notifications, outbox
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                UPDATE "EmergencyAlerts" SET "ReceivedAt" = "TriggeredAt" + make_interval(secs => "DeliveryLatencySeconds")
                WHERE "ReceivedAt" IS NULL AND "DeliveryLatencySeconds" > 0;
                ALTER TABLE "EmergencyAlerts" DROP COLUMN "AssetId";
                ALTER TABLE "EmergencyAlerts" DROP COLUMN "TowerId";
                ALTER TABLE "EmergencyAlerts" DROP COLUMN "MissionId";
                ALTER TABLE "EmergencyAlerts" DROP COLUMN "DeliveryLatencySeconds";
                ALTER TABLE "EmergencyAlerts" DROP CONSTRAINT IF EXISTS "FK_EmergencyAlerts_Users_ReviewedByUserId";
                ALTER TABLE "EmergencyAlerts" RENAME COLUMN "ReviewedByUserId" TO "ReviewedById";
                DROP INDEX IF EXISTS "IX_EmergencyAlerts_ReviewedByUserId";
                CREATE INDEX "IX_EmergencyAlerts_ReviewedById" ON "EmergencyAlerts" ("ReviewedById");
                ALTER TABLE "EmergencyAlerts" ADD CONSTRAINT "FK_EmergencyAlerts_Users_ReviewedById"
                    FOREIGN KEY ("ReviewedById") REFERENCES "Users" ("Id") ON DELETE SET NULL;

                -- tower of a ticket is anomaly.TowerId (already backfilled in step 3d)
                ALTER TABLE "MaintenanceTickets" DROP COLUMN "AssetId";
                ALTER TABLE "MaintenanceTickets" DROP COLUMN "TowerId";

                -- one row per proof file
                INSERT INTO "MaintenanceProofs"
                    ("Id", "TicketId", "UploadedBy", "ProofType", "FileUrl", "AfterRepairImageUrl", "TechnicianNotes", "UploadedAt",
                     "CreatedAt", "CreatedBy", "IsDeleted")
                SELECT gen_random_uuid(), p."TicketId", p."UploadedBy", 'After', p."AfterRepairImageUrl", '', p."TechnicianNotes", p."UploadedAt",
                       now(), p."CreatedBy", p."IsDeleted"
                FROM "MaintenanceProofs" p
                WHERE COALESCE(p."AfterRepairImageUrl", '') <> '' AND COALESCE(p."FileUrl", '') <> ''
                  AND p."AfterRepairImageUrl" <> p."FileUrl";
                UPDATE "MaintenanceProofs" SET "FileUrl" = "AfterRepairImageUrl", "ProofType" = 'After'
                WHERE COALESCE("FileUrl", '') = '' AND COALESCE("AfterRepairImageUrl", '') <> '';
                ALTER TABLE "MaintenanceProofs" DROP COLUMN "AfterRepairImageUrl";

                ALTER TABLE "IncidentReports" DROP COLUMN "AssetId";
                ALTER TABLE "IncidentReports" DROP COLUMN "FileUrl";

                UPDATE "Notifications" SET "ReadAt" = "SentAt" WHERE "IsRead" AND "ReadAt" IS NULL;
                UPDATE "Notifications" SET "PushedAt" = "SentAt" WHERE "IsPushed" AND "PushedAt" IS NULL;
                ALTER TABLE "Notifications" DROP COLUMN "IsRead";
                ALTER TABLE "Notifications" DROP COLUMN "IsPushed";

                DROP INDEX IF EXISTS "IX_OutboxMessages_MessageType_OccurredAt";
                ALTER TABLE "OutboxMessages" DROP COLUMN "CreatedAt";
                ALTER TABLE "OutboxMessages" DROP COLUMN "CreatedBy";
                ALTER TABLE "OutboxMessages" DROP COLUMN "UpdatedAt";
                ALTER TABLE "OutboxMessages" DROP COLUMN "UpdatedBy";
                ALTER TABLE "OutboxMessages" DROP COLUMN "IsDeleted";
                ALTER TABLE "OutboxMessages" DROP COLUMN "DeletedAt";
                CREATE INDEX "IX_OutboxMessages_MessageType_OccurredAt" ON "OutboxMessages" ("MessageType", "OccurredAt")
                    WHERE "PublishedAt" IS NULL;
                """);

            // ---------------------------------------------------------------
            // 12. Contract: grid tables below the tower level and the health-scoring feature
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS "IX_Towers_LineAssetId";
                DROP INDEX IF EXISTS "IX_Towers_LineCode";
                DROP INDEX IF EXISTS "IX_Towers_RiskLevel";
                ALTER TABLE "Towers" DROP COLUMN "LineAssetId";
                ALTER TABLE "Towers" DROP COLUMN "LineCode";
                ALTER TABLE "Towers" DROP COLUMN "Voltage";
                ALTER TABLE "Towers" DROP COLUMN "RiskLevel";
                ALTER TABLE "Towers" DROP COLUMN "ActiveDefectsCount";
                ALTER TABLE "Towers" DROP COLUMN "LastInspectedAt";

                ALTER TABLE "InspectionMedia" DROP COLUMN "AssetId";
                ALTER TABLE "InspectionMedia" DROP COLUMN "AiSource";

                DROP TABLE IF EXISTS "AssetHealthHistories" CASCADE;
                DROP TABLE IF EXISTS "TowerConditionHistories" CASCADE;
                DROP TABLE IF EXISTS "LineSegments" CASCADE;
                DROP TABLE IF EXISTS "MissionTargetLines" CASCADE;
                DROP TABLE IF EXISTS "PreMissionAssessmentAssets" CASCADE;
                DROP TABLE IF EXISTS "AssetComponents" CASCADE;
                DROP TABLE IF EXISTS "TransmissionLines" CASCADE;
                DROP TABLE IF EXISTS "Substations" CASCADE;
                DROP TABLE IF EXISTS "ManagementUnits" CASCADE;
                """);

            // ---------------------------------------------------------------
            // 13. Legacy drift outside the ERD (columns no service maps, duplicate FK, raw-SQL constraint names)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "AssignedToUserId";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "DroneCode";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "RouteData";
                ALTER TABLE "Users" DROP COLUMN IF EXISTS "Username";
                ALTER TABLE "InspectionMedia" DROP CONSTRAINT IF EXISTS "InspectionMedia_TowerId_fkey";

                DO $$
                DECLARE r record;
                BEGIN
                    FOR r IN SELECT * FROM (VALUES
                        ('DroneHandovers', 'DroneHandovers_pkey', 'PK_DroneHandovers'),
                        ('DroneHandovers', 'DroneHandovers_MissionId_fkey', 'FK_DroneHandovers_Missions_MissionId'),
                        ('DroneHandovers', 'DroneHandovers_DroneId_fkey', 'FK_DroneHandovers_UAVs_DroneId'),
                        ('InspectionSchedules', 'InspectionSchedules_pkey', 'PK_InspectionSchedules'),
                        ('InspectionSchedules', 'InspectionSchedules_RegionId_fkey', 'FK_InspectionSchedules_Regions_RegionId'),
                        ('MissionAssignments', 'MissionAssignments_pkey', 'PK_MissionAssignments'),
                        ('MissionAssignments', 'MissionAssignments_MissionId_fkey', 'FK_MissionAssignments_Missions_MissionId'),
                        ('MissionAssignments', 'MissionAssignments_UserId_fkey', 'FK_MissionAssignments_Users_UserId'),
                        ('MissionCheckIns', 'MissionCheckIns_pkey', 'PK_MissionCheckIns'),
                        ('MissionCheckIns', 'MissionCheckIns_MissionId_fkey', 'FK_MissionCheckIns_Missions_MissionId'),
                        ('MissionCheckIns', 'MissionCheckIns_UserId_fkey', 'FK_MissionCheckIns_Users_UserId'),
                        ('MissionTargets', 'MissionTargets_pkey', 'PK_MissionTargets')
                    ) AS v(tbl, old_name, new_name)
                    LOOP
                        IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = r.old_name)
                           AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = r.new_name) THEN
                            EXECUTE format('ALTER TABLE %I RENAME CONSTRAINT %I TO %I', r.tbl, r.old_name, r.new_name);
                        END IF;
                    END LOOP;
                END $$;
                CREATE INDEX IF NOT EXISTS "IX_DroneHandovers_DroneId" ON "DroneHandovers" ("DroneId");
                CREATE INDEX IF NOT EXISTS "IX_MissionAssignments_UserId" ON "MissionAssignments" ("UserId");
                CREATE INDEX IF NOT EXISTS "IX_MissionCheckIns_UserId" ON "MissionCheckIns" ("UserId");
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_Users_Email')
                       AND NOT EXISTS (SELECT 1 FROM "Users" GROUP BY "Email" HAVING count(*) > 1) THEN
                        CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "AlignSchemaWithErdV2_2 drops tables and columns (assets, lines, substations, health history, report files). " +
                "Roll back by restoring the pg_dump taken before the migration (see .github/workflows/migrate-database-erd-v2-2.yml).");
        }
    }
}
