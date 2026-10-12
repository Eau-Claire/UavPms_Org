using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class PreMissionAssessmentServiceTests
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
    public async Task CreateAssessment_ValidRequest_CreatesAssessmentWithAssets()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var sub = new Substation { Id = Guid.NewGuid(), RegionAssetId = region.Id };
        var line = new TransmissionLine { Id = Guid.NewGuid(), Substation = sub };
        var tower = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var asset = new Asset { Id = Guid.NewGuid(), Tower = tower, Status = "Active" };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);

        var result = await service.CreateAsync(region.Id, start, end, new[] { asset.Id }, CancellationToken.None);

        result.Should().NotBeNull();
        result.ManagerId.Should().Be(managerId);
        result.RegionId.Should().Be(region.Id);
        result.Status.Should().BeOneOf(PreMissionAssessmentStatus.Evaluating, PreMissionAssessmentStatus.NotReady);
        result.Assets.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAssessment_WithBoundaryWkt_EnforcesBoundaryCoverage()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        var insidePoint = geometryFactory.CreatePoint(new Coordinate(106.5, 10.5));
        var outsidePoint = geometryFactory.CreatePoint(new Coordinate(109.0, 15.0));

        var sub = new Substation { Id = Guid.NewGuid(), RegionAssetId = region.Id };
        var line = new TransmissionLine { Id = Guid.NewGuid(), Substation = sub };
        var tower1 = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var tower2 = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var assetInside = new Asset { Id = Guid.NewGuid(), Tower = tower1, Status = "Active", Location = insidePoint };
        var assetOutside = new Asset { Id = Guid.NewGuid(), Tower = tower2, Status = "Active", Location = outsidePoint };
        db.Assets.AddRange(assetInside, assetOutside);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);
        var boundaryWkt = "POLYGON((106 10, 107 10, 107 11, 106 11, 106 10))";

        // Asset outside boundary throws BusinessRuleException
        var act = () => service.CreateAsync(region.Id, start, end, new[] { assetOutside.Id }, boundaryWkt, null, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*ASSET_OUTSIDE_BOUNDARY*");

        // Asset inside boundary succeeds
        var success = await service.CreateAsync(region.Id, start, end, new[] { assetInside.Id }, boundaryWkt, null, CancellationToken.None);
        success.Should().NotBeNull();
        success.ProposedBoundary.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAssessment_WithIdempotencyKey_ReturnsExistingInstance()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var sub = new Substation { Id = Guid.NewGuid(), RegionAssetId = region.Id };
        var line = new TransmissionLine { Id = Guid.NewGuid(), Substation = sub };
        var tower = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var asset = new Asset { Id = Guid.NewGuid(), Tower = tower, Status = "Active" };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);
        var key = "IDEMP-ASSESSMENT-001";

        var first = await service.CreateAsync(region.Id, start, end, new[] { asset.Id }, null, key, CancellationToken.None);
        var second = await service.CreateAsync(region.Id, start, end, new[] { asset.Id }, null, key, CancellationToken.None);

        second.Id.Should().Be(first.Id);
    }

    [Fact]
    public async Task CreateAssessment_DbUpdateExceptionWithSameIdempotencyKey_RecoversGracefully()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var sub = new Substation { Id = Guid.NewGuid(), RegionAssetId = region.Id };
        var line = new TransmissionLine { Id = Guid.NewGuid(), Substation = sub };
        var tower = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var asset = new Asset { Id = Guid.NewGuid(), Tower = tower, Status = "Active" };
        db.Assets.Add(asset);

        var key = "RACE-KEY-001";
        var existing = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            RegionId = region.Id,
            ManagerId = managerId,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            IdempotencyKey = key,
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(existing);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var result = await service.CreateAsync(region.Id, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(1).AddHours(4), new[] { asset.Id }, null, key, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(existing.Id);
    }

    [Fact]
    public async Task Evaluate_DetectsEligiblePersonnel_AndChecksScheduleConflictAndScope()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var roleInspector = new Role { RoleName = UserRoles.Inspector };
        var roleAnalyst = new Role { RoleName = UserRoles.Analyst };
        var roleTech = new Role { RoleName = "Technician" };
        db.Roles.AddRange(roleInspector, roleAnalyst, roleTech);

        var eligibleInspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Inspector Clean" };
        eligibleInspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = eligibleInspector.Id, RegionId = region.Id });

        var eligibleAnalyst = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Analyst Clean" };
        eligibleAnalyst.UserRoles.Add(new UserRole { Role = roleAnalyst });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = eligibleAnalyst.Id, RegionId = region.Id });

        var eligibleTech = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Tech Clean" };
        eligibleTech.UserRoles.Add(new UserRole { Role = roleTech });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = eligibleTech.Id, RegionId = region.Id });

        var busyInspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Inspector Busy" };
        busyInspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = busyInspector.Id, RegionId = region.Id });

        var outOfScopeInspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Inspector OutOfScope" };
        outOfScopeInspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = outOfScopeInspector.Id, RegionId = Guid.NewGuid() }); // Different region

        db.Users.AddRange(eligibleInspector, eligibleAnalyst, eligibleTech, busyInspector, outOfScopeInspector);

        var sub = new Substation { Id = Guid.NewGuid(), RegionAssetId = region.Id };
        var line = new TransmissionLine { Id = Guid.NewGuid(), Substation = sub };
        var tower = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var asset = new Asset { Id = Guid.NewGuid(), Tower = tower, Status = "Active" };
        db.Assets.Add(asset);

        var start = DateTime.UtcNow.AddDays(2);
        var end = start.AddHours(4);

        // Add conflicting booking for busyInspector
        db.ResourceBookings.Add(new ResourceBooking
        {
            UserId = busyInspector.Id,
            StartAt = start.AddHours(-1),
            EndAt = start.AddHours(2),
            Status = ResourceBookingStatus.Active
        });

        // Add an available drone with valid technical inspection
        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        drone.TechnicalInspections.Add(new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            Status = DroneTechnicalInspectionStatus.Passed,
            Health = TechnicalHealth.Healthy,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            ValidUntil = DateTime.UtcNow.AddDays(10)
        });
        db.Uavs.Add(drone);

        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var assessment = await service.CreateAsync(region.Id, start, end, new[] { asset.Id }, CancellationToken.None);

        var evaluated = await service.EvaluateAsync(assessment.Id, CancellationToken.None);

        evaluated.Status.Should().Be(PreMissionAssessmentStatus.Ready);
        evaluated.PersonnelCandidates.Should().HaveCount(5);

        var cleanCand = evaluated.PersonnelCandidates.Single(x => x.UserId == eligibleInspector.Id);
        cleanCand.IsEligible.Should().BeTrue();
        cleanCand.AvailabilityStatus.Should().Be(ResourceAvailabilityStatus.Available);

        var busyCand = evaluated.PersonnelCandidates.Single(x => x.UserId == busyInspector.Id);
        busyCand.IsEligible.Should().BeFalse();
        busyCand.ReasonCode.Should().Be("SCHEDULE_CONFLICT");
        busyCand.AvailabilityStatus.Should().Be(ResourceAvailabilityStatus.Unavailable);

        var outScopeCand = evaluated.PersonnelCandidates.Single(x => x.UserId == outOfScopeInspector.Id);
        outScopeCand.IsEligible.Should().BeFalse();
        outScopeCand.ReasonCode.Should().Be("OUTSIDE_MANAGEMENT_SCOPE");
    }

    [Fact]
    public async Task ReEvaluate_ClearsPreviousCandidates_PreventingDuplicates()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = managerId, RegionId = region.Id });

        var roleInspector = new Role { RoleName = UserRoles.Inspector };
        db.Roles.Add(roleInspector);
        var inspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true };
        inspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = inspector.Id, RegionId = region.Id });
        db.Users.Add(inspector);

        var drone = new Uav { Id = Guid.NewGuid(), UavCode = "DRONE-01", OperationalStatus = DroneOperationalStatus.Available };
        drone.TechnicalInspections.Add(new DroneTechnicalInspection
        {
            DroneId = drone.Id,
            Status = DroneTechnicalInspectionStatus.Passed,
            Health = TechnicalHealth.Healthy,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            ValidUntil = DateTime.UtcNow.AddDays(10)
        });
        db.Uavs.Add(drone);

        var sub = new Substation { Id = Guid.NewGuid(), RegionAssetId = region.Id };
        var line = new TransmissionLine { Id = Guid.NewGuid(), Substation = sub };
        var tower = new Tower { Id = Guid.NewGuid(), TransmissionLine = line };
        var asset = new Asset { Id = Guid.NewGuid(), Tower = tower, Status = "Active" };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(4);
        var assessment = await service.CreateAsync(region.Id, start, end, new[] { asset.Id }, CancellationToken.None);

        await service.EvaluateAsync(assessment.Id, CancellationToken.None);
        var reevaluated = await service.EvaluateAsync(assessment.Id, CancellationToken.None);

        // Candidates must not duplicate
        reevaluated.PersonnelCandidates.Should().HaveCount(1);
        reevaluated.DroneCandidates.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAsync_AutoExpiresAssessment_WhenValidUntilPassed()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddMinutes(-5) // Expired 5 min ago
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var fetched = await service.GetAsync(assessment.Id, CancellationToken.None);

        fetched.Status.Should().Be(PreMissionAssessmentStatus.Expired);
    }

    [Fact]
    public async Task GetAsync_DifferentManagerSameRegionScope_AllowsAccess()
    {
        var creatorManagerId = Guid.NewGuid();
        var shiftManagerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();

        var shiftUser = CreateUserMock(shiftManagerId, UserRoles.Manager);
        await using var db = CreateContext(shiftUser.Object);

        db.Users.Add(new User { Id = creatorManagerId, Status = "Active" });
        db.Users.Add(new User { Id = shiftManagerId, Status = "Active" });
        db.Regions.Add(new Region { Id = regionId, Code = "REG-SCOPE-01" });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = shiftManagerId, RegionId = regionId });

        var assessment = new PreMissionAssessment
        {
            ManagerId = creatorManagerId,
            RegionId = regionId,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, shiftUser.Object);
        var result = await service.GetAsync(assessment.Id, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(assessment.Id);
    }

    [Fact]
    public async Task GetAsync_DifferentManagerDifferentRegionScope_ThrowsForbiddenException()
    {
        var creatorManagerId = Guid.NewGuid();
        var otherManagerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var otherRegionId = Guid.NewGuid();

        var otherUser = CreateUserMock(otherManagerId, UserRoles.Manager);
        await using var db = CreateContext(otherUser.Object);

        db.Users.Add(new User { Id = creatorManagerId, Status = "Active" });
        db.Users.Add(new User { Id = otherManagerId, Status = "Active" });
        db.Regions.Add(new Region { Id = regionId, Code = "REG-SCOPE-02" });
        db.Regions.Add(new Region { Id = otherRegionId, Code = "REG-SCOPE-03" });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = otherManagerId, RegionId = otherRegionId });

        var assessment = new PreMissionAssessment
        {
            ManagerId = creatorManagerId,
            RegionId = regionId,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, otherUser.Object);
        var act = () => service.GetAsync(assessment.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*ASSESSMENT_ACCESS_DENIED*");
    }

    [Fact]
    public async Task MarkCompletedAsync_ValidAssessment_MarksCompletedSuccessfully()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(assessment);

        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MS-VALID-01",
            Title = "Valid Mission",
            PreMissionAssessmentId = assessment.Id
        };
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var result = await service.MarkCompletedAsync(assessment.Id, missionId, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be(PreMissionAssessmentStatus.Completed);
        result.ConsumedByMissionId.Should().Be(missionId);
        result.Version.Should().Be(2);
    }

    [Fact]
    public async Task MarkCompletedAsync_MissionNotFound_ThrowsNotFoundException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var randomMissionId = Guid.NewGuid();

        var act = () => service.MarkCompletedAsync(assessment.Id, randomMissionId, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task MarkCompletedAsync_MissingMissionId_ThrowsBusinessRuleException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var act = () => service.MarkCompletedAsync(assessment.Id, null, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*MISSION_REQUIRED*");
    }

    [Fact]
    public async Task MarkCompletedAsync_DraftAssessment_ThrowsBusinessRuleException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Draft
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var act = () => service.MarkCompletedAsync(assessment.Id, Guid.NewGuid(), CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*INVALID_ASSESSMENT_STATUS*");
    }

    [Fact]
    public async Task MarkCompletedAsync_AlreadyCompleted_ThrowsBusinessRuleException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Completed,
            ConsumedByMissionId = Guid.NewGuid()
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        var act = () => service.MarkCompletedAsync(assessment.Id, Guid.NewGuid(), CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*ASSESSMENT_ALREADY_COMPLETED*");
    }

    [Fact]
    public async Task MarkCompletedAsync_MissionFromDifferentAssessment_ThrowsBusinessRuleException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-02" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.Add(assessment);

        var otherAssessmentId = Guid.NewGuid();
        var otherMission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-OTHER-01",
            Title = "Other Mission",
            PreMissionAssessmentId = otherAssessmentId
        };
        db.Missions.Add(otherMission);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var act = () => service.MarkCompletedAsync(assessment.Id, otherMission.Id, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*INVALID_MISSION_REFERENCE*");
    }

    [Fact]
    public async Task MarkCompletedAsync_ExpiredAssessment_ThrowsBusinessRuleException()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-03" };
        db.Regions.Add(region);

        var assessment = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(-2),
            PlannedEnd = DateTime.UtcNow.AddDays(-1),
            Status = PreMissionAssessmentStatus.Expired
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var act = () => service.MarkCompletedAsync(assessment.Id, null, CancellationToken.None);
        await act.Should().ThrowAsync<BusinessRuleException>().WithMessage("*INVALID_ASSESSMENT_STATUS*");
    }

    [Fact]
    public async Task ListAsync_FilterByStatus_ReturnsOnlyMatchingStatus()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var a1 = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Completed
        };
        var a2 = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.NotReady
        };
        var a3 = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Ready
        };
        db.PreMissionAssessments.AddRange(a1, a2, a3);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        // Filter by COMPLETED
        var completedList = await service.ListAsync("COMPLETED", CancellationToken.None);
        completedList.Should().HaveCount(1);
        completedList.Single().Id.Should().Be(a1.Id);

        // Filter by NOT_READY
        var notReadyList = await service.ListAsync("NOT_READY", CancellationToken.None);
        notReadyList.Should().HaveCount(1);
        notReadyList.Single().Id.Should().Be(a2.Id);
    }

    [Fact]
    public async Task ListAsync_LegacyStatusQuery_MapsConsumedAndIncomplete()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User { Id = managerId, Status = "Active" });
        var region = new Region { Id = Guid.NewGuid(), Code = "REG-01" };
        db.Regions.Add(region);

        var a1 = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.Completed
        };
        var a2 = new PreMissionAssessment
        {
            ManagerId = managerId,
            RegionId = region.Id,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(4),
            Status = PreMissionAssessmentStatus.NotReady
        };
        db.PreMissionAssessments.AddRange(a1, a2);
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);

        // Legacy "CONSUMED" maps to Completed
        var consumedList = await service.ListAsync("CONSUMED", CancellationToken.None);
        consumedList.Should().HaveCount(1);
        consumedList.Single().Id.Should().Be(a1.Id);

        // Legacy "INCOMPLETE" maps to NotReady
        var incompleteList = await service.ListAsync("INCOMPLETE", CancellationToken.None);
        incompleteList.Should().HaveCount(1);
        incompleteList.Single().Id.Should().Be(a2.Id);
    }

    [Theory]
    [InlineData("CONSUMED", PreMissionAssessmentStatus.Completed)]
    [InlineData("consumed", PreMissionAssessmentStatus.Completed)]
    [InlineData("COMPLETED", PreMissionAssessmentStatus.Completed)]
    [InlineData("completed", PreMissionAssessmentStatus.Completed)]
    [InlineData("INCOMPLETE", PreMissionAssessmentStatus.NotReady)]
    [InlineData("incomplete", PreMissionAssessmentStatus.NotReady)]
    [InlineData("NOT_READY", PreMissionAssessmentStatus.NotReady)]
    [InlineData("not_ready", PreMissionAssessmentStatus.NotReady)]
    [InlineData("NOTREADY", PreMissionAssessmentStatus.NotReady)]
    [InlineData("READY", PreMissionAssessmentStatus.Ready)]
    [InlineData("DRAFT", PreMissionAssessmentStatus.Draft)]
    [InlineData("EVALUATING", PreMissionAssessmentStatus.Evaluating)]
    [InlineData("EXPIRED", PreMissionAssessmentStatus.Expired)]
    [InlineData("CANCELLED", PreMissionAssessmentStatus.Cancelled)]
    public void NormalizeAssessmentStatusFilter_NormalizesCorrectly(string input, PreMissionAssessmentStatus expected)
    {
        var result = PreMissionAssessmentService.NormalizeAssessmentStatusFilter(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void NormalizeAssessmentStatusFilter_InvalidString_ReturnsNull()
    {
        var result = PreMissionAssessmentService.NormalizeAssessmentStatusFilter("INVALID_STATUS");
        result.Should().BeNull();
    }
}
