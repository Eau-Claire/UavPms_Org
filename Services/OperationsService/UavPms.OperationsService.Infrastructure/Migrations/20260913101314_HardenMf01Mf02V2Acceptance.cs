using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HardenMf01Mf02V2Acceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DroneTechnicalMetrics_InspectionId_MetricCode",
                table: "DroneTechnicalMetrics");

            migrationBuilder.AddColumn<Guid>(
                name: "LastTechnicalInspectionId",
                table: "UAVs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TechnicalHealthUpdatedAt",
                table: "UAVs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvaluationPolicyVersion",
                table: "PreMissionAssessments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SiteFeasibilityStatus",
                table: "PreMissionAssessments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "PreMissionAssessments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvailabilityStatus",
                table: "PreMissionAssessmentPersonnel",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EligibilityStatus",
                table: "PreMissionAssessmentPersonnel",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "PreMissionAssessmentPersonnel",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotAt",
                table: "PreMissionAssessmentPersonnel",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "OperationalAvailabilityStatus",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReasonCode",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SnapshotAt",
                table: "PreMissionAssessmentDrones",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "TechnicalEligibilityStatus",
                table: "PreMissionAssessmentDrones",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegionId",
                table: "Missions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcceptedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostponeReason",
                table: "Missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostponedAt",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "MissionAssignments",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "MissionAssignments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "MissionAssignments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseReason",
                table: "MissionAssignments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseStatus",
                table: "MissionAssignments",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "MissionAssignments",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "BoolValue",
                table: "DroneTechnicalMetrics",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRequired",
                table: "DroneTechnicalMetrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Metadata",
                table: "DroneTechnicalMetrics",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "DroneTechnicalMetrics",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subsystem",
                table: "DroneTechnicalMetrics",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "DroneTechnicalMetrics",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "DroneTechnicalInspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyVersion",
                table: "DroneTechnicalInspections",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "DroneTechnicalInspections",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<Guid>(
                name: "TechnicianUserId",
                table: "DroneTechnicalInspections",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "DroneTechnicalInspections",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ResourceBookings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DroneId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("PK_ResourceBookings", x => x.Id);
                    table.CheckConstraint("CK_ResourceBookings_OneResource", "(\"UserId\" IS NOT NULL) <> (\"DroneId\" IS NOT NULL)");
                    table.CheckConstraint("CK_ResourceBookings_Time", "\"EndAt\" > \"StartAt\"");
                    table.ForeignKey(
                        name: "FK_ResourceBookings_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceBookings_UAVs_DroneId",
                        column: x => x.DroneId,
                        principalTable: "UAVs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceBookings_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MissionAssignments_PostponeReason",
                table: "MissionAssignments",
                sql: "\"ResponseStatus\" <> 'Postponed' OR (\"ResponseReason\" IS NOT NULL AND length(trim(\"ResponseReason\")) > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_DroneTechnicalMetrics_InspectionId_Subsystem_MetricCode",
                table: "DroneTechnicalMetrics",
                columns: new[] { "InspectionId", "Subsystem", "MetricCode" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DroneTechnicalMetrics_SingleValue",
                table: "DroneTechnicalMetrics",
                sql: "(CASE WHEN \"NumericValue\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"ValueText\" IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN \"BoolValue\" IS NOT NULL THEN 1 ELSE 0 END) <= 1");

            migrationBuilder.CreateIndex(
                name: "IX_DroneTechnicalInspections_TechnicianUserId",
                table: "DroneTechnicalInspections",
                column: "TechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_DroneId_StartAt_EndAt",
                table: "ResourceBookings",
                columns: new[] { "DroneId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_MissionId",
                table: "ResourceBookings",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceBookings_UserId_StartAt_EndAt",
                table: "ResourceBookings",
                columns: new[] { "UserId", "StartAt", "EndAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PreMissionAssessments_IdempotencyKey",
                table: "PreMissionAssessments",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_Missions_IdempotencyKey",
                table: "Missions",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL AND NOT \"IsDeleted\"");

            migrationBuilder.AddForeignKey(
                name: "FK_DroneTechnicalInspections_Users_TechnicianUserId",
                table: "DroneTechnicalInspections",
                column: "TechnicianUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DroneTechnicalInspections_Users_TechnicianUserId",
                table: "DroneTechnicalInspections");

            migrationBuilder.DropTable(
                name: "ResourceBookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MissionAssignments_PostponeReason",
                table: "MissionAssignments");

            migrationBuilder.DropIndex(
                name: "IX_DroneTechnicalMetrics_InspectionId_Subsystem_MetricCode",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DroneTechnicalMetrics_SingleValue",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropIndex(
                name: "IX_DroneTechnicalInspections_TechnicianUserId",
                table: "DroneTechnicalInspections");

            migrationBuilder.DropColumn(
                name: "LastTechnicalInspectionId",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "TechnicalHealthUpdatedAt",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "EvaluationPolicyVersion",
                table: "PreMissionAssessments");

            migrationBuilder.DropColumn(
                name: "SiteFeasibilityStatus",
                table: "PreMissionAssessments");

            migrationBuilder.DropColumn(
                name: "AvailabilityStatus",
                table: "PreMissionAssessmentPersonnel");

            migrationBuilder.DropColumn(
                name: "EligibilityStatus",
                table: "PreMissionAssessmentPersonnel");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "PreMissionAssessmentPersonnel");

            migrationBuilder.DropColumn(
                name: "SnapshotAt",
                table: "PreMissionAssessmentPersonnel");

            migrationBuilder.DropColumn(
                name: "OperationalAvailabilityStatus",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "ReasonCode",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "SnapshotAt",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "TechnicalEligibilityStatus",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "AcceptedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PostponeReason",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PostponedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "MissionAssignments");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "MissionAssignments");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "MissionAssignments");

            migrationBuilder.DropColumn(
                name: "ResponseReason",
                table: "MissionAssignments");

            migrationBuilder.DropColumn(
                name: "ResponseStatus",
                table: "MissionAssignments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "MissionAssignments");

            migrationBuilder.DropColumn(
                name: "BoolValue",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropColumn(
                name: "IsRequired",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropColumn(
                name: "Metadata",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropColumn(
                name: "Subsystem",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "DroneTechnicalMetrics");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "DroneTechnicalInspections");

            migrationBuilder.DropColumn(
                name: "PolicyVersion",
                table: "DroneTechnicalInspections");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "DroneTechnicalInspections");

            migrationBuilder.DropColumn(
                name: "TechnicianUserId",
                table: "DroneTechnicalInspections");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "DroneTechnicalInspections");

            migrationBuilder.AlterColumn<Guid>(
                name: "RegionId",
                table: "Missions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DroneTechnicalMetrics_InspectionId_MetricCode",
                table: "DroneTechnicalMetrics",
                columns: new[] { "InspectionId", "MetricCode" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_PreMissionAssessments_IdempotencyKey",
                table: "PreMissionAssessments");

            migrationBuilder.DropIndex(
                name: "IX_Missions_IdempotencyKey",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "PreMissionAssessments");
        }
    }
}
