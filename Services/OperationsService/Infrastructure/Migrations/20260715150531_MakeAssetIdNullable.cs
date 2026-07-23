using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeAssetIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIAnalysisRequests_InspectionMedia_MediaId",
                table: "AIAnalysisRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_AIAnalysisRequests_Missions_MissionId",
                table: "AIAnalysisRequests");

            migrationBuilder.DropIndex(
                name: "IX_AIAnalysisRequests_MediaId",
                table: "AIAnalysisRequests");

            migrationBuilder.DropIndex(
                name: "IX_AIAnalysisRequests_MissionId",
                table: "AIAnalysisRequests");

            migrationBuilder.DropColumn(
                name: "MediaId",
                table: "AIAnalysisRequests");

            migrationBuilder.DropColumn(
                name: "MissionId",
                table: "AIAnalysisRequests");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "InspectionMedia",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "EmergencyAlerts",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "DetectedAnomalies",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "InspectionMedia",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "EmergencyAlerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AssetId",
                table: "DetectedAnomalies",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MediaId",
                table: "AIAnalysisRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MissionId",
                table: "AIAnalysisRequests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisRequests_MediaId",
                table: "AIAnalysisRequests",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisRequests_MissionId",
                table: "AIAnalysisRequests",
                column: "MissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_AIAnalysisRequests_InspectionMedia_MediaId",
                table: "AIAnalysisRequests",
                column: "MediaId",
                principalTable: "InspectionMedia",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AIAnalysisRequests_Missions_MissionId",
                table: "AIAnalysisRequests",
                column: "MissionId",
                principalTable: "Missions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
