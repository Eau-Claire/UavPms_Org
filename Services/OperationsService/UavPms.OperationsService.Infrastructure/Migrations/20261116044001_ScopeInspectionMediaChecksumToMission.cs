using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeInspectionMediaChecksumToMission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionMedia_Checksum",
                table: "InspectionMedia");

            migrationBuilder.DropIndex(
                name: "IX_InspectionMedia_MissionId",
                table: "InspectionMedia");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionMedia_MissionId_Checksum",
                table: "InspectionMedia",
                columns: new[] { "MissionId", "Checksum" },
                unique: true,
                filter: "\"Checksum\" IS NOT NULL AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionMedia_MissionId_Checksum",
                table: "InspectionMedia");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionMedia_Checksum",
                table: "InspectionMedia",
                column: "Checksum",
                unique: true,
                filter: "\"Checksum\" IS NOT NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_InspectionMedia_MissionId",
                table: "InspectionMedia",
                column: "MissionId");
        }
    }
}
