using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using UavPms.OperationsService.Infrastructure.Persistence;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260902033000_AddGisAssetSelection")]
public partial class AddGisAssetSelection : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Regions" ADD COLUMN "Code" text NOT NULL DEFAULT '';
            ALTER TABLE "Regions" ALTER COLUMN "Geom" TYPE geometry(Geometry,4326) USING ST_SetSRID("Geom",4326);
            ALTER TABLE "Regions" ADD COLUMN "Type" text NOT NULL DEFAULT '';
            ALTER TABLE "Regions" ADD COLUMN "ParentId" uuid NULL;
            UPDATE "Regions" SET "Code" = "Id"::text WHERE "Code" = '';
            CREATE UNIQUE INDEX "IX_Regions_Code" ON "Regions" ("Code");
            CREATE INDEX "IX_Regions_ParentId" ON "Regions" ("ParentId");
            ALTER TABLE "Regions" ADD CONSTRAINT "FK_Regions_Regions_ParentId" FOREIGN KEY ("ParentId") REFERENCES "Regions" ("Id") ON DELETE RESTRICT;

            CREATE TABLE "ManagementUnits" (
              "Id" uuid PRIMARY KEY, "Code" text NOT NULL, "Name" text NOT NULL, "Type" text NOT NULL,
              "ParentId" uuid NULL REFERENCES "ManagementUnits"("Id") ON DELETE RESTRICT, "Status" text NOT NULL,
              "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NULL, "CreatedBy" uuid NULL,
              "UpdatedBy" uuid NULL, "IsDeleted" boolean NOT NULL DEFAULT false, "DeletedAt" timestamptz NULL);
            CREATE UNIQUE INDEX "IX_ManagementUnits_Code" ON "ManagementUnits" ("Code");
            CREATE INDEX "IX_ManagementUnits_ParentId" ON "ManagementUnits" ("ParentId");

            ALTER TABLE "Assets" RENAME TO "AssetComponents";
            ALTER TABLE "AssetComponents" RENAME COLUMN "AssetType" TO "ComponentType";
            ALTER TABLE "AssetComponents" RENAME COLUMN "AssetCode" TO "ComponentCode";
            ALTER TABLE "AssetComponents" RENAME CONSTRAINT "PK_Assets" TO "PK_AssetComponents";
            ALTER TABLE "AssetComponents" RENAME CONSTRAINT "FK_Assets_Towers_TowerId" TO "FK_AssetComponents_Towers_TowerId";
            ALTER INDEX "IX_Assets_AssetCode" RENAME TO "IX_AssetComponents_ComponentCode";
            ALTER INDEX "IX_Assets_TowerId" RENAME TO "IX_AssetComponents_TowerId";
            ALTER TABLE "AssetHealthHistories" RENAME CONSTRAINT "FK_AssetHealthHistories_Assets_AssetId" TO "FK_AssetHealthHistories_AssetComponents_AssetId";
            ALTER TABLE "IncidentReports" RENAME CONSTRAINT "FK_IncidentReports_Assets_AssetId" TO "FK_IncidentReports_AssetComponents_AssetId";
            ALTER TABLE "InspectionMedia" RENAME CONSTRAINT "FK_InspectionMedia_Assets_AssetId" TO "FK_InspectionMedia_AssetComponents_AssetId";
            ALTER TABLE "DetectedAnomalies" RENAME CONSTRAINT "FK_DetectedAnomalies_Assets_AssetId" TO "FK_DetectedAnomalies_AssetComponents_AssetId";
            ALTER TABLE "EmergencyAlerts" RENAME CONSTRAINT "FK_EmergencyAlerts_Assets_AssetId" TO "FK_EmergencyAlerts_AssetComponents_AssetId";
            ALTER TABLE "MaintenanceTickets" RENAME CONSTRAINT "FK_MaintenanceTickets_Assets_AssetId" TO "FK_MaintenanceTickets_AssetComponents_AssetId";

            ALTER TABLE "TransmissionLines" ADD COLUMN "Code" text NOT NULL DEFAULT '';
            ALTER TABLE "TransmissionLines" ALTER COLUMN "Geom" TYPE geometry(Geometry,4326) USING ST_SetSRID("Geom",4326);
            ALTER TABLE "TransmissionLines" ADD COLUMN "VoltageLevel" text NOT NULL DEFAULT '';
            ALTER TABLE "TransmissionLines" ADD COLUMN "ManagementUnitId" uuid NULL REFERENCES "ManagementUnits"("Id") ON DELETE RESTRICT;
            ALTER TABLE "TransmissionLines" ADD COLUMN "Status" text NOT NULL DEFAULT 'Active';
            UPDATE "TransmissionLines" SET "Code" = "Id"::text WHERE "Code" = '';
            CREATE UNIQUE INDEX "IX_TransmissionLines_Code" ON "TransmissionLines" ("Code");
            CREATE INDEX "IX_TransmissionLines_ManagementUnitId" ON "TransmissionLines" ("ManagementUnitId");

            ALTER TABLE "AssetComponents" ADD COLUMN "PowerLineId" uuid NULL REFERENCES "TransmissionLines"("Id") ON DELETE RESTRICT;
            ALTER TABLE "AssetComponents" ADD COLUMN "ManagementUnitId" uuid NULL REFERENCES "ManagementUnits"("Id") ON DELETE RESTRICT;
            ALTER TABLE "AssetComponents" ADD COLUMN "Location" geometry(Point,4326) NULL;
            UPDATE "AssetComponents" a SET "Location" = ST_SetSRID(ST_Point(ST_X(t."Geom"), ST_Y(t."Geom")),4326), "PowerLineId" = t."LineAssetId"
              FROM "Towers" t WHERE a."TowerId" = t."Id" AND GeometryType(t."Geom") = 'POINT';
            CREATE INDEX "IX_AssetComponents_PowerLineId" ON "AssetComponents" ("PowerLineId");
            CREATE INDEX "IX_AssetComponents_ManagementUnitId" ON "AssetComponents" ("ManagementUnitId");
            CREATE INDEX "IX_AssetComponents_Location" ON "AssetComponents" USING gist ("Location");

            CREATE TABLE "LineSegments" (
              "Id" uuid PRIMARY KEY, "PowerLineId" uuid NOT NULL REFERENCES "TransmissionLines"("Id") ON DELETE CASCADE,
              "FromAssetId" uuid NOT NULL REFERENCES "AssetComponents"("Id") ON DELETE RESTRICT,
              "ToAssetId" uuid NOT NULL REFERENCES "AssetComponents"("Id") ON DELETE RESTRICT,
              "Sequence" integer NOT NULL, "Geometry" geometry(Geometry,4326) NULL, "Status" text NOT NULL,
              "CreatedAt" timestamptz NOT NULL, "UpdatedAt" timestamptz NULL, "CreatedBy" uuid NULL,
              "UpdatedBy" uuid NULL, "IsDeleted" boolean NOT NULL DEFAULT false, "DeletedAt" timestamptz NULL);
            CREATE INDEX "IX_LineSegments_PowerLineId" ON "LineSegments" ("PowerLineId");
            CREATE INDEX "IX_LineSegments_FromAssetId" ON "LineSegments" ("FromAssetId");
            CREATE INDEX "IX_LineSegments_ToAssetId" ON "LineSegments" ("ToAssetId");
            CREATE INDEX "IX_LineSegments_Geometry" ON "LineSegments" USING gist ("Geometry");

            ALTER TABLE "MissionTargets" ADD COLUMN "AssetId" uuid NULL;
            UPDATE "MissionTargets" mt SET "AssetId" = (SELECT ac."Id" FROM "AssetComponents" ac WHERE ac."TowerId" = mt."TowerId" ORDER BY ac."Id" LIMIT 1);
            DELETE FROM "MissionTargets" WHERE "AssetId" IS NULL;
            ALTER TABLE "MissionTargets" DROP CONSTRAINT "FK_MissionTargets_Towers_TowerId";
            DROP INDEX "IX_MissionTargets_MissionId_TowerId";
            DROP INDEX "IX_MissionTargets_TowerId";
            ALTER TABLE "MissionTargets" DROP COLUMN "TowerId";
            ALTER TABLE "MissionTargets" RENAME COLUMN "Status" TO "InspectionStatus";
            ALTER TABLE "MissionTargets" ALTER COLUMN "AssetId" SET NOT NULL;
            ALTER TABLE "MissionTargets" ADD COLUMN "CreatedAt" timestamptz NOT NULL DEFAULT now();
            ALTER TABLE "MissionTargets" ADD COLUMN "UpdatedAt" timestamptz NULL;
            ALTER TABLE "MissionTargets" ADD COLUMN "CreatedBy" uuid NULL;
            ALTER TABLE "MissionTargets" ADD COLUMN "UpdatedBy" uuid NULL;
            ALTER TABLE "MissionTargets" ADD COLUMN "IsDeleted" boolean NOT NULL DEFAULT false;
            ALTER TABLE "MissionTargets" ADD COLUMN "DeletedAt" timestamptz NULL;
            ALTER TABLE "MissionTargets" ADD CONSTRAINT "FK_MissionTargets_AssetComponents_AssetId" FOREIGN KEY ("AssetId") REFERENCES "AssetComponents"("Id") ON DELETE RESTRICT;
            CREATE INDEX "IX_MissionTargets_AssetId" ON "MissionTargets" ("AssetId");
            CREATE UNIQUE INDEX "IX_MissionTargets_MissionId_AssetId" ON "MissionTargets" ("MissionId", "AssetId");

            ALTER TABLE "InspectionMedia" ADD COLUMN "TowerId" uuid NULL REFERENCES "Towers"("Id") ON DELETE RESTRICT;
            ALTER TABLE "InspectionMedia" ADD COLUMN "CaptureLocation" geometry NULL;
            CREATE INDEX "IX_InspectionMedia_TowerId" ON "InspectionMedia" ("TowerId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // The prior migration represents targets by tower; reconstructing that lossy model is deliberately unsupported.
        throw new NotSupportedException("GIS asset targets cannot be safely downgraded to tower targets.");
    }
}
