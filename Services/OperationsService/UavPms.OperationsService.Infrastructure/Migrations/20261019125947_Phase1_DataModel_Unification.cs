using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_DataModel_Unification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "UAVs",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");


            migrationBuilder.AddColumn<int>(
                name: "ActiveDefectsCount",
                table: "Towers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Towers",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInspectedAt",
                table: "Towers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LineCode",
                table: "Towers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PoleType",
                table: "Towers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                table: "Towers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "Towers",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "Voltage",
                table: "Towers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");


            migrationBuilder.AddColumn<Geometry>(
                name: "CheckInLocation",
                table: "MissionCheckIns",
                type: "geometry",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "MaintenanceTickets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "TowerId",
                table: "MaintenanceTickets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProofType",
                table: "MaintenanceProofs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "After");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "IncidentReports",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "TowerId",
                table: "IncidentReports",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TowerId",
                table: "EmergencyAlerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InitialChecklist",
                table: "DroneHandovers",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReturnChecklist",
                table: "DroneHandovers",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnalystFeedback",
                table: "DetectedAnomalies",
                type: "jsonb",
                nullable: true);


            migrationBuilder.CreateTable(
                name: "TowerConditionHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TowerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskLevel = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Normal"),
                    ActiveDefectsCount = table.Column<int>(type: "integer", nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowerConditionHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TowerConditionHistories_Towers_TowerId",
                        column: x => x.TowerId,
                        principalTable: "Towers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Towers_LineCode",
                table: "Towers",
                column: "LineCode");

            migrationBuilder.CreateIndex(
                name: "IX_Towers_RegionId",
                table: "Towers",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_Towers_RiskLevel",
                table: "Towers",
                column: "RiskLevel");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCheckIns_CheckInLocation",
                table: "MissionCheckIns",
                column: "CheckInLocation")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceTickets_TowerId",
                table: "MaintenanceTickets",
                column: "TowerId");

            migrationBuilder.CreateIndex(
                name: "IX_IncidentReports_TowerId",
                table: "IncidentReports",
                column: "TowerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAlerts_TowerId",
                table: "EmergencyAlerts",
                column: "TowerId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_HandedOverBy",
                table: "DroneHandovers",
                column: "HandedOverBy");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_ReceivedBy",
                table: "DroneHandovers",
                column: "ReceivedBy");


            migrationBuilder.CreateIndex(
                name: "IX_TowerConditionHistories_AssessedAt",
                table: "TowerConditionHistories",
                column: "AssessedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TowerConditionHistories_TowerId",
                table: "TowerConditionHistories",
                column: "TowerId");

            migrationBuilder.AddForeignKey(
                name: "FK_DroneHandovers_Users_HandedOverBy",
                table: "DroneHandovers",
                column: "HandedOverBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DroneHandovers_Users_ReceivedBy",
                table: "DroneHandovers",
                column: "ReceivedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmergencyAlerts_Towers_TowerId",
                table: "EmergencyAlerts",
                column: "TowerId",
                principalTable: "Towers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_IncidentReports_Towers_TowerId",
                table: "IncidentReports",
                column: "TowerId",
                principalTable: "Towers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceTickets_Towers_TowerId",
                table: "MaintenanceTickets",
                column: "TowerId",
                principalTable: "Towers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Towers_Regions_RegionId",
                table: "Towers",
                column: "RegionId",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DroneHandovers_Users_HandedOverBy",
                table: "DroneHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_DroneHandovers_Users_ReceivedBy",
                table: "DroneHandovers");

            migrationBuilder.DropForeignKey(
                name: "FK_EmergencyAlerts_Towers_TowerId",
                table: "EmergencyAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_IncidentReports_Towers_TowerId",
                table: "IncidentReports");

            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceTickets_Towers_TowerId",
                table: "MaintenanceTickets");

            migrationBuilder.DropForeignKey(
                name: "FK_Towers_Regions_RegionId",
                table: "Towers");


            migrationBuilder.DropTable(
                name: "TowerConditionHistories");

            migrationBuilder.DropIndex(
                name: "IX_Towers_LineCode",
                table: "Towers");

            migrationBuilder.DropIndex(
                name: "IX_Towers_RegionId",
                table: "Towers");

            migrationBuilder.DropIndex(
                name: "IX_Towers_RiskLevel",
                table: "Towers");

            migrationBuilder.DropIndex(
                name: "IX_MissionCheckIns_CheckInLocation",
                table: "MissionCheckIns");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceTickets_TowerId",
                table: "MaintenanceTickets");

            migrationBuilder.DropIndex(
                name: "IX_IncidentReports_TowerId",
                table: "IncidentReports");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyAlerts_TowerId",
                table: "EmergencyAlerts");

            migrationBuilder.DropIndex(
                name: "IX_DroneHandovers_HandedOverBy",
                table: "DroneHandovers");

            migrationBuilder.DropIndex(
                name: "IX_DroneHandovers_ReceivedBy",
                table: "DroneHandovers");

            migrationBuilder.DropColumn(
                name: "AssignedRegionId",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "AssignedRegionName",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "CameraPayloadInfo",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "FlightEnduranceMinutes",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "OperationalRangeKm",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "ActiveDefectsCount",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "LastInspectedAt",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "LineCode",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "PoleType",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "RegionId",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "Voltage",
                table: "Towers");

            migrationBuilder.DropColumn(
                name: "CoverageDistanceKm",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "EstimatedFlightsNeeded",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "Suitability",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "SuitabilityReason",
                table: "PreMissionAssessmentDrones");

            migrationBuilder.DropColumn(
                name: "CheckInLocation",
                table: "MissionCheckIns");

            migrationBuilder.DropColumn(
                name: "TowerId",
                table: "MaintenanceTickets");

            migrationBuilder.DropColumn(
                name: "ProofType",
                table: "MaintenanceProofs");

            migrationBuilder.DropColumn(
                name: "TowerId",
                table: "IncidentReports");

            migrationBuilder.DropColumn(
                name: "TowerId",
                table: "EmergencyAlerts");

            migrationBuilder.DropColumn(
                name: "InitialChecklist",
                table: "DroneHandovers");

            migrationBuilder.DropColumn(
                name: "ReturnChecklist",
                table: "DroneHandovers");

            migrationBuilder.DropColumn(
                name: "AnalystFeedback",
                table: "DetectedAnomalies");

            migrationBuilder.AlterColumn<string>(
                name: "Model",
                table: "UAVs",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "MaintenanceTickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "IncidentReports",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
