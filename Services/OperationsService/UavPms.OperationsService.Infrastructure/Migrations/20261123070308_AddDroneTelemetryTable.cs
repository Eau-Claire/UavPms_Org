using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDroneTelemetryTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastTelemetryAt",
                table: "UAVs",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DroneTelemetry",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UavId = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceTimestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FlightMode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Armed = table.Column<bool>(type: "boolean", nullable: true),
                    Location = table.Column<Point>(type: "geometry(Point,4326)", nullable: true),
                    AltRelativeM = table.Column<double>(type: "double precision", nullable: true),
                    AltMslM = table.Column<double>(type: "double precision", nullable: true),
                    Satellites = table.Column<int>(type: "integer", nullable: true),
                    GpsFixType = table.Column<int>(type: "integer", nullable: true),
                    Hdop = table.Column<double>(type: "double precision", nullable: true),
                    RollDeg = table.Column<double>(type: "double precision", nullable: true),
                    PitchDeg = table.Column<double>(type: "double precision", nullable: true),
                    YawDeg = table.Column<double>(type: "double precision", nullable: true),
                    GroundSpeedMs = table.Column<double>(type: "double precision", nullable: true),
                    AirSpeedMs = table.Column<double>(type: "double precision", nullable: true),
                    ClimbRateMs = table.Column<double>(type: "double precision", nullable: true),
                    HeadingDeg = table.Column<double>(type: "double precision", nullable: true),
                    ThrottlePct = table.Column<double>(type: "double precision", nullable: true),
                    BatteryVoltageV = table.Column<double>(type: "double precision", nullable: true),
                    BatteryCurrentA = table.Column<double>(type: "double precision", nullable: true),
                    BatteryRemainingPct = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneTelemetry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneTelemetry_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DroneTelemetry_UAVs_UavId",
                        column: x => x.UavId,
                        principalTable: "UAVs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DroneTelemetry_MissionId_SourceTimestamp",
                table: "DroneTelemetry",
                columns: new[] { "MissionId", "SourceTimestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_DroneTelemetry_ReceivedAt",
                table: "DroneTelemetry",
                column: "ReceivedAt");

            migrationBuilder.CreateIndex(
                name: "UX_DroneTelemetry_UavId_SourceTimestamp",
                table: "DroneTelemetry",
                columns: new[] { "UavId", "SourceTimestamp" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroneTelemetry");

            migrationBuilder.DropColumn(
                name: "LastTelemetryAt",
                table: "UAVs");
        }
    }
}
