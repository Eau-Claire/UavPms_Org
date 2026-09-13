using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPreMissionAssessmentAndDroneTechnicalReadinessV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Geometry>(
                name: "Boundary",
                table: "Missions",
                type: "geometry(Geometry,4326)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MissionType",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedEnd",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PlannedStart",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                table: "Missions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ScheduleId",
                table: "Missions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TriggerReason",
                table: "Missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Missions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "DroneHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DroneId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandedOverBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Condition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneHandovers_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DroneHandovers_UAVs_DroneId",
                        column: x => x.DroneId,
                        principalTable: "UAVs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InspectionSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RecurrenceRule = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    PlannedTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InspectionSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InspectionSchedules_Regions_RegionId",
                        column: x => x.RegionId,
                        principalTable: "Regions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MissionAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentRole = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionAssignments_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionAssignments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MissionCheckIns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CheckedInAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionCheckIns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionCheckIns_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionCheckIns_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserGeographicScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubstationId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransmissionLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagementUnitId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserGeographicScopes", x => x.Id);
                    table.CheckConstraint("CK_UserGeographicScopes_HasScope", "(\"RegionId\" IS NOT NULL OR \"SubstationId\" IS NOT NULL OR \"TransmissionLineId\" IS NOT NULL OR \"ManagementUnitId\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_UserGeographicScopes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_RegionId",
                table: "Missions",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_ScheduleId",
                table: "Missions",
                column: "ScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_DroneId",
                table: "DroneHandovers",
                column: "DroneId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_MissionId_DroneId",
                table: "DroneHandovers",
                columns: new[] { "MissionId", "DroneId" },
                unique: true,
                filter: "\"ReturnedAt\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionSchedules_RegionId",
                table: "InspectionSchedules",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionAssignments_MissionId_UserId",
                table: "MissionAssignments",
                columns: new[] { "MissionId", "UserId" },
                unique: true,
                filter: "\"Status\" = 'Active' AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_MissionAssignments_UserId",
                table: "MissionAssignments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCheckIns_MissionId_UserId",
                table: "MissionCheckIns",
                columns: new[] { "MissionId", "UserId" },
                unique: true,
                filter: "NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCheckIns_UserId",
                table: "MissionCheckIns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGeographicScopes_UserId_ManagementUnitId",
                table: "UserGeographicScopes",
                columns: new[] { "UserId", "ManagementUnitId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGeographicScopes_UserId_RegionId",
                table: "UserGeographicScopes",
                columns: new[] { "UserId", "RegionId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGeographicScopes_UserId_SubstationId",
                table: "UserGeographicScopes",
                columns: new[] { "UserId", "SubstationId" });

            migrationBuilder.CreateIndex(
                name: "IX_UserGeographicScopes_UserId_TransmissionLineId",
                table: "UserGeographicScopes",
                columns: new[] { "UserId", "TransmissionLineId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Missions_InspectionSchedules_ScheduleId",
                table: "Missions",
                column: "ScheduleId",
                principalTable: "InspectionSchedules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Missions_Regions_RegionId",
                table: "Missions",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Missions_InspectionSchedules_ScheduleId",
                table: "Missions");

            migrationBuilder.DropForeignKey(
                name: "FK_Missions_Regions_RegionId",
                table: "Missions");

            migrationBuilder.DropTable(
                name: "DroneHandovers");

            migrationBuilder.DropTable(
                name: "InspectionSchedules");

            migrationBuilder.DropTable(
                name: "MissionAssignments");

            migrationBuilder.DropTable(
                name: "MissionCheckIns");

            migrationBuilder.DropTable(
                name: "UserGeographicScopes");

            migrationBuilder.DropIndex(
                name: "IX_Missions_RegionId",
                table: "Missions");

            migrationBuilder.DropIndex(
                name: "IX_Missions_ScheduleId",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Boundary",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "MissionType",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PlannedEnd",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PlannedStart",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ScheduleId",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "TriggerReason",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "Missions");
        }
    }
}
