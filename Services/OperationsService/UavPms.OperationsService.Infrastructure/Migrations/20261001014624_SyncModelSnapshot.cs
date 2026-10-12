using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UavId",
                table: "Missions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "InspectorId",
                table: "Missions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmationDeadline",
                table: "Missions",
                type: "timestamp with time zone",
                nullable: true);

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

            migrationBuilder.AddColumn<bool>(
                name: "IsOverdueNotified",
                table: "Missions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ManagerInstructions",
                table: "Missions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Objective",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "PeriodicInspection");

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Missions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PriorityDefectsJson",
                table: "Missions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "MissionCommunicationLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MissionCommunicationLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MissionCommunicationLogs_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MissionCommunicationLogs_Users_SenderId",
                        column: x => x.SenderId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Missions_Priority_PlannedStart_CreatedAt",
                table: "Missions",
                columns: new[] { "Priority", "PlannedStart", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MissionCommunicationLogs_CreatedAt",
                table: "MissionCommunicationLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCommunicationLogs_MissionId",
                table: "MissionCommunicationLogs",
                column: "MissionId");

            migrationBuilder.CreateIndex(
                name: "IX_MissionCommunicationLogs_SenderId",
                table: "MissionCommunicationLogs",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MissionCommunicationLogs");

            migrationBuilder.DropIndex(
                name: "IX_Missions_Priority_PlannedStart_CreatedAt",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ConfirmationDeadline",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "EmergencyReason",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsImmediate",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "IsOverdueNotified",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "ManagerInstructions",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Objective",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Missions");

            migrationBuilder.DropColumn(
                name: "PriorityDefectsJson",
                table: "Missions");

            migrationBuilder.AlterColumn<Guid>(
                name: "UavId",
                table: "Missions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "InspectorId",
                table: "Missions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
