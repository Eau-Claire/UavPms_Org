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
    ///
    /// Production has been patched by hand in the past (repair workflows), so its schema can differ from the
    /// migration history. Every statement is therefore defensive: backfills run only when the legacy
    /// tables/columns they read exist, and every add/drop/rename/index/constraint is idempotent.
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
            // 0. Session helpers (pg_temp, dropped with the session) and objects created outside EF
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION pg_temp.has_table(t text) RETURNS boolean LANGUAGE sql STABLE AS $f$
                    SELECT EXISTS (SELECT 1 FROM information_schema.tables
                                   WHERE table_schema = 'public' AND table_name = t AND table_type = 'BASE TABLE') $f$;
                CREATE OR REPLACE FUNCTION pg_temp.has_col(t text, c text) RETURNS boolean LANGUAGE sql STABLE AS $f$
                    SELECT EXISTS (SELECT 1 FROM information_schema.columns
                                   WHERE table_schema = 'public' AND table_name = t AND column_name = c) $f$;
                CREATE OR REPLACE FUNCTION pg_temp.col_type(t text, c text) RETURNS text LANGUAGE sql STABLE AS $f$
                    SELECT udt_name::text FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = t AND column_name = c $f$;
                CREATE OR REPLACE PROCEDURE pg_temp.exec_if(cond boolean, stmt text) LANGUAGE plpgsql AS $p$
                    BEGIN IF cond THEN EXECUTE stmt; END IF; END $p$;
                CREATE OR REPLACE PROCEDURE pg_temp.rename_col(t text, old_name text, new_name text) LANGUAGE plpgsql AS $p$
                    BEGIN
                        IF pg_temp.has_col(t, old_name) AND NOT pg_temp.has_col(t, new_name) THEN
                            EXECUTE format('ALTER TABLE %I RENAME COLUMN %I TO %I', t, old_name, new_name);
                        ELSIF pg_temp.has_col(t, old_name) THEN
                            -- both exist (hand-patched schema): the old column is the one the services wrote to
                            EXECUTE format('UPDATE %I SET %I = %I WHERE %I IS NOT NULL', t, new_name, old_name, old_name);
                            EXECUTE format('ALTER TABLE %I DROP COLUMN %I', t, old_name);
                        END IF;
                    END $p$;

                CALL pg_temp.exec_if(to_regclass('public.towers') IS NOT NULL,
                    $q$DROP TRIGGER IF EXISTS trg_sync_towers_assets ON public.towers$q$);
                DROP FUNCTION IF EXISTS trg_sync_tower_to_asset();
                DROP VIEW IF EXISTS v_assets_compat;
                DROP VIEW IF EXISTS v_substations_compat;
                DROP VIEW IF EXISTS v_transmission_lines_compat;
                """);

            // ---------------------------------------------------------------
            // 1. Regions: RegionName -> Name
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                CALL pg_temp.rename_col('Regions', 'RegionName', 'Name');
                ALTER TABLE "Regions" ADD COLUMN IF NOT EXISTS "Name" text NOT NULL DEFAULT '';
                ALTER TABLE "Regions" ALTER COLUMN "Name" DROP DEFAULT;
                ALTER TABLE "Regions" ALTER COLUMN "Name" TYPE character varying(150) USING left("Name", 150);
                """);

            // ---------------------------------------------------------------
            // 2. Towers: every tower belongs directly to a region (RegionId NOT NULL)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Towers" ADD COLUMN IF NOT EXISTS "RegionId" uuid NULL;

                -- 2a. region of the old line's substation
                CALL pg_temp.exec_if(pg_temp.has_col('Towers', 'LineAssetId') AND pg_temp.has_col('TransmissionLines', 'SubstationAssetId')
                                     AND pg_temp.has_col('Substations', 'RegionAssetId'), $q$
                    UPDATE "Towers" t SET "RegionId" = s."RegionAssetId"
                    FROM "TransmissionLines" l JOIN "Substations" s ON s."Id" = l."SubstationAssetId"
                    WHERE t."RegionId" IS NULL AND t."LineAssetId" = l."Id"
                      AND EXISTS (SELECT 1 FROM "Regions" r WHERE r."Id" = s."RegionAssetId")$q$);

                -- 2b. smallest region polygon that contains the tower
                CALL pg_temp.exec_if(pg_temp.has_col('Towers', 'Geom') AND pg_temp.has_col('Regions', 'Geom'), $q$
                    UPDATE "Towers" t
                    SET "RegionId" = (
                        SELECT r."Id" FROM "Regions" r
                        WHERE NOT r."IsDeleted" AND r."Geom" IS NOT NULL AND t."Geom" IS NOT NULL
                          AND ST_Contains(r."Geom", ST_SetSRID(t."Geom", ST_SRID(r."Geom")))
                        ORDER BY ST_Area(r."Geom") ASC
                        LIMIT 1)
                    WHERE t."RegionId" IS NULL$q$);

                -- 2c. anything left (or pointing to a missing region) goes to an explicit "unassigned" region
                UPDATE "Towers" t SET "RegionId" = NULL
                WHERE t."RegionId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Regions" r WHERE r."Id" = t."RegionId");
                INSERT INTO "Regions" ("Id", "Code", "Name", "Type", "CreatedAt", "IsDeleted")
                SELECT gen_random_uuid(), 'UNASSIGNED', 'Chưa phân khu vực', 'Area', now(), false
                WHERE EXISTS (SELECT 1 FROM "Towers" WHERE "RegionId" IS NULL)
                  AND NOT EXISTS (SELECT 1 FROM "Regions" WHERE "Code" = 'UNASSIGNED');
                UPDATE "Towers"
                SET "RegionId" = (SELECT "Id" FROM "Regions" WHERE "Code" = 'UNASSIGNED' LIMIT 1)
                WHERE "RegionId" IS NULL;

                ALTER TABLE "Towers" DROP CONSTRAINT IF EXISTS "FK_Towers_Regions_RegionId";
                ALTER TABLE "Towers" ALTER COLUMN "RegionId" SET NOT NULL;
                CREATE INDEX IF NOT EXISTS "IX_Towers_RegionId" ON "Towers" ("RegionId");
                ALTER TABLE "Towers" ADD CONSTRAINT "FK_Towers_Regions_RegionId"
                    FOREIGN KEY ("RegionId") REFERENCES "Regions" ("Id") ON DELETE RESTRICT;
                """);

            // ---------------------------------------------------------------
            // 3. asset_id -> tower_id everywhere (before AssetComponents is dropped)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                -- 3a. MissionTargets.AssetId -> TowerId
                ALTER TABLE "MissionTargets" DROP CONSTRAINT IF EXISTS "FK_MissionTargets_AssetComponents_AssetId";
                ALTER TABLE "MissionTargets" DROP CONSTRAINT IF EXISTS "FK_MissionTargets_Towers_TowerId";
                DROP INDEX IF EXISTS "IX_MissionTargets_MissionId_AssetId";
                DROP INDEX IF EXISTS "IX_MissionTargets_AssetId";
                DROP INDEX IF EXISTS "IX_MissionTargets_MissionId_TowerId";
                DROP INDEX IF EXISTS "IX_MissionTargets_TowerId";
                CALL pg_temp.exec_if(pg_temp.has_col('MissionTargets', 'AssetId') AND pg_temp.has_col('AssetComponents', 'TowerId'), $q$
                    UPDATE "MissionTargets" mt SET "AssetId" = a."TowerId"
                    FROM "AssetComponents" a WHERE mt."AssetId" = a."Id"$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('MissionTargets', 'AssetId') AND pg_temp.has_col('MissionTargets', 'TowerId'), $q$
                    UPDATE "MissionTargets" SET "TowerId" = COALESCE("TowerId", "AssetId")$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('MissionTargets', 'AssetId') AND pg_temp.has_col('MissionTargets', 'TowerId'), $q$
                    ALTER TABLE "MissionTargets" DROP COLUMN "AssetId"$q$);
                CALL pg_temp.rename_col('MissionTargets', 'AssetId', 'TowerId');
                ALTER TABLE "MissionTargets" ADD COLUMN IF NOT EXISTS "TowerId" uuid NULL;
                DELETE FROM "MissionTargets" mt
                WHERE mt."TowerId" IS NULL OR NOT EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = mt."TowerId");
                DELETE FROM "MissionTargets" mt
                USING (
                    SELECT "Id", row_number() OVER (PARTITION BY "MissionId", "TowerId" ORDER BY "Sequence", "CreatedAt") AS rn
                    FROM "MissionTargets") d
                WHERE mt."Id" = d."Id" AND d.rn > 1;
                ALTER TABLE "MissionTargets" ALTER COLUMN "TowerId" SET NOT NULL;
                CREATE UNIQUE INDEX "IX_MissionTargets_MissionId_TowerId" ON "MissionTargets" ("MissionId", "TowerId");
                CREATE INDEX "IX_MissionTargets_TowerId" ON "MissionTargets" ("TowerId");
                ALTER TABLE "MissionTargets" ADD CONSTRAINT "FK_MissionTargets_Towers_TowerId"
                    FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE RESTRICT;

                -- 3b. PreMissionAssessmentAssets -> PreMissionAssessmentTowers
                CREATE TABLE IF NOT EXISTS "PreMissionAssessmentTowers" (
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
                CALL pg_temp.exec_if(pg_temp.has_col('PreMissionAssessmentAssets', 'AssetId'), $q$
                    INSERT INTO "PreMissionAssessmentTowers"
                        ("Id", "AssessmentId", "TowerId", "Sequence", "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt")
                    SELECT DISTINCT ON (x."AssessmentId", x.tower_id)
                        x."Id", x."AssessmentId", x.tower_id, x."Sequence", x."CreatedAt", x."UpdatedAt", x."CreatedBy", x."UpdatedBy", x."IsDeleted", x."DeletedAt"
                    FROM (
                        SELECT pa.*, COALESCE(
                                   (SELECT a."TowerId" FROM "AssetComponents" a WHERE a."Id" = pa."AssetId"),
                                   (SELECT t."Id" FROM "Towers" t WHERE t."Id" = pa."AssetId")) AS tower_id
                        FROM "PreMissionAssessmentAssets" pa) x
                    WHERE x.tower_id IS NOT NULL
                      AND EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = x.tower_id)
                      AND EXISTS (SELECT 1 FROM "PreMissionAssessments" p WHERE p."Id" = x."AssessmentId")
                    ORDER BY x."AssessmentId", x.tower_id, x."Sequence"
                    ON CONFLICT DO NOTHING$q$);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_PreMissionAssessmentTowers_AssessmentId_TowerId" ON "PreMissionAssessmentTowers" ("AssessmentId", "TowerId");
                CREATE INDEX IF NOT EXISTS "IX_PreMissionAssessmentTowers_TowerId" ON "PreMissionAssessmentTowers" ("TowerId");

                -- 3c. InspectionMedia / IncidentReports: keep TowerId, fill it from the old asset
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "TowerId" uuid NULL;
                ALTER TABLE "IncidentReports" ADD COLUMN IF NOT EXISTS "TowerId" uuid NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('InspectionMedia', 'AssetId') AND pg_temp.has_col('AssetComponents', 'TowerId'), $q$
                    UPDATE "InspectionMedia" m SET "TowerId" = a."TowerId"
                    FROM "AssetComponents" a WHERE m."TowerId" IS NULL AND m."AssetId" = a."Id"$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('IncidentReports', 'AssetId') AND pg_temp.has_col('AssetComponents', 'TowerId'), $q$
                    UPDATE "IncidentReports" i SET "TowerId" = a."TowerId"
                    FROM "AssetComponents" a WHERE i."TowerId" IS NULL AND i."AssetId" = a."Id"$q$);

                -- 3d. DetectedAnomalies.TowerId: legacy varchar(128) (code or id) -> uuid FK
                ALTER TABLE "DetectedAnomalies" ADD COLUMN IF NOT EXISTS "TowerIdNew" uuid NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('DetectedAnomalies', 'AssetId') AND pg_temp.has_col('AssetComponents', 'TowerId'), $q$
                    UPDATE "DetectedAnomalies" d SET "TowerIdNew" = a."TowerId"
                    FROM "AssetComponents" a WHERE d."AssetId" = a."Id"$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('DetectedAnomalies', 'TowerId'), $q$
                    UPDATE "DetectedAnomalies" d SET "TowerIdNew" = t."Id"
                    FROM "Towers" t
                    WHERE d."TowerIdNew" IS NULL AND d."TowerId" IS NOT NULL
                      AND (t."TowerCode" = d."TowerId"::text OR t."Id"::text = lower(d."TowerId"::text))$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('MaintenanceTickets', 'TowerId'), $q$
                    UPDATE "DetectedAnomalies" d SET "TowerIdNew" = mt."TowerId"
                    FROM "MaintenanceTickets" mt
                    WHERE d."TowerIdNew" IS NULL AND mt."AnomalyId" = d."Id" AND mt."TowerId" IS NOT NULL
                      AND EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = mt."TowerId")$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('EmergencyAlerts', 'TowerId'), $q$
                    UPDATE "DetectedAnomalies" d SET "TowerIdNew" = ea."TowerId"
                    FROM "EmergencyAlerts" ea
                    WHERE d."TowerIdNew" IS NULL AND ea."AnomalyId" = d."Id" AND ea."TowerId" IS NOT NULL
                      AND EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = ea."TowerId")$q$);
                UPDATE "DetectedAnomalies" d SET "TowerIdNew" = m."TowerId"
                FROM "InspectionMedia" m
                WHERE d."TowerIdNew" IS NULL AND d."MediaId" = m."Id" AND m."TowerId" IS NOT NULL
                  AND EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = m."TowerId");
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "TowerId";
                ALTER TABLE "DetectedAnomalies" RENAME COLUMN "TowerIdNew" TO "TowerId";
                """);

            // ---------------------------------------------------------------
            // 4. UserGeographicScopes: region only
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "UserGeographicScopes" DROP CONSTRAINT IF EXISTS "CK_UserGeographicScopes_HasScope";
                ALTER TABLE "UserGeographicScopes" DROP CONSTRAINT IF EXISTS "FK_UserGeographicScopes_Regions_RegionId";
                ALTER TABLE "UserGeographicScopes" ADD COLUMN IF NOT EXISTS "RegionId" uuid NULL;
                ALTER TABLE "UserGeographicScopes" ALTER COLUMN "RegionId" DROP NOT NULL;

                CALL pg_temp.exec_if(pg_temp.has_col('UserGeographicScopes', 'SubstationId') AND pg_temp.has_col('Substations', 'RegionAssetId'), $q$
                    UPDATE "UserGeographicScopes" g SET "RegionId" = s."RegionAssetId"
                    FROM "Substations" s WHERE g."RegionId" IS NULL AND g."SubstationId" = s."Id"$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('UserGeographicScopes', 'TransmissionLineId') AND pg_temp.has_col('TransmissionLines', 'SubstationAssetId')
                                     AND pg_temp.has_col('Substations', 'RegionAssetId'), $q$
                    UPDATE "UserGeographicScopes" g SET "RegionId" = s."RegionAssetId"
                    FROM "TransmissionLines" l JOIN "Substations" s ON s."Id" = l."SubstationAssetId"
                    WHERE g."RegionId" IS NULL AND g."TransmissionLineId" = l."Id"$q$);
                -- a management-unit scope becomes one region scope per region its lines' towers are in
                CALL pg_temp.exec_if(pg_temp.has_col('UserGeographicScopes', 'ManagementUnitId') AND pg_temp.has_col('TransmissionLines', 'ManagementUnitId')
                                     AND pg_temp.has_col('Towers', 'LineAssetId'), $q$
                    INSERT INTO "UserGeographicScopes" ("Id", "UserId", "RegionId", "CreatedAt", "CreatedBy", "IsDeleted")
                    SELECT gen_random_uuid(), x."UserId", x."RegionId", now(), x."CreatedBy", false
                    FROM (
                        SELECT DISTINCT g."UserId", t."RegionId", g."CreatedBy"
                        FROM "UserGeographicScopes" g
                        JOIN "TransmissionLines" l ON l."ManagementUnitId" = g."ManagementUnitId"
                        JOIN "Towers" t ON t."LineAssetId" = l."Id"
                        WHERE g."RegionId" IS NULL AND g."ManagementUnitId" IS NOT NULL AND NOT g."IsDeleted") x$q$);
                DELETE FROM "UserGeographicScopes" g
                WHERE g."RegionId" IS NULL OR NOT EXISTS (SELECT 1 FROM "Regions" r WHERE r."Id" = g."RegionId");
                DELETE FROM "UserGeographicScopes" g
                USING (
                    SELECT "Id", row_number() OVER (PARTITION BY "UserId", "RegionId" ORDER BY "IsDeleted", "CreatedAt") AS rn
                    FROM "UserGeographicScopes") d
                WHERE g."Id" = d."Id" AND d.rn > 1;

                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_RegionId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_SubstationId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_TransmissionLineId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_UserId_ManagementUnitId";
                DROP INDEX IF EXISTS "IX_UserGeographicScopes_RegionId";
                ALTER TABLE "UserGeographicScopes" DROP COLUMN IF EXISTS "SubstationId";
                ALTER TABLE "UserGeographicScopes" DROP COLUMN IF EXISTS "TransmissionLineId";
                ALTER TABLE "UserGeographicScopes" DROP COLUMN IF EXISTS "ManagementUnitId";
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
                ALTER TABLE "Reports" DROP CONSTRAINT IF EXISTS "FK_Reports_Regions_RegionId";
                ALTER TABLE "Reports" ADD COLUMN IF NOT EXISTS "RegionId" uuid NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('Reports', 'SubstationId') AND pg_temp.has_col('Substations', 'RegionAssetId'), $q$
                    UPDATE "Reports" r SET "RegionId" = s."RegionAssetId"
                    FROM "Substations" s WHERE r."RegionId" IS NULL AND r."SubstationId" = s."Id"$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('Reports', 'TransmissionLineId') AND pg_temp.has_col('TransmissionLines', 'SubstationAssetId')
                                     AND pg_temp.has_col('Substations', 'RegionAssetId'), $q$
                    UPDATE "Reports" r SET "RegionId" = s."RegionAssetId"
                    FROM "TransmissionLines" l JOIN "Substations" s ON s."Id" = l."SubstationAssetId"
                    WHERE r."RegionId" IS NULL AND r."TransmissionLineId" = l."Id"$q$);
                UPDATE "Reports" r SET "RegionId" = NULL
                WHERE r."RegionId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Regions" g WHERE g."Id" = r."RegionId");
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "SubstationId";
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "TransmissionLineId";
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "PdfFileUrl";
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "PdfFileSize";
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "ExcelFileUrl";
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "ExcelFileSize";
                ALTER TABLE "Reports" DROP COLUMN IF EXISTS "DefectCount";
                CREATE INDEX IF NOT EXISTS "IX_Reports_RegionId" ON "Reports" ("RegionId");
                ALTER TABLE "Reports" ADD CONSTRAINT "FK_Reports_Regions_RegionId"
                    FOREIGN KEY ("RegionId") REFERENCES "Regions" ("Id") ON DELETE SET NULL;

                CALL pg_temp.rename_col('ReportMissions', 'AssignedAt', 'AddedAt');
                ALTER TABLE "ReportMissions" ADD COLUMN IF NOT EXISTS "AddedAt" timestamp with time zone NOT NULL DEFAULT now();
                ALTER TABLE "ReportMissions" ALTER COLUMN "AddedAt" DROP DEFAULT;
                ALTER TABLE "ReportMissions" DROP COLUMN IF EXISTS "AssignedAt";
                """);

            // ---------------------------------------------------------------
            // 6. Missions: inspector -> assignments, priority defects -> junction, flags -> timestamps
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Missions" ADD COLUMN IF NOT EXISTS "OverdueNotifiedAt" timestamp with time zone NULL;
                ALTER TABLE "Missions" ADD COLUMN IF NOT EXISTS "PlannedStart" timestamp with time zone NULL;
                ALTER TABLE "Missions" ADD COLUMN IF NOT EXISTS "TriggerReason" text NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('Missions', 'IsOverdueNotified'), $q$
                    UPDATE "Missions" SET "OverdueNotifiedAt" = COALESCE("UpdatedAt", "CreatedAt")
                    WHERE "IsOverdueNotified" AND "OverdueNotifiedAt" IS NULL$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('Missions', 'ScheduledStartAt'), $q$
                    UPDATE "Missions" SET "PlannedStart" = "ScheduledStartAt"
                    WHERE "PlannedStart" IS NULL AND "ScheduledStartAt" IS NOT NULL$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('Missions', 'EmergencyReason'), $q$
                    UPDATE "Missions" SET "TriggerReason" = "EmergencyReason"
                    WHERE ("TriggerReason" IS NULL OR btrim("TriggerReason") = '') AND "EmergencyReason" IS NOT NULL$q$);

                -- MissionAssignments.AssignedByUserId -> AssignedById (FK to Users)
                ALTER TABLE "MissionAssignments" DROP CONSTRAINT IF EXISTS "FK_MissionAssignments_Users_AssignedById";
                CALL pg_temp.rename_col('MissionAssignments', 'AssignedByUserId', 'AssignedById');
                ALTER TABLE "MissionAssignments" ADD COLUMN IF NOT EXISTS "AssignedById" uuid NULL;

                -- inspector that is not yet an assignment becomes an INSPECTOR assignment
                CALL pg_temp.exec_if(pg_temp.has_col('Missions', 'InspectorId'), $q$
                    INSERT INTO "MissionAssignments"
                        ("Id", "MissionId", "UserId", "AssignmentRole", "Status", "ResponseStatus", "IsRequired",
                         "AssignedAt", "AssignedById", "Version", "CreatedAt", "CreatedBy", "IsDeleted")
                    SELECT gen_random_uuid(), m."Id", m."InspectorId", 'INSPECTOR', 'Active',
                           CASE WHEN m."AcceptedAt" IS NOT NULL THEN 'Accepted' ELSE 'Pending' END,
                           true, m."CreatedAt", m."ManagerId", 1, now(), m."ManagerId", false
                    FROM "Missions" m
                    WHERE m."InspectorId" IS NOT NULL AND NOT m."IsDeleted"
                      AND EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = m."InspectorId")
                      AND NOT EXISTS (SELECT 1 FROM "MissionAssignments" a
                                      WHERE a."MissionId" = m."Id" AND a."UserId" = m."InspectorId" AND NOT a."IsDeleted")$q$);

                UPDATE "MissionAssignments" a SET "AssignedById" = m."ManagerId"
                FROM "Missions" m
                WHERE a."MissionId" = m."Id"
                  AND (a."AssignedById" IS NULL OR NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = a."AssignedById"));
                DELETE FROM "MissionAssignments" a
                WHERE a."AssignedById" IS NULL OR NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = a."AssignedById");
                ALTER TABLE "MissionAssignments" ALTER COLUMN "AssignedById" SET NOT NULL;
                CREATE INDEX IF NOT EXISTS "IX_MissionAssignments_AssignedById" ON "MissionAssignments" ("AssignedById");
                ALTER TABLE "MissionAssignments" ADD CONSTRAINT "FK_MissionAssignments_Users_AssignedById"
                    FOREIGN KEY ("AssignedById") REFERENCES "Users" ("Id") ON DELETE RESTRICT;

                CREATE TABLE IF NOT EXISTS "MissionPriorityDefects" (
                    "MissionId" uuid NOT NULL,
                    "CategoryId" integer NOT NULL,
                    CONSTRAINT "PK_MissionPriorityDefects" PRIMARY KEY ("MissionId", "CategoryId"),
                    CONSTRAINT "FK_MissionPriorityDefects_DefectCategories_CategoryId"
                        FOREIGN KEY ("CategoryId") REFERENCES "DefectCategories" ("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_MissionPriorityDefects_Missions_MissionId"
                        FOREIGN KEY ("MissionId") REFERENCES "Missions" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_MissionPriorityDefects_CategoryId" ON "MissionPriorityDefects" ("CategoryId");

                CALL pg_temp.exec_if(pg_temp.has_col('Missions', 'PriorityDefectsJson'), $q$
                    DO $d$
                    DECLARE r record;
                    BEGIN
                        FOR r IN SELECT "Id", "PriorityDefectsJson"::text AS js FROM "Missions"
                                 WHERE "PriorityDefectsJson" IS NOT NULL AND btrim("PriorityDefectsJson"::text) NOT IN ('', '[]')
                        LOOP
                            BEGIN
                                INSERT INTO "MissionPriorityDefects" ("MissionId", "CategoryId")
                                SELECT DISTINCT r."Id", c."Id"
                                FROM jsonb_array_elements_text(r.js::jsonb) AS e(v)
                                JOIN "DefectCategories" c
                                  ON lower(c."CategoryCode") = lower(btrim(e.v)) OR lower(c."CategoryName") = lower(btrim(e.v))
                                ON CONFLICT DO NOTHING;
                            EXCEPTION WHEN others THEN
                                RAISE NOTICE 'Skipping malformed PriorityDefectsJson for mission %', r."Id";
                            END;
                        END LOOP;
                    END $d$$q$);

                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "InspectorId";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "ScheduledStartAt";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "EmergencyReason";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "PriorityDefectsJson";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "IsOverdueNotified";

                -- assessment -> mission link is one-directional (Missions.PreMissionAssessmentId)
                ALTER TABLE "Missions" ADD COLUMN IF NOT EXISTS "PreMissionAssessmentId" uuid NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('PreMissionAssessments', 'ConsumedByMissionId'), $q$
                    UPDATE "Missions" m SET "PreMissionAssessmentId" = p."Id"
                    FROM "PreMissionAssessments" p
                    WHERE p."ConsumedByMissionId" = m."Id" AND m."PreMissionAssessmentId" IS NULL
                      AND NOT EXISTS (SELECT 1 FROM "Missions" o WHERE o."PreMissionAssessmentId" = p."Id" AND NOT o."IsDeleted")$q$);
                ALTER TABLE "PreMissionAssessments" DROP COLUMN IF EXISTS "ConsumedByMissionId";
                ALTER TABLE "PreMissionAssessmentPersonnel" DROP COLUMN IF EXISTS "IsEligible";
                ALTER TABLE "PreMissionAssessmentDrones" DROP COLUMN IF EXISTS "IsEligible";

                -- sender name/role are derived from SenderId -> Users
                ALTER TABLE "MissionCommunicationLogs" DROP COLUMN IF EXISTS "SenderName";
                ALTER TABLE "MissionCommunicationLogs" DROP COLUMN IF EXISTS "SenderRole";

                CALL pg_temp.exec_if(pg_temp.has_col('InspectionSchedules', 'CreatedByUserId'), $q$
                    UPDATE "InspectionSchedules" SET "CreatedBy" = "CreatedByUserId" WHERE "CreatedBy" IS NULL$q$);
                ALTER TABLE "InspectionSchedules" DROP COLUMN IF EXISTS "CreatedByUserId";
                """);

            // ---------------------------------------------------------------
            // 7. UAV fleet
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                CALL pg_temp.rename_col('UAVs', 'Status', 'FlightStatus');
                ALTER TABLE "UAVs" ADD COLUMN IF NOT EXISTS "FlightStatus" text NOT NULL DEFAULT 'Idle';
                ALTER TABLE "UAVs" ALTER COLUMN "FlightStatus" DROP DEFAULT;
                ALTER TABLE "UAVs" DROP COLUMN IF EXISTS "AssignedRegionName";
                ALTER TABLE "UAVs" DROP COLUMN IF EXISTS "TechnicalHealthUpdatedAt";
                ALTER TABLE "UAVs" ADD COLUMN IF NOT EXISTS "AssignedRegionId" uuid NULL;
                ALTER TABLE "UAVs" ADD COLUMN IF NOT EXISTS "LastTechnicalInspectionId" uuid NULL;
                ALTER TABLE "UAVs" DROP CONSTRAINT IF EXISTS "FK_UAVs_Regions_AssignedRegionId";
                ALTER TABLE "UAVs" DROP CONSTRAINT IF EXISTS "FK_UAVs_DroneTechnicalInspections_LastTechnicalInspectionId";
                UPDATE "UAVs" u SET "AssignedRegionId" = NULL
                WHERE "AssignedRegionId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Regions" r WHERE r."Id" = u."AssignedRegionId");
                UPDATE "UAVs" u SET "LastTechnicalInspectionId" = NULL
                WHERE "LastTechnicalInspectionId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "DroneTechnicalInspections" i WHERE i."Id" = u."LastTechnicalInspectionId");
                CREATE INDEX IF NOT EXISTS "IX_UAVs_AssignedRegionId" ON "UAVs" ("AssignedRegionId");
                CREATE INDEX IF NOT EXISTS "IX_UAVs_LastTechnicalInspectionId" ON "UAVs" ("LastTechnicalInspectionId");
                ALTER TABLE "UAVs" ADD CONSTRAINT "FK_UAVs_Regions_AssignedRegionId"
                    FOREIGN KEY ("AssignedRegionId") REFERENCES "Regions" ("Id") ON DELETE SET NULL;
                ALTER TABLE "UAVs" ADD CONSTRAINT "FK_UAVs_DroneTechnicalInspections_LastTechnicalInspectionId"
                    FOREIGN KEY ("LastTechnicalInspectionId") REFERENCES "DroneTechnicalInspections" ("Id") ON DELETE SET NULL;

                -- active pairing = UnpairedAt IS NULL, at most one per drone and per controller UID
                ALTER TABLE "DroneFlightControllers" ADD COLUMN IF NOT EXISTS "UnpairedAt" timestamp with time zone NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('DroneFlightControllers', 'IsActive'), $q$
                    UPDATE "DroneFlightControllers" SET "UnpairedAt" = COALESCE("UpdatedAt", now())
                    WHERE NOT "IsActive" AND "UnpairedAt" IS NULL$q$);
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
                DROP INDEX IF EXISTS "IX_DroneFlightControllers_DroneId";
                DROP INDEX IF EXISTS "IX_DroneFlightControllers_FlightControllerUid";
                ALTER TABLE "DroneFlightControllers" DROP COLUMN IF EXISTS "IsActive";
                CREATE UNIQUE INDEX "IX_DroneFlightControllers_DroneId" ON "DroneFlightControllers" ("DroneId")
                    WHERE "UnpairedAt" IS NULL AND NOT "IsDeleted";
                CREATE UNIQUE INDEX "IX_DroneFlightControllers_FlightControllerUid" ON "DroneFlightControllers" ("FlightControllerUid")
                    WHERE "UnpairedAt" IS NULL AND NOT "IsDeleted";

                ALTER TABLE "DroneTechnicalInspections" DROP CONSTRAINT IF EXISTS "FK_DroneTechnicalInspections_Users_TechnicianUserId";
                ALTER TABLE "DroneTechnicalInspections" DROP CONSTRAINT IF EXISTS "FK_DroneTechnicalInspections_Users_TechnicianId";
                CALL pg_temp.rename_col('DroneTechnicalInspections', 'TechnicianUserId', 'TechnicianId');
                ALTER TABLE "DroneTechnicalInspections" ADD COLUMN IF NOT EXISTS "TechnicianId" uuid NULL;
                DROP INDEX IF EXISTS "IX_DroneTechnicalInspections_TechnicianUserId";
                UPDATE "DroneTechnicalInspections" i SET "TechnicianId" = NULL
                WHERE "TechnicianId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = i."TechnicianId");
                CREATE INDEX IF NOT EXISTS "IX_DroneTechnicalInspections_TechnicianId" ON "DroneTechnicalInspections" ("TechnicianId");
                ALTER TABLE "DroneTechnicalInspections" ADD CONSTRAINT "FK_DroneTechnicalInspections_Users_TechnicianId"
                    FOREIGN KEY ("TechnicianId") REFERENCES "Users" ("Id") ON DELETE RESTRICT;

                CALL pg_temp.exec_if(pg_temp.has_col('DroneTechnicalMetrics', 'Critical'), $q$
                    UPDATE "DroneTechnicalMetrics" SET "Severity" = 'Critical'
                    WHERE "Critical" AND ("Severity" IS NULL OR btrim("Severity") = '')$q$);
                ALTER TABLE "DroneTechnicalMetrics" DROP COLUMN IF EXISTS "Critical";
                """);

            // ---------------------------------------------------------------
            // 8. Inspection media metadata (video metadata moves from anomalies to media)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "Checksum" character varying(64) NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "FileSizeBytes" bigint NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "WidthPx" integer NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "HeightPx" integer NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "DurationSeconds" double precision NULL;
                ALTER TABLE "InspectionMedia" ADD COLUMN IF NOT EXISTS "Fps" double precision NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('DetectedAnomalies', 'VideoDuration') AND pg_temp.has_col('DetectedAnomalies', 'VideoFps')
                                     AND pg_temp.has_col('DetectedAnomalies', 'VideoWidth') AND pg_temp.has_col('DetectedAnomalies', 'VideoHeight'), $q$
                    UPDATE "InspectionMedia" m SET
                        "DurationSeconds" = COALESCE(m."DurationSeconds", v.duration), "Fps" = COALESCE(m."Fps", v.fps),
                        "WidthPx" = COALESCE(m."WidthPx", v.width), "HeightPx" = COALESCE(m."HeightPx", v.height)
                    FROM (SELECT "MediaId", max("VideoDuration") AS duration, max("VideoFps") AS fps,
                                 max("VideoWidth") AS width, max("VideoHeight") AS height
                          FROM "DetectedAnomalies" GROUP BY "MediaId") v
                    WHERE v."MediaId" = m."Id"$q$);
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_InspectionMedia_Checksum" ON "InspectionMedia" ("Checksum")
                    WHERE "Checksum" IS NOT NULL AND NOT "IsDeleted";

                UPDATE "InspectionMedia" m SET "TowerId" = NULL
                WHERE "TowerId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = m."TowerId");
                ALTER TABLE "InspectionMedia" DROP CONSTRAINT IF EXISTS "FK_InspectionMedia_Towers_TowerId";
                ALTER TABLE "InspectionMedia" DROP CONSTRAINT IF EXISTS "InspectionMedia_TowerId_fkey";
                CREATE INDEX IF NOT EXISTS "IX_InspectionMedia_TowerId" ON "InspectionMedia" ("TowerId");
                ALTER TABLE "InspectionMedia" ADD CONSTRAINT "FK_InspectionMedia_Towers_TowerId"
                    FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE SET NULL;

                UPDATE "IncidentReports" i SET "TowerId" = NULL
                WHERE "TowerId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Towers" t WHERE t."Id" = i."TowerId");
                ALTER TABLE "IncidentReports" DROP CONSTRAINT IF EXISTS "FK_IncidentReports_Towers_TowerId";
                CREATE INDEX IF NOT EXISTS "IX_IncidentReports_TowerId" ON "IncidentReports" ("TowerId");
                ALTER TABLE "IncidentReports" ADD CONSTRAINT "FK_IncidentReports_Towers_TowerId"
                    FOREIGN KEY ("TowerId") REFERENCES "Towers" ("Id") ON DELETE SET NULL;
                """);

            // ---------------------------------------------------------------
            // 9. AI analysis runs (edge + cloud) and anomaly lineage
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "AIAnalysisRuns" (
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

                -- cloud requests become cloud runs (requests without media cannot be represented and are dropped).
                -- AnalysisType/Status were stored as integers; text values from hand-patched rows are kept when valid.
                CALL pg_temp.exec_if(pg_temp.has_col('AIAnalysisRequests', 'MediaId') AND pg_temp.has_col('AIAnalysisRequests', 'UploadedBy'), $q$
                    INSERT INTO "AIAnalysisRuns"
                        ("Id", "MediaId", "ExecutionLocation", "UavId", "RequestedBy", "AnalysisType", "ModelName", "ModelVersion",
                         "Status", "SourceEventId", "BatchId", "ErrorMessage", "StartedAt", "CompletedAt",
                         "CreatedAt", "UpdatedAt", "CreatedBy", "UpdatedBy", "IsDeleted", "DeletedAt")
                    SELECT q."Id", q."MediaId", 'Cloud', NULL,
                           CASE WHEN EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = q."UploadedBy") THEN q."UploadedBy" END,
                           CASE q."AnalysisType"::text WHEN '0' THEN 'DefectDetection' WHEN '1' THEN 'HumanMotionDetection'
                                WHEN '2' THEN 'ObjectClassification' WHEN '3' THEN 'AssetConditionAssessment' WHEN '4' THEN 'General'
                                ELSE CASE WHEN q."AnalysisType"::text IN ('DefectDetection', 'HumanMotionDetection', 'ObjectClassification',
                                                                          'AssetConditionAssessment', 'General')
                                          THEN q."AnalysisType"::text ELSE 'General' END END,
                           left(COALESCE(NULLIF(btrim(q."ModelName"), ''), 'SERVER'), 100), NULL,
                           CASE q."Status"::text WHEN '0' THEN 'Pending' WHEN '1' THEN 'Processing' WHEN '2' THEN 'Completed' WHEN '3' THEN 'Failed'
                                ELSE CASE WHEN q."Status"::text IN ('Pending', 'Processing', 'Completed', 'Failed')
                                          THEN q."Status"::text ELSE 'Failed' END END,
                           q."SourceEventId", q."BatchId", q."Notes", NULL, q."CompletedAt",
                           q."CreatedAt", q."UpdatedAt", q."CreatedBy", q."UpdatedBy", q."IsDeleted", q."DeletedAt"
                    FROM "AIAnalysisRequests" q
                    WHERE q."MediaId" IS NOT NULL AND EXISTS (SELECT 1 FROM "InspectionMedia" m WHERE m."Id" = q."MediaId")
                    ON CONFLICT DO NOTHING$q$);

                ALTER TABLE "DetectedAnomalies" ADD COLUMN IF NOT EXISTS "AnalysisRunId" uuid NULL;
                ALTER TABLE "DetectedAnomalies" ADD COLUMN IF NOT EXISTS "SupersededById" uuid NULL;

                -- link AI anomalies to the latest cloud run of the same media and model
                CALL pg_temp.exec_if(pg_temp.has_col('DetectedAnomalies', 'AiSource'), $q$
                    UPDATE "DetectedAnomalies" d SET "AnalysisRunId" = (
                        SELECT r."Id" FROM "AIAnalysisRuns" r
                        WHERE r."MediaId" = d."MediaId" AND r."ModelName" = d."AiSource"
                        ORDER BY r."CompletedAt" DESC NULLS LAST, r."CreatedAt" DESC
                        LIMIT 1)
                    WHERE d."AnalysisRunId" IS NULL AND COALESCE(btrim(d."AiSource"), '') NOT IN ('', 'Manual')$q$);

                -- remaining AI anomalies (edge results had no request row): one completed run per media + source
                CALL pg_temp.exec_if(pg_temp.has_col('DetectedAnomalies', 'AiSource'), $q$
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
                    GROUP BY d."MediaId", d."AiSource"
                    ON CONFLICT DO NOTHING$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('DetectedAnomalies', 'AiSource'), $q$
                    UPDATE "DetectedAnomalies" d SET "AnalysisRunId" = md5(d."MediaId"::text || '|' || d."AiSource")::uuid
                    WHERE d."AnalysisRunId" IS NULL AND COALESCE(btrim(d."AiSource"), '') NOT IN ('', 'Manual')
                      AND EXISTS (SELECT 1 FROM "AIAnalysisRuns" r
                                  WHERE r."Id" = md5(d."MediaId"::text || '|' || d."AiSource")::uuid AND r."MediaId" = d."MediaId")$q$);
                UPDATE "DetectedAnomalies" d SET "AnalysisRunId" = NULL
                WHERE d."AnalysisRunId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "AIAnalysisRuns" r WHERE r."Id" = d."AnalysisRunId" AND r."MediaId" = d."MediaId");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AIAnalysisRuns_ActiveLogicalAnalysis" ON "AIAnalysisRuns" ("MediaId", "AnalysisType", "ModelName")
                    WHERE "IsDeleted" = false AND "Status" IN ('Pending', 'Processing');
                CREATE INDEX IF NOT EXISTS "IX_AIAnalysisRuns_RequestedBy" ON "AIAnalysisRuns" ("RequestedBy");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_AIAnalysisRuns_SourceEventId" ON "AIAnalysisRuns" ("SourceEventId") WHERE "SourceEventId" IS NOT NULL;
                CREATE INDEX IF NOT EXISTS "IX_AIAnalysisRuns_UavId" ON "AIAnalysisRuns" ("UavId");
                -- target of the composite FK below (run and anomaly must point at the same media)
                ALTER TABLE "AIAnalysisRuns" DROP CONSTRAINT IF EXISTS "AK_AIAnalysisRuns_Id_MediaId" CASCADE;
                ALTER TABLE "AIAnalysisRuns" ADD CONSTRAINT "AK_AIAnalysisRuns_Id_MediaId" UNIQUE ("Id", "MediaId");

                DROP TABLE IF EXISTS "AIAnalysisRequests" CASCADE;
                """);

            // ---------------------------------------------------------------
            // 10. DetectedAnomalies contract
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "DetectedAnomalies" ADD COLUMN IF NOT EXISTS "Location" geometry(Point,4326) NULL;
                CALL pg_temp.exec_if(pg_temp.col_type('DetectedAnomalies', 'Gps') IN ('jsonb', 'json'), $q$
                    UPDATE "DetectedAnomalies"
                    SET "Location" = ST_SetSRID(ST_MakePoint(
                            COALESCE("Gps"::jsonb->>'lng', "Gps"::jsonb->>'lon', "Gps"::jsonb->>'longitude')::double precision,
                            COALESCE("Gps"::jsonb->>'lat', "Gps"::jsonb->>'latitude')::double precision), 4326)
                    WHERE "Location" IS NULL AND "Gps" IS NOT NULL AND jsonb_typeof("Gps"::jsonb) = 'object'
                      AND COALESCE("Gps"::jsonb->>'lng', "Gps"::jsonb->>'lon', "Gps"::jsonb->>'longitude') ~ '^-?[0-9]+(\.[0-9]+)?$'
                      AND COALESCE("Gps"::jsonb->>'lat', "Gps"::jsonb->>'latitude') ~ '^-?[0-9]+(\.[0-9]+)?$'$q$);
                CALL pg_temp.rename_col('DetectedAnomalies', 'Timestamp', 'FrameTimeSeconds');
                ALTER TABLE "DetectedAnomalies" ADD COLUMN IF NOT EXISTS "FrameTimeSeconds" double precision NULL;

                DROP INDEX IF EXISTS "IX_DetectedAnomalies_MediaId_AiDetectionId";
                DROP INDEX IF EXISTS "IX_DetectedAnomalies_AnalysisRunId_AiDetectionId";
                DROP INDEX IF EXISTS "IX_DetectedAnomalies_TowerId";
                DROP INDEX IF EXISTS "IX_DetectedAnomalies_SupersededById";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "AiSource";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "AssetId";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "Gps";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "VideoDuration";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "VideoFps";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "VideoWidth";
                ALTER TABLE "DetectedAnomalies" DROP COLUMN IF EXISTS "VideoHeight";
                ALTER TABLE "DetectedAnomalies" ALTER COLUMN "ValidationStatus" TYPE character varying(20) USING left("ValidationStatus", 20);

                -- AI detection ids must be unique per run; keep the oldest duplicate's id
                UPDATE "DetectedAnomalies" d SET "AiDetectionId" = NULL
                FROM (SELECT "Id", row_number() OVER (PARTITION BY "AnalysisRunId", "AiDetectionId" ORDER BY "CreatedAt") AS rn
                      FROM "DetectedAnomalies"
                      WHERE "AiDetectionId" IS NOT NULL AND NOT "IsDeleted" AND "AnalysisRunId" IS NOT NULL) x
                WHERE d."Id" = x."Id" AND x.rn > 1;

                CREATE INDEX IF NOT EXISTS "IX_DetectedAnomalies_MediaId" ON "DetectedAnomalies" ("MediaId");
                CREATE INDEX "IX_DetectedAnomalies_TowerId" ON "DetectedAnomalies" ("TowerId");
                CREATE INDEX "IX_DetectedAnomalies_SupersededById" ON "DetectedAnomalies" ("SupersededById");
                CREATE UNIQUE INDEX "IX_DetectedAnomalies_AnalysisRunId_AiDetectionId" ON "DetectedAnomalies" ("AnalysisRunId", "AiDetectionId")
                    WHERE "AiDetectionId" IS NOT NULL AND "IsDeleted" = false;
                ALTER TABLE "DetectedAnomalies" DROP CONSTRAINT IF EXISTS "FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId";
                ALTER TABLE "DetectedAnomalies" DROP CONSTRAINT IF EXISTS "FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId_MediaId";
                ALTER TABLE "DetectedAnomalies" DROP CONSTRAINT IF EXISTS "FK_DetectedAnomalies_DetectedAnomalies_SupersededById";
                ALTER TABLE "DetectedAnomalies" DROP CONSTRAINT IF EXISTS "FK_DetectedAnomalies_Towers_TowerId";
                ALTER TABLE "DetectedAnomalies" DROP CONSTRAINT IF EXISTS "CK_DetectedAnomalies_ConfirmedRequiresTower";
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
                CALL pg_temp.exec_if(pg_temp.has_col('EmergencyAlerts', 'DeliveryLatencySeconds'), $q$
                    UPDATE "EmergencyAlerts" SET "ReceivedAt" = "TriggeredAt" + make_interval(secs => "DeliveryLatencySeconds")
                    WHERE "ReceivedAt" IS NULL AND "DeliveryLatencySeconds" > 0$q$);
                ALTER TABLE "EmergencyAlerts" DROP COLUMN IF EXISTS "AssetId";
                ALTER TABLE "EmergencyAlerts" DROP COLUMN IF EXISTS "TowerId";
                ALTER TABLE "EmergencyAlerts" DROP COLUMN IF EXISTS "MissionId";
                ALTER TABLE "EmergencyAlerts" DROP COLUMN IF EXISTS "DeliveryLatencySeconds";
                ALTER TABLE "EmergencyAlerts" DROP CONSTRAINT IF EXISTS "FK_EmergencyAlerts_Users_ReviewedByUserId";
                ALTER TABLE "EmergencyAlerts" DROP CONSTRAINT IF EXISTS "FK_EmergencyAlerts_Users_ReviewedById";
                CALL pg_temp.rename_col('EmergencyAlerts', 'ReviewedByUserId', 'ReviewedById');
                ALTER TABLE "EmergencyAlerts" ADD COLUMN IF NOT EXISTS "ReviewedById" uuid NULL;
                DROP INDEX IF EXISTS "IX_EmergencyAlerts_ReviewedByUserId";
                UPDATE "EmergencyAlerts" e SET "ReviewedById" = NULL
                WHERE "ReviewedById" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Users" u WHERE u."Id" = e."ReviewedById");
                CREATE INDEX IF NOT EXISTS "IX_EmergencyAlerts_ReviewedById" ON "EmergencyAlerts" ("ReviewedById");
                ALTER TABLE "EmergencyAlerts" ADD CONSTRAINT "FK_EmergencyAlerts_Users_ReviewedById"
                    FOREIGN KEY ("ReviewedById") REFERENCES "Users" ("Id") ON DELETE SET NULL;

                -- tower of a ticket is anomaly.TowerId (already backfilled in step 3d)
                ALTER TABLE "MaintenanceTickets" DROP COLUMN IF EXISTS "AssetId";
                ALTER TABLE "MaintenanceTickets" DROP COLUMN IF EXISTS "TowerId";

                -- one row per proof file
                CALL pg_temp.exec_if(pg_temp.has_col('MaintenanceProofs', 'AfterRepairImageUrl'), $q$
                    INSERT INTO "MaintenanceProofs"
                        ("Id", "TicketId", "UploadedBy", "ProofType", "FileUrl", "AfterRepairImageUrl", "TechnicianNotes", "UploadedAt",
                         "CreatedAt", "CreatedBy", "IsDeleted")
                    SELECT gen_random_uuid(), p."TicketId", p."UploadedBy", 'After', p."AfterRepairImageUrl", '', p."TechnicianNotes", p."UploadedAt",
                           now(), p."CreatedBy", p."IsDeleted"
                    FROM "MaintenanceProofs" p
                    WHERE COALESCE(p."AfterRepairImageUrl", '') <> '' AND COALESCE(p."FileUrl", '') <> ''
                      AND p."AfterRepairImageUrl" <> p."FileUrl"$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('MaintenanceProofs', 'AfterRepairImageUrl'), $q$
                    UPDATE "MaintenanceProofs" SET "FileUrl" = "AfterRepairImageUrl", "ProofType" = 'After'
                    WHERE COALESCE("FileUrl", '') = '' AND COALESCE("AfterRepairImageUrl", '') <> ''$q$);
                ALTER TABLE "MaintenanceProofs" DROP COLUMN IF EXISTS "AfterRepairImageUrl";

                ALTER TABLE "IncidentReports" DROP COLUMN IF EXISTS "AssetId";
                ALTER TABLE "IncidentReports" DROP COLUMN IF EXISTS "FileUrl";

                ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "ReadAt" timestamp with time zone NULL;
                ALTER TABLE "Notifications" ADD COLUMN IF NOT EXISTS "PushedAt" timestamp with time zone NULL;
                CALL pg_temp.exec_if(pg_temp.has_col('Notifications', 'IsRead'), $q$
                    UPDATE "Notifications" SET "ReadAt" = "SentAt" WHERE "IsRead" AND "ReadAt" IS NULL$q$);
                CALL pg_temp.exec_if(pg_temp.has_col('Notifications', 'IsPushed'), $q$
                    UPDATE "Notifications" SET "PushedAt" = "SentAt" WHERE "IsPushed" AND "PushedAt" IS NULL$q$);
                ALTER TABLE "Notifications" DROP COLUMN IF EXISTS "IsRead";
                ALTER TABLE "Notifications" DROP COLUMN IF EXISTS "IsPushed";

                DROP INDEX IF EXISTS "IX_OutboxMessages_MessageType_OccurredAt";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "CreatedAt";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "CreatedBy";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "UpdatedAt";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "UpdatedBy";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "IsDeleted";
                ALTER TABLE "OutboxMessages" DROP COLUMN IF EXISTS "DeletedAt";
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
                ALTER TABLE "Towers" DROP COLUMN IF EXISTS "LineAssetId";
                ALTER TABLE "Towers" DROP COLUMN IF EXISTS "LineCode";
                ALTER TABLE "Towers" DROP COLUMN IF EXISTS "Voltage";
                ALTER TABLE "Towers" DROP COLUMN IF EXISTS "RiskLevel";
                ALTER TABLE "Towers" DROP COLUMN IF EXISTS "ActiveDefectsCount";
                ALTER TABLE "Towers" DROP COLUMN IF EXISTS "LastInspectedAt";

                ALTER TABLE "InspectionMedia" DROP COLUMN IF EXISTS "AssetId";
                ALTER TABLE "InspectionMedia" DROP COLUMN IF EXISTS "AiSource";

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
            // 13. Legacy drift outside the ERD (columns no service maps, raw-SQL constraint names)
            // ---------------------------------------------------------------
            migrationBuilder.Sql("""
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "AssignedToUserId";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "DroneCode";
                ALTER TABLE "Missions" DROP COLUMN IF EXISTS "RouteData";
                ALTER TABLE "Users" DROP COLUMN IF EXISTS "Username";

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

                DROP PROCEDURE IF EXISTS pg_temp.exec_if(boolean, text);
                DROP PROCEDURE IF EXISTS pg_temp.rename_col(text, text, text);
                DROP FUNCTION IF EXISTS pg_temp.col_type(text, text);
                DROP FUNCTION IF EXISTS pg_temp.has_col(text, text);
                DROP FUNCTION IF EXISTS pg_temp.has_table(text);
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
