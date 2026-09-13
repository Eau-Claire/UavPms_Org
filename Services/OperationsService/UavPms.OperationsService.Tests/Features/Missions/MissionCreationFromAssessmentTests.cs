using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionCreationFromAssessmentTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    [Fact]
    public async Task CreateMissionFromAssessment_CreatesPendingAcceptanceWithBookingsAndOutbox()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });

        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        db.Uavs.Add(drone);

        var asset = new Asset { Id = Guid.NewGuid(), Status = "Active" };
        db.Assets.Add(asset);

        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = start,
            PlannedEnd = end,
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(3)
        };
        assessment.Assets.Add(new PreMissionAssessmentAsset { AssetId = asset.Id, Sequence = 1 });
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = inspectorId, IsEligible = true });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = true });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Inspection Mission 101",
            "High priority inspection",
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id },
            "IDEMP-KEY-101"
        );

        var mission = await service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);

        mission.Should().NotBeNull();
        mission.Status.Should().Be(MissionStatus.PendingAcceptance);
        mission.PreMissionAssessmentId.Should().Be(assessment.Id);
        mission.UavId.Should().Be(drone.Id);
        mission.Assignments.Should().HaveCount(1);
        mission.Assignments.Single().ResponseStatus.Should().Be(MissionAssignmentResponse.Pending);
        mission.MissionTargets.Should().HaveCount(1);

        // Verify Assessment is consumed
        assessment.Status.Should().Be(PreMissionAssessmentStatus.Consumed);
        assessment.ConsumedByMissionId.Should().Be(mission.Id);

        // Verify ResourceBookings were created
        var bookings = await db.ResourceBookings.Where(b => b.MissionId == mission.Id).ToListAsync();
        bookings.Should().HaveCount(2); // 1 for user, 1 for drone
        bookings.Should().Contain(b => b.UserId == inspectorId && b.Status == ResourceBookingStatus.Active);
        bookings.Should().Contain(b => b.DroneId == drone.Id && b.Status == ResourceBookingStatus.Active);

        // Verify OutboxMessage
        var outbox = await db.OutboxMessages.FirstOrDefaultAsync(x => x.MessageType == "MissionCreatedFromAssessment");
        outbox.Should().NotBeNull();
        outbox!.Payload.Should().Contain("Inspection Mission 101");
    }

    [Fact]
    public async Task CreateMissionFromAssessment_IdempotentCall_ReturnsSameMission()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid() };
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        var asset = new Asset { Id = Guid.NewGuid(), Status = "Active" };
        db.Regions.Add(region);
        db.Uavs.Add(drone);
        db.Assets.Add(asset);

        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = start,
            PlannedEnd = end,
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(3)
        };
        assessment.Assets.Add(new PreMissionAssessmentAsset { AssetId = asset.Id, Sequence = 1 });
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = inspectorId, IsEligible = true });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = true });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Mission Idempotency Test",
            null,
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id },
            "IDEMP-KEY-999"
        );

        var first = await service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);
        var second = await service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task CreateMissionFromAssessment_ResourceNotCandidate_ThrowsBusinessRule()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var unlistedInspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid() };
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        var asset = new Asset { Id = Guid.NewGuid(), Status = "Active" };
        db.Regions.Add(region);
        db.Uavs.Add(drone);
        db.Assets.Add(asset);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(3)
        };
        assessment.Assets.Add(new PreMissionAssessmentAsset { AssetId = asset.Id, Sequence = 1 });
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = inspectorId, IsEligible = true });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = true });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Test Invalid Inspector",
            null,
            new List<MissionPersonnelAssignmentRequest> { new(unlistedInspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id }
        );

        var act = () => service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*RESOURCE_NOT_IN_ASSESSMENT*");
    }

    [Fact]
    public async Task CreateMissionFromAssessment_AlreadyConsumed_ThrowsBusinessRule()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid() };
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01" };
        db.Regions.Add(region);
        db.Uavs.Add(drone);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Consumed,
            ConsumedByMissionId = Guid.NewGuid()
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Test Already Consumed",
            null,
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id }
        );

        var act = () => service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*ASSESSMENT_ALREADY_CONSUMED*");
    }
}
