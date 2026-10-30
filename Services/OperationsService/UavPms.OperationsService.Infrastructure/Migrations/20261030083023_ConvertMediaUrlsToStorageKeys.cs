using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UavPms.OperationsService.Infrastructure.Migrations
{
    /// <summary>
    /// Plan item 2.8: Supabase public URLs in the media/proof/crop columns become storage keys
    /// ("bucket/object"). The services rebuild the URL from Supabase:Url on read (StorageKeys + EF value
    /// converter), so the configured Supabase host must be the host these URLs point to.
    /// Local paths (/images/...) and URLs of other hosts are left unchanged.
    /// </summary>
    public partial class ConvertMediaUrlsToStorageKeys : Migration
    {
        private const string PublicUrlPattern = "^https?://[^/]+/storage/v1/object/public/";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Keys only carry bucket/object; a second Supabase host could not be rebuilt from one Supabase:Url.
            migrationBuilder.Sql($$"""
                DO $$
                DECLARE hosts text;
                BEGIN
                    SELECT string_agg(DISTINCT substring(u FROM '^https?://([^/]+)/'), ', ') INTO hosts
                    FROM (
                        SELECT "FileUrl" AS u FROM "InspectionMedia"
                        UNION ALL SELECT "FileUrl" FROM "MaintenanceProofs"
                        UNION ALL SELECT "ImageUrl" FROM "DetectedAnomalies"
                        UNION ALL SELECT "CropUrl" FROM "DetectedAnomalies"
                    ) urls
                    WHERE u ~ '{{PublicUrlPattern}}';

                    IF hosts LIKE '%,%' THEN
                        RAISE EXCEPTION 'Supabase public URLs point to several hosts (%); keys can be rebuilt for one Supabase:Url only.', hosts;
                    END IF;
                    RAISE NOTICE 'Converting Supabase public URLs of host % to storage keys. Supabase:Url must point to this host.', COALESCE(hosts, '(none)');
                END $$;
                """);

            foreach (var (table, column) in new[]
                     {
                         ("InspectionMedia", "FileUrl"),
                         ("MaintenanceProofs", "FileUrl"),
                         ("DetectedAnomalies", "ImageUrl"),
                         ("DetectedAnomalies", "CropUrl"),
                     })
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" SET "{column}" = regexp_replace("{column}", '{PublicUrlPattern}', '')
                    WHERE "{column}" ~ '{PublicUrlPattern}';
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible in SQL (the host lives in configuration); the services read keys and URLs alike,
            // so rolling back the code needs the pre-migration backup instead.
        }
    }
}
