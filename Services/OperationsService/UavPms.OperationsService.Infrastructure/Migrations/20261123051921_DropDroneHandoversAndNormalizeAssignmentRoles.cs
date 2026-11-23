using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropDroneHandoversAndNormalizeAssignmentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DroneHandovers");

            migrationBuilder.Sql("""
                UPDATE "MissionAssignments"
                SET "AssignmentRole" = 'INSPECTOR'
                WHERE UPPER(TRIM("AssignmentRole")) = 'PILOT';

                UPDATE "MissionAssignments"
                SET "AssignmentRole" = 'TECHNICIAN'
                WHERE UPPER(REPLACE(TRIM("AssignmentRole"), '_', '')) = 'MAINTENANCETECHNICIAN';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DroneHandovers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DroneId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandedOverBy = table.Column<Guid>(type: "uuid", nullable: false),
                    MissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceivedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Condition = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InitialChecklist = table.Column<string>(type: "jsonb", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReturnChecklist = table.Column<string>(type: "jsonb", nullable: true),
                    ReturnedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DroneHandovers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DroneHandovers_Missions_MissionId",
                        column: x => x.MissionId,
                        principalTable: "Missions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DroneHandovers_UAVs_DroneId",
                        column: x => x.DroneId,
                        principalTable: "UAVs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DroneHandovers_Users_HandedOverBy",
                        column: x => x.HandedOverBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DroneHandovers_Users_ReceivedBy",
                        column: x => x.ReceivedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_DroneId",
                table: "DroneHandovers",
                column: "DroneId");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_HandedOverBy",
                table: "DroneHandovers",
                column: "HandedOverBy");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_MissionId_DroneId",
                table: "DroneHandovers",
                columns: new[] { "MissionId", "DroneId" },
                unique: true,
                filter: "\"ReturnedAt\" IS NULL AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_DroneHandovers_ReceivedBy",
                table: "DroneHandovers",
                column: "ReceivedBy");
        }
    }
}
