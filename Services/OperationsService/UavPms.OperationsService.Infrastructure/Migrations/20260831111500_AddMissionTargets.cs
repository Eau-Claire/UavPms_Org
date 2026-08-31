using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissionTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE TABLE IF NOT EXISTS public."MissionTargets" (
                    "Id" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "MissionId" uuid NOT NULL,
                    "TowerId" uuid NOT NULL,
                    "Sequence" integer NOT NULL DEFAULT 0,
                    "Status" text NOT NULL DEFAULT 'Pending',
                    "Notes" text,
                    CONSTRAINT "MissionTargets_pkey" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_MissionTargets_Missions_MissionId"
                        FOREIGN KEY ("MissionId") REFERENCES public."Missions"("Id") ON DELETE CASCADE,
                    CONSTRAINT "FK_MissionTargets_Towers_TowerId"
                        FOREIGN KEY ("TowerId") REFERENCES public."Towers"("Id") ON DELETE RESTRICT
                );
                """);

            migrationBuilder.Sql(
                """
                CREATE INDEX IF NOT EXISTS "IX_MissionTargets_TowerId"
                    ON public."MissionTargets" ("TowerId");
                """);

            migrationBuilder.Sql(
                """
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MissionTargets_MissionId_TowerId"
                    ON public."MissionTargets" ("MissionId", "TowerId");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Supabase already contains this operational table, so rollback should not drop data.
        }
    }
}
