using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorAssessmentStatusCompletedAndNotReady : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Migrate legacy data
            migrationBuilder.Sql("UPDATE \"PreMissionAssessments\" SET \"Status\" = 'COMPLETED' WHERE \"Status\" IN ('CONSUMED', 'Consumed');");
            migrationBuilder.Sql("UPDATE \"PreMissionAssessments\" SET \"Status\" = 'NOT_READY' WHERE \"Status\" IN ('INCOMPLETE', 'Incomplete', 'NOTREADY', 'NotReady');");
            migrationBuilder.Sql("UPDATE \"PreMissionAssessments\" SET \"Status\" = UPPER(\"Status\");");

            // 2. Add Check Constraint
            migrationBuilder.AddCheckConstraint(
                name: "CK_PreMissionAssessments_Status",
                table: "PreMissionAssessments",
                sql: "\"Status\" IN ('DRAFT', 'EVALUATING', 'READY', 'NOT_READY', 'EXPIRED', 'COMPLETED', 'CANCELLED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_PreMissionAssessments_Status",
                table: "PreMissionAssessments");
        }
    }
}
