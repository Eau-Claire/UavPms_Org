using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAllEntityStatusCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE "Missions" DROP CONSTRAINT IF EXISTS "CK_Missions_Status";
                ALTER TABLE "MaintenanceTickets" DROP CONSTRAINT IF EXISTS "CK_MaintenanceTickets_Status";
                ALTER TABLE "EmergencyAlerts" DROP CONSTRAINT IF EXISTS "CK_EmergencyAlerts_Status";
                ALTER TABLE "DetectedAnomalies" DROP CONSTRAINT IF EXISTS "CK_DetectedAnomalies_ValidationStatus";
                ALTER TABLE "AIAnalysisRuns" DROP CONSTRAINT IF EXISTS "CK_AIAnalysisRuns_Status";

                UPDATE "Missions"
                SET "Status" = CASE UPPER(REPLACE(REPLACE(TRIM("Status"), '_', ''), ' ', ''))
                    WHEN 'DRAFT' THEN 'Draft'
                    WHEN 'PENDING' THEN 'Draft'
                    WHEN 'PENDINGACCEPTANCE' THEN 'PendingAcceptance'
                    WHEN 'PENDINGCONFIRMATION' THEN 'PendingAcceptance'
                    WHEN 'POSTPONED' THEN 'Postponed'
                    WHEN 'CONFIRMED' THEN 'Confirmed'
                    WHEN 'ASSIGNED' THEN 'Confirmed'
                    WHEN 'PREPARING' THEN 'Confirmed'
                    WHEN 'READY' THEN 'Ready'
                    WHEN 'INPROGRESS' THEN 'InProgress'
                    WHEN 'EXECUTING' THEN 'InProgress'
                    WHEN 'SUSPENDED' THEN 'Suspended'
                    WHEN 'COMPLETED' THEN 'Completed'
                    WHEN 'CANCELLED' THEN 'Cancelled'
                    WHEN 'CANCELED' THEN 'Cancelled'
                    ELSE "Status"
                END
                WHERE "Status" NOT IN ('Draft', 'PendingAcceptance', 'Postponed', 'Confirmed', 'Ready', 'InProgress', 'Suspended', 'Completed', 'Cancelled');

                UPDATE "MaintenanceTickets"
                SET "Status" = CASE UPPER(REPLACE(REPLACE(TRIM("Status"), '_', ''), ' ', ''))
                    WHEN 'OPEN' THEN 'Open'
                    WHEN 'ASSIGNED' THEN 'Assigned'
                    WHEN 'INPROGRESS' THEN 'InProgress'
                    WHEN 'PENDINGVERIFICATION' THEN 'PendingVerification'
                    WHEN 'CLOSED' THEN 'Closed'
                    WHEN 'RESOLVED' THEN 'Closed'
                    WHEN 'CANCELLED' THEN 'Cancelled'
                    WHEN 'CANCELED' THEN 'Cancelled'
                    ELSE "Status"
                END
                WHERE "Status" NOT IN ('Open', 'Assigned', 'InProgress', 'PendingVerification', 'Closed', 'Cancelled');

                UPDATE "EmergencyAlerts"
                SET "Status" = CASE UPPER(REPLACE(REPLACE(TRIM("Status"), '_', ''), ' ', ''))
                    WHEN 'OPEN' THEN 'Open'
                    WHEN 'ACTIVE' THEN 'Open'
                    WHEN 'ACKNOWLEDGED' THEN 'Acknowledged'
                    WHEN 'ESCALATED' THEN 'Acknowledged'
                    WHEN 'RESOLVED' THEN 'Resolved'
                    WHEN 'CONFIRMED' THEN 'Resolved'
                    WHEN 'DISMISSED' THEN 'Resolved'
                    ELSE "Status"
                END
                WHERE "Status" NOT IN ('Open', 'Acknowledged', 'Resolved');

                UPDATE "DetectedAnomalies"
                SET "ValidationStatus" = CASE UPPER(REPLACE(REPLACE(TRIM("ValidationStatus"), '_', ''), ' ', ''))
                    WHEN 'PENDING' THEN 'Pending'
                    WHEN 'PENDINGREVIEW' THEN 'Pending'
                    WHEN 'INREVIEW' THEN 'InReview'
                    WHEN 'CONFIRMED' THEN 'Confirmed'
                    WHEN 'APPROVED' THEN 'Confirmed'
                    WHEN 'REJECTED' THEN 'Rejected'
                    WHEN 'RESOLVED' THEN 'Resolved'
                    ELSE "ValidationStatus"
                END
                WHERE "ValidationStatus" NOT IN ('Pending', 'InReview', 'Confirmed', 'Rejected', 'Resolved');

                UPDATE "AIAnalysisRuns"
                SET "Status" = CASE UPPER(REPLACE(REPLACE(TRIM("Status"), '_', ''), ' ', ''))
                    WHEN 'QUEUED' THEN 'Queued'
                    WHEN 'PENDING' THEN 'Queued'
                    WHEN 'PROCESSING' THEN 'Processing'
                    WHEN 'COMPLETED' THEN 'Completed'
                    WHEN 'FAILED' THEN 'Failed'
                    ELSE "Status"
                END
                WHERE "Status" NOT IN ('Queued', 'Processing', 'Completed', 'Failed');
                """);

            migrationBuilder.DropIndex(
                name: "IX_AIAnalysisRuns_ActiveLogicalAnalysis",
                table: "AIAnalysisRuns");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Missions_Status",
                table: "Missions",
                sql: "\"Status\" IN ('Draft', 'PendingAcceptance', 'Postponed', 'Confirmed', 'Ready', 'InProgress', 'Suspended', 'Completed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceTickets_Status",
                table: "MaintenanceTickets",
                sql: "\"Status\" IN ('Open', 'Assigned', 'InProgress', 'PendingVerification', 'Closed', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EmergencyAlerts_Status",
                table: "EmergencyAlerts",
                sql: "\"Status\" IN ('Open', 'Acknowledged', 'Resolved')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DetectedAnomalies_ValidationStatus",
                table: "DetectedAnomalies",
                sql: "\"ValidationStatus\" IN ('Pending', 'InReview', 'Confirmed', 'Rejected', 'Resolved')");

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisRuns_ActiveLogicalAnalysis",
                table: "AIAnalysisRuns",
                columns: new[] { "MediaId", "AnalysisType", "ModelName" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('Queued', 'Processing')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AIAnalysisRuns_Status",
                table: "AIAnalysisRuns",
                sql: "\"Status\" IN ('Queued', 'Processing', 'Completed', 'Failed')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Missions_Status",
                table: "Missions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceTickets_Status",
                table: "MaintenanceTickets");

            migrationBuilder.DropCheckConstraint(
                name: "CK_EmergencyAlerts_Status",
                table: "EmergencyAlerts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DetectedAnomalies_ValidationStatus",
                table: "DetectedAnomalies");

            migrationBuilder.DropIndex(
                name: "IX_AIAnalysisRuns_ActiveLogicalAnalysis",
                table: "AIAnalysisRuns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AIAnalysisRuns_Status",
                table: "AIAnalysisRuns");

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisRuns_ActiveLogicalAnalysis",
                table: "AIAnalysisRuns",
                columns: new[] { "MediaId", "AnalysisType", "ModelName" },
                unique: true,
                filter: "\"IsDeleted\" = false AND \"Status\" IN ('Pending', 'Processing')");
        }
    }
}
