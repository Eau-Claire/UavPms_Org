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
        result.Status.Should().Be(PreMissionAssessmentStatus.Evaluating);
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
        db.Roles.Add(roleInspector);

        var eligibleInspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Inspector Clean" };
        eligibleInspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = eligibleInspector.Id, RegionId = region.Id });

        var busyInspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Inspector Busy" };
        busyInspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = busyInspector.Id, RegionId = region.Id });

        var outOfScopeInspector = new User { Id = Guid.NewGuid(), Status = "Active", IsEmailVerified = true, FullName = "Inspector OutOfScope" };
        outOfScopeInspector.UserRoles.Add(new UserRole { Role = roleInspector });
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = outOfScopeInspector.Id, RegionId = Guid.NewGuid() }); // Different region

        db.Users.AddRange(eligibleInspector, busyInspector, outOfScopeInspector);

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
        evaluated.PersonnelCandidates.Should().HaveCount(3);

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
}
