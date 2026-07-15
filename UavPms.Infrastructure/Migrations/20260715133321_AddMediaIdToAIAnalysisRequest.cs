using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaIdToAIAnalysisRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
