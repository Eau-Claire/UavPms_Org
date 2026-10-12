using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddResourceBookingExclusionConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'no_overlapping_drone_bookings'
                    ) THEN
                        ALTER TABLE ""ResourceBookings"" ADD CONSTRAINT ""no_overlapping_drone_bookings""
                        EXCLUDE USING gist (
                            ""DroneId"" WITH =,
                            tstzrange(""StartAt"", ""EndAt"") WITH &&
                        ) WHERE (""Status"" = 'Active' AND ""DroneId"" IS NOT NULL);
                    END IF;

                    IF NOT EXISTS (
                        SELECT 1 FROM pg_constraint WHERE conname = 'no_overlapping_user_bookings'
                    ) THEN
                        ALTER TABLE ""ResourceBookings"" ADD CONSTRAINT ""no_overlapping_user_bookings""
                        EXCLUDE USING gist (
                            ""UserId"" WITH =,
                            tstzrange(""StartAt"", ""EndAt"") WITH &&
                        ) WHERE (""Status"" = 'Active' AND ""UserId"" IS NOT NULL);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE ""ResourceBookings"" DROP CONSTRAINT IF EXISTS ""no_overlapping_drone_bookings"";
                ALTER TABLE ""ResourceBookings"" DROP CONSTRAINT IF EXISTS ""no_overlapping_user_bookings"";
            ");
        }
    }
}
