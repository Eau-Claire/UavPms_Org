using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeCanonicalStatuses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreMissionAssessments_Status",
                table: "PreMissionAssessments");

            migrationBuilder.Sql("""
                UPDATE "PreMissionAssessments" SET "Status" = 'Evaluating' WHERE "Status" IN ('DRAFT', 'EVALUATING', 'Draft');
                UPDATE "PreMissionAssessments" SET "Status" = 'Ready' WHERE "Status" = 'READY';
                UPDATE "PreMissionAssessments" SET "Status" = 'NotReady' WHERE "Status" IN ('NOT_READY', 'INCOMPLETE', 'Incomplete');
                UPDATE "PreMissionAssessments" SET "Status" = 'Expired' WHERE "Status" = 'EXPIRED';
                UPDATE "PreMissionAssessments" SET "Status" = 'Completed' WHERE "Status" IN ('COMPLETED', 'CONSUMED', 'Consumed');
                UPDATE "PreMissionAssessments" SET "Status" = 'Cancelled' WHERE "Status" = 'CANCELLED';
                UPDATE "Missions" SET "Status" = 'Draft' WHERE "Status" = 'Pending';
                UPDATE "Missions" SET "Status" = 'Confirmed' WHERE "Status" IN ('Assigned', 'Preparing');
                UPDATE "Missions" SET "Status" = 'InProgress' WHERE "Status" = 'Executing';
                UPDATE "Missions" SET "Status" = 'PendingAcceptance' WHERE "Status" = 'PendingConfirmation';
                UPDATE "MaintenanceTickets" SET "Status" = 'Closed' WHERE "Status" = 'Resolved';
                UPDATE "AIAnalysisRuns" SET "Status" = 'Queued' WHERE "Status" = 'Pending';
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreMissionAssessments_Status",
                table: "PreMissionAssessments",
                sql: "\"Status\" IN ('Evaluating', 'Ready', 'NotReady', 'Expired', 'Completed', 'Cancelled')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreMissionAssessments_Status",
                table: "PreMissionAssessments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PreMissionAssessments_Status",
                table: "PreMissionAssessments",
                sql: "\"Status\" IN ('DRAFT', 'EVALUATING', 'READY', 'NOT_READY', 'EXPIRED', 'COMPLETED', 'CANCELLED')");
        }
    }
}
