using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Moq;
using Npgsql;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Migration ExcludeDeletedResourceBookingsFromOverlap on a database that already holds data, as on production:
/// leaked bookings are repaired, and real overlaps stop the migration with the offending rows listed.
/// </summary>
[Collection(PostgresCollection.Name)]
public class ResourceBookingMigrationTests
{
    private const string Previous = "20261031055707_WidenAuditLogActionTypeLength";
    private const string Target = "20261031162258_ExcludeDeletedResourceBookingsFromOverlap";

    private readonly PostgresFixture _pg;

    public ResourceBookingMigrationTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task Migration_RepairsLeakedBookings_RecreatesTheConstraints_AndRefusesRealOverlaps()
    {
        if (!_pg.Available) return;
        var dbName = $"mig_{Guid.NewGuid():N}"[..20];
        await using (var admin = _pg.CreateContext())
        {
            await admin.Database.ExecuteSqlRawAsync($"CREATE DATABASE {dbName}");
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_pg.ConnectionString) { Database = dbName }.ConnectionString;
        try
        {
            await using var db = new ApplicationDbContext(
                new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString, o => o.UseNetTopologySuite()).Options,
                Mock.Of<ICurrentUserServices>());
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(Previous);

            // Leaks: Active bookings of a deleted, a cancelled and a completed mission, plus one live booking.
            var managerId = Guid.NewGuid();
            var regionId = Guid.NewGuid();
            var start = DateTime.UtcNow.AddHours(1);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
                VALUES ({managerId}, 'Manager', 'm@mig.test', '', 'Active', true, 'x', now(), false);
                INSERT INTO "Regions" ("Id", "Code", "Name", "Type", "CreatedAt", "IsDeleted") VALUES ({regionId}, 'R-MIG', 'Region', 'Province', now(), false);
                """);
            var deleted = await AddMissionAsync(db, managerId, regionId, "Draft", isDeleted: true);
            var cancelled = await AddMissionAsync(db, managerId, regionId, "Cancelled");
            var completed = await AddMissionAsync(db, managerId, regionId, "Completed");
            var live = await AddMissionAsync(db, managerId, regionId, "Assigned");
            var bookings = new Dictionary<Guid, Guid>();
            foreach (var mission in new[] { deleted, cancelled, completed, live })
            {
                bookings[mission] = await AddDroneBookingAsync(db, mission, start, start.AddHours(2));
            }

            await migrator.MigrateAsync(Target);

            async Task<string> StatusOf(Guid missionId) => await db.Database
                .SqlQuery<string>($"""SELECT "Status" AS "Value" FROM "ResourceBookings" WHERE "Id" = {bookings[missionId]}""").SingleAsync();
            (await StatusOf(deleted)).Should().Be("Cancelled");
            (await StatusOf(cancelled)).Should().Be("Cancelled");
            (await StatusOf(completed)).Should().Be("Released");
            (await StatusOf(live)).Should().Be("Active");

            var definition = await db.Database.SqlQuery<string>($"""
                SELECT pg_get_constraintdef(oid) AS "Value" FROM pg_constraint WHERE conname = 'no_overlapping_drone_bookings'
                """).SingleAsync();
            definition.Should().Contain("IsDeleted");
            (await db.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM "SystemSettings" WHERE "Key" LIKE 'Mission:%'""").SingleAsync()).Should().Be(2);

            // A soft-deleted Active booking no longer blocks the window.
            var droneId = await db.Database.SqlQuery<Guid>($"""SELECT "DroneId" AS "Value" FROM "ResourceBookings" WHERE "Id" = {bookings[live]}""").SingleAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""UPDATE "ResourceBookings" SET "IsDeleted" = true WHERE "Id" = {bookings[live]}""");
            var other = await AddMissionAsync(db, managerId, regionId, "Assigned");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ResourceBookings" ("Id", "MissionId", "DroneId", "StartAt", "EndAt", "Status", "CreatedAt", "IsDeleted")
                VALUES ({Guid.NewGuid()}, {other}, {droneId}, {start}, {start.AddHours(2)}, 'Active', now(), false)
                """);

            // Production drift: no constraint at all and two live overlapping bookings. The migration must stop and say which.
            await migrator.MigrateAsync(Previous);
            await db.Database.ExecuteSqlRawAsync("""ALTER TABLE "ResourceBookings" DROP CONSTRAINT IF EXISTS "no_overlapping_drone_bookings" """);
            var clash = await AddMissionAsync(db, managerId, regionId, "Assigned");
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ResourceBookings" ("Id", "MissionId", "DroneId", "StartAt", "EndAt", "Status", "CreatedAt", "IsDeleted")
                VALUES ({Guid.NewGuid()}, {clash}, {droneId}, {start.AddHours(1)}, {start.AddHours(3)}, 'Active', now(), false)
                """);

            var migrate = () => migrator.MigrateAsync(Target);
            (await migrate.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("Overlapping Active resource bookings").And.Contain(droneId.ToString());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = _pg.CreateContext();
            await admin.Database.ExecuteSqlRawAsync($"DROP DATABASE IF EXISTS {dbName} WITH (FORCE)");
        }
    }

    private static async Task<Guid> AddMissionAsync(ApplicationDbContext db, Guid managerId, Guid regionId, string status, bool isDeleted = false)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Missions" ("Id", "MissionCode", "Title", "ManagerId", "RegionId", "Status", "Description", "MissionType", "Version", "CreatedAt", "IsDeleted")
            VALUES ({id}, {"M-" + id.ToString("N")[..10]}, 'Mission', {managerId}, {regionId}, {status}, '', 'AdHoc', 1, now(), {isDeleted})
            """);
        return id;
    }

    private static async Task<Guid> AddDroneBookingAsync(ApplicationDbContext db, Guid missionId, DateTime start, DateTime end)
    {
        var droneId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UAVs" ("Id", "UavCode", "Name", "Model", "Category", "SerialNumber", "FlightStatus", "OperationalStatus", "TechnicalHealth", "FlightEnduranceMinutes", "OperationalRangeKm", "BatteryLevel", "Version", "CreatedAt", "IsDeleted")
            VALUES ({droneId}, {"U-" + droneId.ToString("N")[..10]}, 'Drone', 'M300', 'Hexacopter', {droneId.ToString("N")}, 'Idle', 'Available', 'Healthy', 30, 10, 100, 1, now(), false);
            INSERT INTO "ResourceBookings" ("Id", "MissionId", "DroneId", "StartAt", "EndAt", "Status", "CreatedAt", "IsDeleted")
            VALUES ({bookingId}, {missionId}, {droneId}, {start}, {end}, 'Active', now(), false);
            """);
        return bookingId;
    }
}
