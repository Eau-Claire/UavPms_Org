using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewFieldsToEmergencyAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "UAVs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EmergencyAlerts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "EmergencyAlerts",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreMissionAssessments_ProposedBoundary",
                table: "PreMissionAssessments",
                column: "ProposedBoundary")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_EmergencyAlerts_ReviewedByUserId",
                table: "EmergencyAlerts",
                column: "ReviewedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_EmergencyAlerts_Users_ReviewedByUserId",
                table: "EmergencyAlerts",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EmergencyAlerts_Users_ReviewedByUserId",
                table: "EmergencyAlerts");

            migrationBuilder.DropIndex(
                name: "IX_PreMissionAssessments_ProposedBoundary",
                table: "PreMissionAssessments");

            migrationBuilder.DropIndex(
                name: "IX_EmergencyAlerts_ReviewedByUserId",
                table: "EmergencyAlerts");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "UAVs");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EmergencyAlerts");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "EmergencyAlerts");
        }
    }
}
