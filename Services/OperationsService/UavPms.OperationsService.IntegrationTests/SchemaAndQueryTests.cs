using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using Npgsql;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence.Configurations;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using UavPms.Shared.Contracts.Settings;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Migrations, ERD v2.2 constraints and the LINQ queries that InMemory tests cannot prove, on real PostgreSQL/PostGIS.
/// </summary>
[Collection(PostgresCollection.Name)]
public class SchemaAndQueryTests
{
    private readonly PostgresFixture _pg;

    public SchemaAndQueryTests(PostgresFixture pg) => _pg = pg;

    private static Mock<ICurrentUserServices> User(Guid id, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(id);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock;
    }

    /// <summary>A region/tower/mission/media/run/anomaly/ticket graph with unique codes per call.</summary>
    private async Task<Seed> SeedAsync()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        await using var db = _pg.CreateContext();
        var managerId = await InsertUserAsync(db, $"m-{tag}@it.test");
        var inspectorId = await InsertUserAsync(db, $"i-{tag}@it.test");
        var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = "Region", Type = "Province" };
        var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}", Geom = new Point(105.85, 21.03) { SRID = 4326 } };
        var mission = new Mission { Id = Guid.NewGuid(), MissionCode = $"M-{tag}", Title = "Mission", ManagerId = managerId, Status = MissionStatus.InProgress, RegionId = region.Id };
        var category = new DefectCategory { CategoryCode = $"C-{tag}", CategoryName = "Insulator crack", Description = "", SeverityWeight = 1 };
        db.AddRange(region, tower, mission, category);
        db.Add(new MissionTarget { Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = tower.Id, Sequence = 1 });
        db.Add(new MissionAssignment { Id = Guid.NewGuid(), MissionId = mission.Id, UserId = inspectorId, AssignmentRole = "INSPECTOR", AssignedById = managerId });
        await db.SaveChangesAsync();

        var media = new InspectionMedia
        {
            Id = Guid.NewGuid(), MissionId = mission.Id, TowerId = tower.Id, MediaType = "Image", ValidationStatus = "Pending",
            FileUrl = "/images/it.jpg", CapturedAt = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc)
        };
        var run = new AIAnalysisRun { Id = Guid.NewGuid(), MediaId = media.Id, ExecutionLocation = AIAnalysisRun.Cloud, ModelName = "RF-DETR", Status = AIAnalysisStatus.Completed };
        DetectedAnomaly Anomaly(string status) => new()
        {
            Id = Guid.NewGuid(), MediaId = media.Id, AnalysisRunId = run.Id, CategoryId = category.Id, TowerId = tower.Id,
            BoundingBox = "{}", ConfidenceScore = 0.9, ValidationStatus = status, AnalystNotes = ""
        };
        var pending = Anomaly("Pending");
        var confirmed = Anomaly("Confirmed");
        db.AddRange(media, run, pending, confirmed);
        db.Add(new MaintenanceTicket { Id = Guid.NewGuid(), TicketCode = $"TK-{tag}", AnomalyId = confirmed.Id, ManagerId = managerId, TechnicianId = inspectorId, Status = TicketStatus.Open, Description = "Fix" });
        await db.SaveChangesAsync();
        return new Seed(managerId, inspectorId, region.Id, tower.Id, mission.Id, media.Id, run.Id, category.Id, pending.Id, confirmed.Id);
    }

    /// <summary>Users carry PasswordHash, which only IdentityService maps, so they are inserted in SQL.</summary>
    private static async Task<Guid> InsertUserAsync(ApplicationDbContext db, string email, Guid? id = null)
    {
        var userId = id ?? Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
            VALUES ({userId}, 'IT user', {email}, '', 'Active', true, 'x', now(), false)
            """);
        return userId;
    }

    private sealed record Seed(Guid ManagerId, Guid InspectorId, Guid RegionId, Guid TowerId, Guid MissionId,
        Guid MediaId, Guid RunId, int CategoryId, Guid PendingAnomalyId, Guid ConfirmedAnomalyId);

    [Fact]
    public async Task AllMigrations_ApplyToAnEmptyDatabase_AndMatchTheModel()
    {
        if (!_pg.Available) return;
        await using var db = _pg.CreateContext();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
    }

    [Fact]
    public async Task ErdConstraintsAndLengths_ExistInTheDatabase()
    {
        if (!_pg.Available) return;
        await using var conn = new NpgsqlConnection(_pg.ConnectionString);
        await conn.OpenAsync();

        async Task<long> Count(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            return (long)(await cmd.ExecuteScalarAsync())!;
        }

        (await Count("""
            SELECT count(*) FROM pg_constraint WHERE conname IN (
              'CK_AlertEscalations_DifferentUsers', 'CK_InspectionSchedules_RecurrenceRule',
              'AK_AIAnalysisRuns_Id_MediaId', 'FK_DetectedAnomalies_AIAnalysisRuns_AnalysisRunId_MediaId',
              'CK_DetectedAnomalies_ConfirmedRequiresTower')
            """)).Should().Be(5);
        (await Count("""
            SELECT count(*) FROM pg_indexes WHERE indexname IN (
              'UX_EmergencyAlerts_ActivePerAnomaly', 'IX_EmergencyAlerts_AnomalyId', 'IX_Users_Email', 'IX_Users_Phone',
              'IX_MissionAssignments_UserId_Status', 'IX_MaintenanceTickets_TechnicianId_Status')
            """)).Should().Be(6);

        foreach (var (table, column, length) in ErdColumnLengths.Columns)
        {
            (await Count($"""
                SELECT count(*) FROM information_schema.columns
                WHERE table_name = '{table}' AND column_name = '{column}'
                  AND data_type = 'character varying' AND character_maximum_length = {length}
                """)).Should().Be(1, $"{table}.{column} should be varchar({length})");
        }
    }

    [Fact]
    public async Task Constraints_RejectDuplicateActiveAlert_AndRunOfAnotherMedia()
    {
        if (!_pg.Available) return;
        var seed = await SeedAsync();

        await using (var db = _pg.CreateContext())
        {
            db.EmergencyAlerts.Add(new EmergencyAlert { Id = Guid.NewGuid(), AnomalyId = seed.PendingAnomalyId, Status = "Active", Priority = "High" });
            await db.SaveChangesAsync();
            db.EmergencyAlerts.Add(new EmergencyAlert { Id = Guid.NewGuid(), AnomalyId = seed.PendingAnomalyId, Status = "Active", Priority = "High" });
            var duplicate = () => db.SaveChangesAsync();
            (await duplicate.Should().ThrowAsync<DbUpdateException>()).Which.InnerException!.Message.Should().Contain("UX_EmergencyAlerts_ActivePerAnomaly");
        }

        await using (var db = _pg.CreateContext())
        {
            // An anomaly may not reference a run of a different media (composite FK, plan item 3.2).
            var otherMedia = new InspectionMedia { Id = Guid.NewGuid(), MissionId = seed.MissionId, MediaType = "Image", ValidationStatus = "Pending", FileUrl = "/images/o.jpg" };
            db.InspectionMedia.Add(otherMedia);
            await db.SaveChangesAsync();
            db.DetectedAnomalies.Add(new DetectedAnomaly
            {
                Id = Guid.NewGuid(), MediaId = otherMedia.Id, AnalysisRunId = seed.RunId, CategoryId = seed.CategoryId,
                BoundingBox = "{}", ValidationStatus = "Pending", AnalystNotes = ""
            });
            var mismatch = () => db.SaveChangesAsync();
            (await mismatch.Should().ThrowAsync<DbUpdateException>()).Which.InnerException!.Message.Should().Contain("AnalysisRunId_MediaId");
        }
    }

    [Fact]
    public async Task TowerHistoryQueries_TranslateAndCount()
    {
        if (!_pg.Available) return;
        var seed = await SeedAsync();
        await using var db = _pg.CreateContext();

        var (items, total) = await new AnomalyRepository(db).GetByTowerIdPagedAsync(
            seed.TowerId, "Pending", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), 1, 10);
        items.Select(a => a.Id).Should().Equal(seed.PendingAnomalyId);
        total.Should().Be(1);

        var anomalyCounts = await new AnomalyRepository(db).CountByTowerGroupedByStatusAsync(seed.TowerId);
        anomalyCounts.Should().BeEquivalentTo(new Dictionary<string, int> { ["Pending"] = 1, ["Confirmed"] = 1 });

        var (tickets, ticketTotal) = await new MaintenanceTicketRepository(db).GetByTowerIdPagedAsync(seed.TowerId, null, null, 1, 10);
        ticketTotal.Should().Be(1);
        tickets.Single().MaintenanceProofs.Should().BeEmpty();
        (await new MaintenanceTicketRepository(db).CountByTowerGroupedByStatusAsync(seed.TowerId))
            .Should().BeEquivalentTo(new Dictionary<TicketStatus, int> { [TicketStatus.Open] = 1 });

        (await new InspectionMediaRepository(db).GetLastCapturedAtByTowerAsync(seed.TowerId))
            .Should().Be(new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GeographicAccessFilter_GrantsTowersThroughAssignment_AndHidesThemFromOthers()
    {
        if (!_pg.Available) return;
        var seed = await SeedAsync();

        await using (var db = _pg.CreateContext())
        {
            var inspector = User(seed.InspectorId, UserRoles.Inspector).Object;
            var towers = new TowerRepository(db, new GeographicAccessFilter(db, inspector));
            (await towers.GetAccessibleByIdAsync(seed.TowerId, CancellationToken.None)).Should().NotBeNull();
        }

        await using (var db = _pg.CreateContext())
        {
            var stranger = User(Guid.NewGuid(), UserRoles.Inspector).Object;
            await InsertUserAsync(db, $"s-{Guid.NewGuid():N}@it.test", stranger.UserId);
            var towers = new TowerRepository(db, new GeographicAccessFilter(db, stranger));
            (await towers.GetAccessibleByIdAsync(seed.TowerId, CancellationToken.None)).Should().BeNull();
        }
    }

    [Fact]
    public async Task MediaUrl_IsStoredAsStorageKey_AndReadBackAsUrl()
    {
        if (!_pg.Available) return;
        var seed = await SeedAsync();
        const string baseUrl = "http://minio.it.test:9000";
        StorageKeys.Configure(baseUrl);
        try
        {
            var mediaId = Guid.NewGuid();
            await using (var db = _pg.CreateContext())
            {
                db.InspectionMedia.Add(new InspectionMedia
                {
                    Id = mediaId, MissionId = seed.MissionId, MediaType = "Image", ValidationStatus = "Pending",
                    FileUrl = $"{baseUrl}/uav-media/2026/10/frame.jpg"
                });
                await db.SaveChangesAsync();
            }

            await using var conn = new NpgsqlConnection(_pg.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("""SELECT "FileUrl" FROM "InspectionMedia" WHERE "Id" = @id""", conn);
            cmd.Parameters.AddWithValue("id", mediaId);
            ((string)(await cmd.ExecuteScalarAsync())!).Should().Be("uav-media/2026/10/frame.jpg");

            await using var read = _pg.CreateContext();
            (await read.InspectionMedia.SingleAsync(m => m.Id == mediaId)).FileUrl.Should().Be($"{baseUrl}/uav-media/2026/10/frame.jpg");
        }
        finally
        {
            StorageKeys.Configure(null);
        }
    }
}
