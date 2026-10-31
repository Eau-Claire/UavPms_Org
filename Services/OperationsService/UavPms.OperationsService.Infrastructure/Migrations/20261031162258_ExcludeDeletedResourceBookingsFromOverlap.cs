using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <summary>
    /// BR-19: a booking conflict is an Active, not deleted booking that overlaps [start, end). The exclusion
    /// constraints of AddResourceBookingExclusionConstraints also counted soft-deleted rows, which the application
    /// does not see. Production was hand-patched, so every step is idempotent (IF EXISTS / IF NOT EXISTS).
    /// Steps: (1) cancel or release Active bookings leaked by deleted, cancelled or completed missions;
    /// (2) stop with the list of offenders if Active, not deleted bookings still overlap; (3) recreate both
    /// constraints with NOT "IsDeleted"; (4) add the MF02 mission settings (BR-09).
    /// </summary>
    public partial class ExcludeDeletedResourceBookingsFromOverlap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // 1. Bookings must end with their mission (BR-33, BR-45); a soft-deleted mission keeps none.
            migrationBuilder.Sql("""
                UPDATE "ResourceBookings" b
                SET "Status" = 'Cancelled', "UpdatedAt" = now()
                FROM "Missions" m
                WHERE b."MissionId" = m."Id" AND b."Status" = 'Active' AND NOT b."IsDeleted"
                  AND (m."IsDeleted" OR m."Status" = 'Cancelled');

                UPDATE "ResourceBookings" b
                SET "Status" = 'Released', "UpdatedAt" = now()
                FROM "Missions" m
                WHERE b."MissionId" = m."Id" AND b."Status" = 'Active' AND NOT b."IsDeleted"
                  AND NOT m."IsDeleted" AND m."Status" = 'Completed';
                """);

            // 2. The new constraints must hold: list every overlapping pair instead of failing on the first ALTER.
            migrationBuilder.Sql("""
                DO $$
                DECLARE offenders text;
                BEGIN
                    SELECT string_agg(format('%s %s: booking %s (mission %s, %s - %s) overlaps booking %s (mission %s, %s - %s)',
                                             o.kind, o.resource, o.a_id, o.a_mission, o.a_start, o.a_end, o.b_id, o.b_mission, o.b_start, o.b_end), '; ')
                    INTO offenders
                    FROM (
                        SELECT 'UAV' AS kind, a."DroneId" AS resource,
                               a."Id" AS a_id, a."MissionId" AS a_mission, a."StartAt" AS a_start, a."EndAt" AS a_end,
                               b."Id" AS b_id, b."MissionId" AS b_mission, b."StartAt" AS b_start, b."EndAt" AS b_end
                        FROM "ResourceBookings" a
                        JOIN "ResourceBookings" b ON b."DroneId" = a."DroneId" AND b."Id" > a."Id"
                         AND tstzrange(a."StartAt", a."EndAt") && tstzrange(b."StartAt", b."EndAt")
                        WHERE a."DroneId" IS NOT NULL
                          AND a."Status" = 'Active' AND b."Status" = 'Active' AND NOT a."IsDeleted" AND NOT b."IsDeleted"
                        UNION ALL
                        SELECT 'User', a."UserId",
                               a."Id", a."MissionId", a."StartAt", a."EndAt",
                               b."Id", b."MissionId", b."StartAt", b."EndAt"
                        FROM "ResourceBookings" a
                        JOIN "ResourceBookings" b ON b."UserId" = a."UserId" AND b."Id" > a."Id"
                         AND tstzrange(a."StartAt", a."EndAt") && tstzrange(b."StartAt", b."EndAt")
                        WHERE a."UserId" IS NOT NULL
                          AND a."Status" = 'Active' AND b."Status" = 'Active' AND NOT a."IsDeleted" AND NOT b."IsDeleted"
                        LIMIT 50
                    ) o;
                    IF offenders IS NOT NULL THEN
                        RAISE EXCEPTION 'Overlapping Active resource bookings must be resolved before the exclusion constraints are recreated: %', offenders;
                    END IF;
                END $$;
                """);

            // 3. Same constraints, now ignoring soft-deleted rows. [start, end) ranges: touching windows are allowed.
            migrationBuilder.Sql("""
                ALTER TABLE "ResourceBookings" DROP CONSTRAINT IF EXISTS "no_overlapping_drone_bookings";
                ALTER TABLE "ResourceBookings" DROP CONSTRAINT IF EXISTS "no_overlapping_user_bookings";

                ALTER TABLE "ResourceBookings" ADD CONSTRAINT "no_overlapping_drone_bookings"
                    EXCLUDE USING gist ("DroneId" WITH =, tstzrange("StartAt", "EndAt") WITH &&)
                    WHERE ("Status" = 'Active' AND "DroneId" IS NOT NULL AND NOT "IsDeleted");

                ALTER TABLE "ResourceBookings" ADD CONSTRAINT "no_overlapping_user_bookings"
                    EXCLUDE USING gist ("UserId" WITH =, tstzrange("StartAt", "EndAt") WITH &&)
                    WHERE ("Status" = 'Active' AND "UserId" IS NOT NULL AND NOT "IsDeleted");
                """);

            // 4. BR-09 settings used by MF02/MF03 (defaults also apply when the rows are missing).
            migrationBuilder.Sql("""
                INSERT INTO "SystemSettings" ("Id", "Key", "Value", "Description", "Category", "DataType", "CreatedAt", "IsDeleted")
                VALUES
                    (gen_random_uuid(), 'Mission:ConfirmationDeadlineHours', '24', 'Default confirmation deadline after dispatch, in hours; capped at the planned start (BR-31)', 'Mission', 'Int', now(), false),
                    (gen_random_uuid(), 'Mission:StartToleranceMinutes', '120', 'How many minutes before the planned start a mission may be started (BR-38)', 'Mission', 'Int', now(), false)
                ON CONFLICT ("Key") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "SystemSettings" WHERE "Key" IN ('Mission:ConfirmationDeadlineHours', 'Mission:StartToleranceMinutes');

                -- The old constraints count soft-deleted rows; those bookings are invisible to the application anyway.
                UPDATE "ResourceBookings" SET "Status" = 'Cancelled', "UpdatedAt" = now() WHERE "Status" = 'Active' AND "IsDeleted";

                ALTER TABLE "ResourceBookings" DROP CONSTRAINT IF EXISTS "no_overlapping_drone_bookings";
                ALTER TABLE "ResourceBookings" DROP CONSTRAINT IF EXISTS "no_overlapping_user_bookings";

                ALTER TABLE "ResourceBookings" ADD CONSTRAINT "no_overlapping_drone_bookings"
                    EXCLUDE USING gist ("DroneId" WITH =, tstzrange("StartAt", "EndAt") WITH &&)
                    WHERE ("Status" = 'Active' AND "DroneId" IS NOT NULL);

                ALTER TABLE "ResourceBookings" ADD CONSTRAINT "no_overlapping_user_bookings"
                    EXCLUDE USING gist ("UserId" WITH =, tstzrange("StartAt", "EndAt") WITH &&)
                    WHERE ("Status" = 'Active' AND "UserId" IS NOT NULL);
                """);
        }
    }
}
