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
            migrationBuilder.Sql("""
                UPDATE \"PreMissionAssessments\"
                SET \"Status\" = CASE UPPER(REPLACE(TRIM(\"Status\"), ' ', ''))
                    WHEN 'CONSUMED' THEN 'COMPLETED'
                    WHEN 'INCOMPLETE' THEN 'NOT_READY'
                    WHEN 'NOTREADY' THEN 'NOT_READY'
                    WHEN 'CANCELED' THEN 'CANCELLED'
                    ELSE UPPER(TRIM(\"Status\"))
                END;
                """);

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
