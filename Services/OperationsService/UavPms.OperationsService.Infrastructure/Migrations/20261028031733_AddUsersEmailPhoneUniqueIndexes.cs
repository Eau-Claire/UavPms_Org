using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersEmailPhoneUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IX_Users_Email is already in the model (AlignSchemaWithErdV2_2 created it only when the data had
            // no duplicates, which was not the case on production). Fail loudly instead of skipping, so the
            // data is cleaned first with the "Cleanup Duplicate Users" workflow (scripts/db/cleanup_duplicate_users.sql).
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Users" GROUP BY "Email" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Users has duplicate Email values: run the "Cleanup Duplicate Users" workflow (scripts/db/cleanup_duplicate_users.sql) before this migration.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "Users" WHERE "Phone" <> '' GROUP BY "Phone" HAVING count(*) > 1) THEN
                        RAISE EXCEPTION 'Users has duplicate Phone values: run the "Cleanup Duplicate Users" workflow (scripts/db/cleanup_duplicate_users.sql) before this migration.';
                    END IF;
                END $$;
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email" ON "Users" ("Email");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Phone" ON "Users" ("Phone") WHERE "Phone" <> '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // IX_Users_Email belongs to the model since ERD v2.2, so only the phone index is removed.
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Users_Phone";""");
        }
    }
}
