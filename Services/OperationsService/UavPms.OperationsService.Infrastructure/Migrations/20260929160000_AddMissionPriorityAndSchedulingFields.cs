using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionPriorityAndSchedulingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Objective",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "PeriodicInspection");

            migrationBuilder.AddColumn<string>(
                name: "PriorityDefectsJson",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "EmergencyReason",
                table: "Missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsImmediate",
                table: "Missions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Missions_Priority_PlannedStart_CreatedAt",
                table: "Missions",
                columns: new[] { "Priority", "PlannedStart", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Missions_Priority_PlannedStart_CreatedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Objective",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PriorityDefectsJson",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "EmergencyReason",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsImmediate",
                table: "Missions");
        }
    }
}
