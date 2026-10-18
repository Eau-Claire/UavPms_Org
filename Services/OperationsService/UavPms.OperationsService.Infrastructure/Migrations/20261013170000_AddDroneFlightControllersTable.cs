using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDroneFlightControllersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DroneFlightControllers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DroneId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlightControllerUid = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    BoardIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FirmwareName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PairedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UnpairedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneFlightControllers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneFlightControllers_UAVs_DroneId",
                        column: x => x.DroneId,
                        principalTable: "UAVs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DroneFlightControllers_FlightControllerUid_IsActive",
                table: "DroneFlightControllers",
                columns: new[] { "FlightControllerUid", "IsActive" },
                filter: "\"IsActive\" AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_DroneFlightControllers_DroneId_IsActive",
                table: "DroneFlightControllers",
                columns: new[] { "DroneId", "IsActive" },
                filter: "\"IsActive\" AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroneFlightControllers");
        }
    }
}
