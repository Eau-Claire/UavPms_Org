using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeMaintenanceTicketTechnicianNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TechnicianId",
                table: "MaintenanceTickets",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // Tickets auto-created before this fix may carry Guid.Empty as "unassigned".
            migrationBuilder.Sql(
                "UPDATE \"MaintenanceTickets\" SET \"TechnicianId\" = NULL " +
                "WHERE \"TechnicianId\" = '00000000-0000-0000-0000-000000000000';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NOT NULL + FK to Users: fall back to the ticket's manager for unassigned tickets.
            migrationBuilder.Sql(
                "UPDATE \"MaintenanceTickets\" SET \"TechnicianId\" = \"ManagerId\" WHERE \"TechnicianId\" IS NULL;");

            migrationBuilder.AlterColumn<Guid>(
                name: "TechnicianId",
                table: "MaintenanceTickets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
