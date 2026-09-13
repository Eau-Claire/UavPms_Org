using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Infrastructure.Persistence;

#nullable disable
namespace UavPms.OperationsService.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260909130000_AddMf01MissionLifecycle")]
public sealed class AddMf01MissionLifecycle : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        // RegionId stays nullable during rollout because legacy missions cannot all be
        // assigned an honest EVNSPC region without an operator-approved data mapping.
        m.AddColumn<Guid>("RegionId", "Missions", nullable: true);
        m.AddColumn<Guid>("ScheduleId", "Missions", nullable: true);
        m.AddColumn<string>("MissionType", "Missions", nullable: false, defaultValue: "AdHoc");
        m.AddColumn<string>("TriggerReason", "Missions", nullable: true);
        m.AddColumn<DateTime>("PlannedStart", "Missions", nullable: true);
        m.AddColumn<DateTime>("PlannedEnd", "Missions", nullable: true);
        m.AddColumn<Geometry>("Boundary", "Missions", type: "geometry(Geometry,4326)", nullable: true);
        m.AddColumn<uint>("Version", "Missions", nullable: false, defaultValue: 0u);
        m.Sql("""UPDATE "Missions" SET "Status"='Draft' WHERE "Status"='Pending'; UPDATE "Missions" SET "Status"='InProgress' WHERE "Status" IN ('Executing','In Progress');""");
        m.Sql("""
CREATE TABLE "InspectionSchedules" ("Id" uuid PRIMARY KEY,"RegionId" uuid NOT NULL REFERENCES "Regions"("Id") ON DELETE RESTRICT,"Name" varchar(256) NOT NULL,"RecurrenceRule" varchar(512) NOT NULL,"PlannedTime" time NOT NULL,"IsActive" boolean NOT NULL,"CreatedByUserId" uuid NOT NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NULL,"CreatedBy" uuid NULL,"UpdatedBy" uuid NULL,"IsDeleted" boolean NOT NULL,"DeletedAt" timestamptz NULL);
CREATE TABLE "MissionAssignments" ("Id" uuid PRIMARY KEY,"MissionId" uuid NOT NULL REFERENCES "Missions"("Id") ON DELETE CASCADE,"UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,"AssignmentRole" varchar(100) NOT NULL,"Status" text NOT NULL,"AssignedByUserId" uuid NOT NULL,"EndedAt" timestamptz NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NULL,"CreatedBy" uuid NULL,"UpdatedBy" uuid NULL,"IsDeleted" boolean NOT NULL,"DeletedAt" timestamptz NULL);
CREATE TABLE "MissionCheckIns" ("Id" uuid PRIMARY KEY,"MissionId" uuid NOT NULL REFERENCES "Missions"("Id") ON DELETE CASCADE,"UserId" uuid NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,"CheckedInAt" timestamptz NOT NULL,"Status" text NOT NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NULL,"CreatedBy" uuid NULL,"UpdatedBy" uuid NULL,"IsDeleted" boolean NOT NULL,"DeletedAt" timestamptz NULL);
CREATE TABLE "DroneHandovers" ("Id" uuid PRIMARY KEY,"MissionId" uuid NOT NULL REFERENCES "Missions"("Id") ON DELETE CASCADE,"DroneId" uuid NOT NULL REFERENCES "UAVs"("Id") ON DELETE RESTRICT,"HandedOverBy" uuid NOT NULL,"ReceivedBy" uuid NOT NULL,"ReceivedAt" timestamptz NOT NULL,"Condition" varchar(500) NOT NULL,"Status" text NOT NULL,"ReturnedAt" timestamptz NULL,"CreatedAt" timestamptz NOT NULL,"UpdatedAt" timestamptz NULL,"CreatedBy" uuid NULL,"UpdatedBy" uuid NULL,"IsDeleted" boolean NOT NULL,"DeletedAt" timestamptz NULL);
CREATE INDEX "IX_Missions_RegionId" ON "Missions"("RegionId"); CREATE INDEX "IX_Missions_ScheduleId" ON "Missions"("ScheduleId"); CREATE INDEX "IX_InspectionSchedules_RegionId" ON "InspectionSchedules"("RegionId");
CREATE UNIQUE INDEX "IX_MissionAssignments_MissionId_UserId" ON "MissionAssignments"("MissionId","UserId") WHERE "Status"='Active' AND NOT "IsDeleted";
CREATE UNIQUE INDEX "IX_MissionCheckIns_MissionId_UserId" ON "MissionCheckIns"("MissionId","UserId") WHERE NOT "IsDeleted";
CREATE UNIQUE INDEX "IX_DroneHandovers_MissionId_DroneId" ON "DroneHandovers"("MissionId","DroneId") WHERE "ReturnedAt" IS NULL AND NOT "IsDeleted";
ALTER TABLE "Missions" ADD CONSTRAINT "FK_Missions_Regions_RegionId" FOREIGN KEY ("RegionId") REFERENCES "Regions"("Id") ON DELETE RESTRICT;
ALTER TABLE "Missions" ADD CONSTRAINT "FK_Missions_InspectionSchedules_ScheduleId" FOREIGN KEY ("ScheduleId") REFERENCES "InspectionSchedules"("Id") ON DELETE RESTRICT;
""");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropForeignKey("FK_Missions_InspectionSchedules_ScheduleId", "Missions"); m.DropForeignKey("FK_Missions_Regions_RegionId", "Missions");
        m.DropTable("DroneHandovers"); m.DropTable("MissionCheckIns"); m.DropTable("MissionAssignments"); m.DropTable("InspectionSchedules");
        foreach (var column in new[] { "Boundary", "MissionType", "PlannedEnd", "PlannedStart", "RegionId", "ScheduleId", "TriggerReason", "Version" }) m.DropColumn(column, "Missions");
    }
}
