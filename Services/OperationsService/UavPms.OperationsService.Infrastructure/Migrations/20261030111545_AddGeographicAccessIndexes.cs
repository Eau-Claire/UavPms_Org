using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan item 3.5: GeographicAccessFilter looks up the current user's active assignments and open tickets on
    /// every GIS-scoped query. The composite indexes replace the single-column FK indexes they start with.
    /// IF [NOT] EXISTS because the production schema was partly hand-patched.
    /// </summary>
    public partial class AddGeographicAccessIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_MissionAssignments_UserId_Status" ON "MissionAssignments" ("UserId", "Status");
                CREATE INDEX IF NOT EXISTS "IX_MaintenanceTickets_TechnicianId_Status" ON "MaintenanceTickets" ("TechnicianId", "Status");
                DROP INDEX IF EXISTS "IX_MissionAssignments_UserId";
                DROP INDEX IF EXISTS "IX_MaintenanceTickets_TechnicianId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS "IX_MissionAssignments_UserId" ON "MissionAssignments" ("UserId");
                CREATE INDEX IF NOT EXISTS "IX_MaintenanceTickets_TechnicianId" ON "MaintenanceTickets" ("TechnicianId");
                DROP INDEX IF EXISTS "IX_MissionAssignments_UserId_Status";
                DROP INDEX IF EXISTS "IX_MaintenanceTickets_TechnicianId_Status";
                """);
        }
    }
}
