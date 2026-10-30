using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan item 3.2: AK_AIAnalysisRuns_Id_MediaId and the composite FK
    /// FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId_MediaId were created in raw SQL by
    /// AlignSchemaWithErdV2_2; the EF model now declares them, so this migration only adds what is missing
    /// (the FK index) and leaves existing constraints alone.
    /// </summary>
    public partial class ModelCompositeAnalysisRunMediaKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'AK_AIAnalysisRuns_Id_MediaId') THEN
                        ALTER TABLE "AIAnalysisRuns" ADD CONSTRAINT "AK_AIAnalysisRuns_Id_MediaId" UNIQUE ("Id", "MediaId");
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId_MediaId') THEN
                        ALTER TABLE "DetectedAnomalies" ADD CONSTRAINT "FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId_MediaId"
                            FOREIGN KEY ("AnalysisRunId", "MediaId") REFERENCES "AIAnalysisRuns" ("Id", "MediaId");
                    END IF;
                END $$;
                CREATE INDEX IF NOT EXISTS "IX_DetectedAnomalies_AnalysisRunId_MediaId"
                    ON "DetectedAnomalies" ("AnalysisRunId", "MediaId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The key and FK belong to AlignSchemaWithErdV2_2; only the index is new here.
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_DetectedAnomalies_AnalysisRunId_MediaId";""");
        }
    }
}
