using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.IntegrationTests;

/// <summary>
/// Seeds one isolated MF02 world per test on the shared PostgreSQL container: a region with towers, a manager in
/// scope, field users with roles and scope, and Available/Healthy UAVs. Every service call runs on a fresh
/// DbContext, like one HTTP request.
/// </summary>
internal sealed class MissionWorld
{
    private readonly PostgresFixture _pg;

    public string Tag { get; } = Guid.NewGuid().ToString("N")[..8];
    public Guid RegionId { get; private set; }
    public List<Guid> TowerIds { get; } = new();
    public Guid ManagerId { get; private set; }

    private MissionWorld(PostgresFixture pg) => _pg = pg;

    public static async Task<MissionWorld> CreateAsync(PostgresFixture pg)
    {
        var world = new MissionWorld(pg);
        await using var db = pg.CreateContext();
        var region = new Region { Id = Guid.NewGuid(), Code = $"R-{world.Tag}", Name = "Region", Type = "Province" };
        world.RegionId = region.Id;
        db.Add(region);
        for (var i = 1; i <= 2; i++)
        {
            var tower = new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{world.Tag}-{i}", Geom = new Point(105.85 + i * 0.001, 21.03) { SRID = 4326 } };
            world.TowerIds.Add(tower.Id);
            db.Add(tower);
        }
        await db.SaveChangesAsync();
        world.ManagerId = await world.AddUserAsync(UserRoles.Manager);
        return world;
    }

    /// <summary>A user with one system role and a scope row for the world's region (unless inScope is false).</summary>
    public async Task<Guid> AddUserAsync(string role = UserRoles.Inspector, bool inScope = true, string status = "Active")
    {
        var userId = Guid.NewGuid();
        var email = $"{role.ToLowerInvariant()}-{userId:N}@it.test";
        await using var db = _pg.CreateContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
            VALUES ({userId}, {role + " " + Tag}, {email}, '', {status}, true, 'x', now(), false)
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Roles" ("RoleName", "Description") VALUES ({role}, '') ON CONFLICT ("RoleName") DO NOTHING
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "UserRoles" ("UserId", "RoleId", "AssignedAt")
            SELECT {userId}, "Id", now() FROM "Roles" WHERE "RoleName" = {role}
            """);
        if (inScope)
        {
            db.UserGeographicScopes.Add(new UserGeographicScope { UserId = userId, RegionId = RegionId });
            await db.SaveChangesAsync();
        }
        return userId;
    }

    public async Task<Guid> AddDroneAsync(TechnicalHealth health = TechnicalHealth.Healthy, DroneOperationalStatus status = DroneOperationalStatus.Available)
    {
        await using var db = _pg.CreateContext();
        var id = Guid.NewGuid();
        db.Uavs.Add(new Uav
        {
            Id = id, UavCode = $"UAV-{Tag}-{id.ToString()[..4]}", Name = "Drone", Model = "M300", SerialNumber = $"SN-{id:N}",
            AssignedRegionId = RegionId, TechnicalHealth = health, OperationalStatus = status, FlightStatus = DroneStatus.Idle,
        });
        await db.SaveChangesAsync();
        return id;
    }

    /// <summary>A Ready assessment over [start, end) whose candidates are the given people and UAVs.</summary>
    public async Task<Guid> AddAssessmentAsync(DateTime start, DateTime end, IEnumerable<Guid> people, IEnumerable<Guid> drones)
    {
        await using var db = _pg.CreateContext();
        var assessment = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            ManagerId = ManagerId,
            RegionId = RegionId,
            PlannedStart = start,
            PlannedEnd = end,
            ProposedBoundary = new Polygon(new LinearRing(new[]
            {
                new Coordinate(105.8, 21.0), new Coordinate(105.9, 21.0), new Coordinate(105.9, 21.1), new Coordinate(105.8, 21.0)
            })) { SRID = 4326 },
            Status = PreMissionAssessmentStatus.Ready,
            SiteFeasibilityStatus = ReadinessCheckStatus.Passed,
            OverallTechnicalHealth = TechnicalHealth.Healthy,
            ValidUntil = DateTime.UtcNow.AddHours(3),
        };
        assessment.Towers = TowerIds.Select((t, i) => new PreMissionAssessmentTower { AssessmentId = assessment.Id, TowerId = t, Sequence = i + 1 }).ToList();
        assessment.PersonnelCandidates = people.Select(userId => new PreMissionAssessmentPersonnel
        {
            AssessmentId = assessment.Id, UserId = userId, SnapshotAt = DateTime.UtcNow,
            EligibilityStatus = ResourceEligibilityStatus.Eligible, AvailabilityStatus = ResourceAvailabilityStatus.Available,
        }).ToList();
        assessment.DroneCandidates = drones.Select(droneId => new PreMissionAssessmentDrone
        {
            AssessmentId = assessment.Id, DroneId = droneId, SnapshotAt = DateTime.UtcNow, TechnicalHealth = TechnicalHealth.Healthy,
            OperationalAvailabilityStatus = ResourceAvailabilityStatus.Available, TechnicalEligibilityStatus = ResourceEligibilityStatus.Eligible,
        }).ToList();
        db.Add(assessment);
        await db.SaveChangesAsync();
        return assessment.Id;
    }

    public static CreateMissionFromAssessmentRequest Request(
        Guid assessmentId, IEnumerable<MissionPersonnelAssignmentRequest> people, IEnumerable<Guid> drones,
        string? idempotencyKey = null, DateTime? confirmationDeadline = null, string title = "IT mission") =>
        new(assessmentId, title, null, people.ToList(), drones.ToList(), idempotencyKey,
            ConfirmationDeadline: confirmationDeadline, ManagerInstructions: "Bring spare batteries");

    /// <summary>Creates a Pending Acceptance mission through the real create-mission service.</summary>
    public Task<Mission> CreateMissionAsync(CreateMissionFromAssessmentRequest request) =>
        WithAssessments(ManagerId, UserRoles.Manager, s => s.CreateMissionFromAssessmentAsync(request, default));

    /// <summary>Assessment + mission over [start, end) with one required inspector (and optional extra people).</summary>
    public async Task<Mission> CreateMissionAsync(DateTime start, DateTime end, Guid inspectorId, Guid droneId, params MissionPersonnelAssignmentRequest[] extra)
    {
        var people = new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) };
        people.AddRange(extra);
        var assessmentId = await AddAssessmentAsync(start, end, people.Select(p => p.UserId), new[] { droneId });
        return await CreateMissionAsync(Request(assessmentId, people, new[] { droneId }));
    }

    public async Task<T> As<T>(Guid userId, string role, Func<MissionLifecycleService, Task<T>> action)
    {
        var current = Current(userId, role);
        await using var db = _pg.CreateContext(current);
        return await action(new MissionLifecycleService(db, current));
    }

    public async Task As(Guid userId, string role, Func<MissionLifecycleService, Task> action)
    {
        var current = Current(userId, role);
        await using var db = _pg.CreateContext(current);
        await action(new MissionLifecycleService(db, current));
    }

    public Task AsManager(Func<MissionLifecycleService, Task> action) => As(ManagerId, UserRoles.Manager, action);
    public Task<T> AsManager<T>(Func<MissionLifecycleService, Task<T>> action) => As(ManagerId, UserRoles.Manager, action);
    public Task AsInspector(Guid inspectorId, Func<MissionLifecycleService, Task> action) => As(inspectorId, UserRoles.Inspector, action);

    public async Task<T> WithAssessments<T>(Guid userId, string role, Func<PreMissionAssessmentService, Task<T>> action)
    {
        var current = Current(userId, role);
        await using var db = _pg.CreateContext(current);
        return await action(new PreMissionAssessmentService(db, current));
    }

    public async Task<T> Read<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        await using var db = _pg.CreateContext();
        return await query(db);
    }

    public async Task Write(Func<ApplicationDbContext, Task> change)
    {
        await using var db = _pg.CreateContext();
        await change(db);
        await db.SaveChangesAsync();
    }

    public Task<Mission> LoadMissionAsync(Guid missionId) => Read(db => db.Missions
        .Include(m => m.Assignments).Include(m => m.CheckIns).Include(m => m.DroneHandovers)
        .AsNoTracking().SingleAsync(m => m.Id == missionId));

    public Task<List<ResourceBooking>> BookingsAsync(Guid missionId) => Read(db => db.ResourceBookings.AsNoTracking()
        .Where(b => b.MissionId == missionId).ToListAsync());

    public Task<Uav> DroneAsync(Guid droneId) => Read(db => db.Uavs.AsNoTracking().SingleAsync(u => u.Id == droneId));

    /// <summary>Every required member accepts, so the mission becomes Assigned (BR-30).</summary>
    public async Task AcceptAllAsync(Mission mission)
    {
        foreach (var a in mission.Assignments.Where(a => a.Status == MissionAssignmentStatus.Active && a.IsRequired))
            await AsInspector(a.UserId, s => s.AcceptAssignmentAsync(mission.Id, default));
    }

    public static ICurrentUserServices Current(Guid userId, string role)
    {
        var current = new Mock<ICurrentUserServices>();
        current.SetupGet(u => u.UserId).Returns(userId);
        current.SetupGet(u => u.IsAuthenticated).Returns(true);
        current.SetupGet(u => u.Roles).Returns(new[] { role });
        current.SetupGet(u => u.Username).Returns(role);
        return current.Object;
    }
}
