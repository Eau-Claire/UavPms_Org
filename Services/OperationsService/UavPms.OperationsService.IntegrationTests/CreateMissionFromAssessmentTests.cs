using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
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
/// POST /pre-mission-assessments/{id}/create-mission is now the only way to create a mission, so its save and the
/// JSON the controller returns are checked on real PostgreSQL.
/// </summary>
[Collection(PostgresCollection.Name)]
public class CreateMissionFromAssessmentTests
{
    private readonly PostgresFixture _pg;

    public CreateMissionFromAssessmentTests(PostgresFixture pg) => _pg = pg;

    [Fact]
    public async Task CreateMission_FromReadyAssessment_SavesMissionAndSerializes()
    {
        if (!_pg.Available) return;

        var tag = Guid.NewGuid().ToString("N")[..8];
        Guid managerId, inspectorId, analystId, droneId, assessmentId;
        await using (var db = _pg.CreateContext())
        {
            managerId = await InsertUserAsync(db, $"m-{tag}@it.test");
            inspectorId = await InsertUserAsync(db, $"i-{tag}@it.test");
            analystId = await InsertUserAsync(db, $"a-{tag}@it.test");

            var region = new Region { Id = Guid.NewGuid(), Code = $"R-{tag}", Name = "Central", Type = "Province" };
            var towers = Enumerable.Range(1, 2)
                .Select(i => new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = $"T-{tag}-{i}", Geom = new Point(105.85 + i * 0.001, 21.03) { SRID = 4326 } })
                .ToList();
            var drone = new Uav { Id = Guid.NewGuid(), UavCode = $"UAV-{tag}", Name = "Drone", Model = "M300", SerialNumber = $"SN-{tag}", AssignedRegionId = region.Id, TechnicalHealth = TechnicalHealth.Healthy };
            droneId = drone.Id;

            var start = DateTime.UtcNow.AddHours(1);
            var assessment = new PreMissionAssessment
            {
                Id = Guid.NewGuid(),
                ManagerId = managerId,
                RegionId = region.Id,
                PlannedStart = start,
                PlannedEnd = start.AddHours(3),
                ProposedBoundary = new Polygon(new LinearRing(new[]
                {
                    new Coordinate(105.8, 21.0), new Coordinate(105.9, 21.0), new Coordinate(105.9, 21.1), new Coordinate(105.8, 21.0)
                })) { SRID = 4326 },
                Status = PreMissionAssessmentStatus.Ready,
                SiteFeasibilityStatus = ReadinessCheckStatus.Passed,
                OverallTechnicalHealth = TechnicalHealth.Healthy,
                ValidUntil = DateTime.UtcNow.AddHours(3),
            };
            assessmentId = assessment.Id;
            assessment.Towers = towers.Select((t, i) => new PreMissionAssessmentTower { AssessmentId = assessment.Id, TowerId = t.Id, Sequence = i + 1 }).ToList();
            assessment.PersonnelCandidates = new[] { inspectorId, analystId }
                .Select(userId => new PreMissionAssessmentPersonnel
                {
                    AssessmentId = assessment.Id, UserId = userId, SnapshotAt = DateTime.UtcNow,
                    EligibilityStatus = ResourceEligibilityStatus.Eligible, AvailabilityStatus = ResourceAvailabilityStatus.Available,
                })
                .ToList();
            assessment.DroneCandidates = new List<PreMissionAssessmentDrone>
            {
                new()
                {
                    AssessmentId = assessment.Id, DroneId = drone.Id, SnapshotAt = DateTime.UtcNow, TechnicalHealth = TechnicalHealth.Healthy,
                    OperationalAvailabilityStatus = ResourceAvailabilityStatus.Available, TechnicalEligibilityStatus = ResourceEligibilityStatus.Eligible,
                },
            };

            db.Add(region);
            db.AddRange(towers);
            db.AddRange(drone, assessment);
            await db.SaveChangesAsync();
        }

        var current = new Mock<ICurrentUserServices>();
        current.SetupGet(u => u.UserId).Returns(managerId);
        current.SetupGet(u => u.IsAuthenticated).Returns(true);
        current.SetupGet(u => u.Roles).Returns(new[] { UserRoles.Manager });

        // Same body the frontend sends from /missions/new.
        var request = new CreateMissionFromAssessmentRequest(
            assessmentId,
            "Kiểm tra Central",
            null,
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR"), new(analystId, "ANALYST") },
            new List<Guid> { droneId },
            Guid.NewGuid().ToString(),
            MissionPriority.Normal,
            InspectionObjective.PeriodicInspection,
            null,
            null,
            false);

        Mission mission;
        await using (var db = _pg.CreateContext(current.Object))
        {
            mission = await new PreMissionAssessmentService(db, current.Object).CreateMissionFromAssessmentAsync(request, default);

            // The controller returns this entity as-is; it must serialize with the API's JSON options.
            var serialize = () => JsonSerializer.Serialize(mission, ApiJsonOptions());
            serialize.Should().NotThrow();
        }

        await using (var db = _pg.CreateContext())
        {
            var saved = await db.Missions.Include(m => m.MissionTargets).Include(m => m.Assignments).SingleAsync(m => m.Id == mission.Id);
            saved.Status.Should().Be(MissionStatus.PendingAcceptance);
            saved.MissionTargets.Should().HaveCount(2);
            saved.Assignments.Should().HaveCount(2);
            (await db.PreMissionAssessments.SingleAsync(a => a.Id == assessmentId)).Status.Should().Be(PreMissionAssessmentStatus.Completed);
        }
    }

    /// <summary>Mirrors AddJsonOptions in OperationsService.API Program.cs.</summary>
    private static JsonSerializerOptions ApiJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
        };
        options.Converters.Add(new LenientMissionPriorityConverter());
        options.Converters.Add(new LenientInspectionObjectiveConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new NetTopologySuite.IO.Converters.GeoJsonConverterFactory());
        return options;
    }

    private static async Task<Guid> InsertUserAsync(ApplicationDbContext db, string email)
    {
        var userId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Users" ("Id", "FullName", "Email", "Phone", "Status", "IsEmailVerified", "PasswordHash", "CreatedAt", "IsDeleted")
            VALUES ({userId}, 'IT user', {email}, '', 'Active', true, 'x', now(), false)
            """);
        return userId;
    }
}
