using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.Commands.CreateMission;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionPriorityAndSchedulingTests
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
    public void Validator_ShouldFail_WhenEmergencyPriority_HasNoEmergencyReason()
    {
        var validator = new CreateMissionCommandValidator();
        var command = new CreateMissionCommand(
            Title: "Emergency Flight",
            RouteData: null,
            AssignedToUserId: Guid.NewGuid(),
            DroneCode: "DRONE-01",
            Status: "Pending",
            Description: "Emergency inspection",
            TargetAssetIds: new[] { Guid.NewGuid() },
            Priority: MissionPriority.Emergency,
            EmergencyReason: null
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "EmergencyReason");
    }

    [Fact]
    public void Validator_ShouldPass_WhenEmergencyPriority_HasValidEmergencyReason()
    {
        var validator = new CreateMissionCommandValidator();
        var command = new CreateMissionCommand(
            Title: "Emergency Flight",
            RouteData: null,
            AssignedToUserId: Guid.NewGuid(),
            DroneCode: "DRONE-01",
            Status: "Pending",
            Description: "Emergency inspection",
            TargetAssetIds: new[] { Guid.NewGuid() },
            Priority: MissionPriority.Emergency,
            EmergencyReason: "Critical conductor defect detected after lightning strike."
        );

        var result = validator.Validate(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMissionFromAssessment_ShouldEnforceEmergencyReason_WhenEmergency()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        var asset = new Asset { Id = Guid.NewGuid(), Status = "Active" };
        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Regions.Add(region);
        db.Uavs.Add(drone);
        db.Assets.Add(asset);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddHours(1),
            PlannedEnd = DateTime.UtcNow.AddHours(3),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(5)
        };
        assessment.Assets.Add(new PreMissionAssessmentAsset { AssetId = asset.Id, Sequence = 1 });
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = inspectorId, IsEligible = true });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = true });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        // Request with Emergency but missing EmergencyReason
        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Urgent Tower Check",
            "Emergency mission",
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id },
            Priority: MissionPriority.Emergency,
            EmergencyReason: ""
        );

        var act = async () => await service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*EMERGENCY_REASON_REQUIRED*");
    }

    [Fact]
    public async Task CreateMissionFromAssessment_ShouldPersistPriorityFields_AndAudit()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        var asset = new Asset { Id = Guid.NewGuid(), Status = "Active" };
        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Regions.Add(region);
        db.Uavs.Add(drone);
        db.Assets.Add(asset);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddHours(1),
            PlannedEnd = DateTime.UtcNow.AddHours(3),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(5)
        };
        assessment.Assets.Add(new PreMissionAssessmentAsset { AssetId = asset.Id, Sequence = 1 });
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = inspectorId, IsEligible = true });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = true });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Urgent Conductor Check",
            "Emergency mission description",
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id },
            Priority: MissionPriority.Emergency,
            Objective: InspectionObjective.IncidentFollowUp,
            PriorityDefects: new List<string> { PriorityDefectCategories.Conductor, PriorityDefectCategories.Insulator },
            EmergencyReason: "Broken conductor reported by ground team.",
            IsImmediate: true
        );

        var mission = await service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);

        mission.Should().NotBeNull();
        mission.Priority.Should().Be(MissionPriority.Emergency);
        mission.Objective.Should().Be(InspectionObjective.IncidentFollowUp);
        mission.EmergencyReason.Should().Be("Broken conductor reported by ground team.");
        mission.IsImmediate.Should().BeTrue();
        mission.PriorityDefectsJson.Should().Contain(PriorityDefectCategories.Conductor);

        // Verify Audit Log was recorded
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == mission.Id && a.ActionType == "MISSION_CREATED_FROM_ASSESSMENT");
        audit.Should().NotBeNull();
        audit!.NewValues.Should().Contain("Emergency");
        audit.NewValues.Should().Contain("Broken conductor");
    }

    [Fact]
    public async Task ConflictDetection_ShouldPreventAutomaticPreemption_AndProvideDetails()
    {
        var managerId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        var asset = new Asset { Id = Guid.NewGuid(), Status = "Active" };
        db.Users.Add(new User { Id = managerId, Status = "Active" });
        db.Users.Add(new User { Id = inspectorId, Status = "Active" });
        db.Regions.Add(region);
        db.Uavs.Add(drone);
        db.Assets.Add(asset);

        var start = DateTime.UtcNow.AddHours(2);
        var end = DateTime.UtcNow.AddHours(4);

        // Existing conflicting booking held by another mission
        var existingMission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-EXISTING-001",
            Title = "Scheduled maintenance",
            Status = MissionStatus.Assigned
        };
        db.Missions.Add(existingMission);

        db.ResourceBookings.Add(new ResourceBooking
        {
            Id = Guid.NewGuid(),
            MissionId = existingMission.Id,
            DroneId = drone.Id,
            StartAt = start,
            EndAt = end,
            Status = ResourceBookingStatus.Active
        });

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = start,
            PlannedEnd = end,
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(5)
        };
        assessment.Assets.Add(new PreMissionAssessmentAsset { AssetId = asset.Id, Sequence = 1 });
        assessment.PersonnelCandidates.Add(new PreMissionAssessmentPersonnel { UserId = inspectorId, IsEligible = true });
        assessment.DroneCandidates.Add(new PreMissionAssessmentDrone { DroneId = drone.Id, IsEligible = true });
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var request = new CreateMissionFromAssessmentRequest(
            assessment.Id,
            "Emergency Overlapping Check",
            "Urgent request",
            new List<MissionPersonnelAssignmentRequest> { new(inspectorId, "INSPECTOR", true) },
            new List<Guid> { drone.Id },
            Priority: MissionPriority.Emergency,
            EmergencyReason: "Flashover occurred on line segment."
        );

        // Creating Emergency mission MUST NOT automatically preempt or cancel existing mission
        var act = async () => await service.CreateMissionFromAssessmentAsync(request, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.Message.Should().Contain("RESOURCE_BOOKING_CONFLICT");
        ex.Which.Message.Should().Contain("Drone");
        ex.Which.Message.Should().Contain("MS-EXISTING-001");

        // Existing booking remains untouched
        var booking = await db.ResourceBookings.SingleAsync(b => b.MissionId == existingMission.Id);
        booking.Status.Should().Be(ResourceBookingStatus.Active);
    }

    [Fact]
    public async Task MissionOrdering_ShouldOrderEmergencyFirst_ThenPlannedStart_ThenCreatedAt()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        var mNormal1 = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MIS-008",
            Title = "Normal Later",
            ManagerId = managerId,
            Priority = MissionPriority.Normal,
            PlannedStart = now.AddDays(2),
            CreatedAt = now.AddHours(1)
        };

        var mNormal2 = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MIS-010",
            Title = "Normal Earlier",
            ManagerId = managerId,
            Priority = MissionPriority.Normal,
            PlannedStart = now.AddDays(1),
            CreatedAt = now.AddHours(2)
        };

        var mHigh1 = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MIS-011",
            Title = "High Earlier Start",
            ManagerId = managerId,
            Priority = MissionPriority.High,
            PlannedStart = now.AddHours(10),
            CreatedAt = now.AddHours(3)
        };

        var mHigh2 = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MIS-012",
            Title = "High Later Start",
            ManagerId = managerId,
            Priority = MissionPriority.High,
            PlannedStart = now.AddHours(20),
            CreatedAt = now.AddHours(4)
        };

        var mEmergency = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MIS-014",
            Title = "Emergency ASAP",
            ManagerId = managerId,
            Priority = MissionPriority.Emergency,
            PlannedStart = now.AddHours(1),
            CreatedAt = now.AddHours(5)
        };

        db.Missions.AddRange(mNormal1, mNormal2, mHigh1, mHigh2, mEmergency);
        await db.SaveChangesAsync();

        var repo = new MissionRepository(db);

        var (items, total) = await repo.GetMissionsPagedAsync(
            page: 1,
            pageSize: 10,
            search: null,
            status: null,
            sortBy: "priority",
            sortDescending: true
        );

        total.Should().Be(5);
        items.Count.Should().Be(5);
        var orderedCodes = items.Select(x => x.MissionCode).ToList();

        // EMERGENCY (MIS-014) -> HIGH (MIS-011, MIS-012) -> NORMAL (MIS-010, MIS-008)
        orderedCodes[0].Should().Be("MIS-014"); // Emergency
        orderedCodes[1].Should().Be("MIS-011"); // High, plannedStart = 10h
        orderedCodes[2].Should().Be("MIS-012"); // High, plannedStart = 20h
        orderedCodes[3].Should().Be("MIS-010"); // Normal, plannedStart = 1d
        orderedCodes[4].Should().Be("MIS-008"); // Normal, plannedStart = 2d
    }
}
