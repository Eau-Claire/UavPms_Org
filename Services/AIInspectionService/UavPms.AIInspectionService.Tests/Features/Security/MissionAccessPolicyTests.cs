using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.AIInspectionService.Application.Common.Security;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.AIInspectionService.Infrastructure.Persistence;
using UavPms.AIInspectionService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.AIInspectionService.Tests.Features.Security;

/// <summary>
/// Spec §4: defect gắn với nhiệm vụ. Xem: SystemAdmin, Manager phụ trách, thành viên được giao, Manager/Analyst của vùng.
/// Duyệt: SystemAdmin, Analyst được giao hoặc Analyst của vùng. Upload: Inspector/Analyst được giao, Analyst của vùng;
/// standalone chỉ SystemAdmin và Analyst; Manager không upload.
/// </summary>
public class MissionAccessPolicyTests
{
    private readonly Guid _regionId = Guid.NewGuid();
    private readonly Guid _childRegionId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            Mock.Of<ICurrentUserServices>());

    private static IMissionAccessPolicy Policy(ApplicationDbContext db, Guid userId, params string[] roles)
    {
        var user = new Mock<ICurrentUserServices>();
        user.SetupGet(u => u.UserId).Returns(userId);
        user.SetupGet(u => u.Roles).Returns(roles);
        return new MissionAccessPolicy(
            new GenericRepository<MissionAssignment>(db),
            new GenericRepository<UserGeographicScope>(db),
            new GenericRepository<Region>(db),
            user.Object);
    }

    private Mission NewMission(Guid? regionId) => new() { Id = Guid.NewGuid(), MissionCode = Guid.NewGuid().ToString("N"), RegionId = regionId, ManagerId = Guid.NewGuid() };

    private async Task AssignAsync(ApplicationDbContext db, Mission mission, string role)
    {
        db.MissionAssignments.Add(new MissionAssignment { MissionId = mission.Id, UserId = _userId, AssignmentRole = role });
        await db.SaveChangesAsync();
    }

    private async Task GrantRegionAsync(ApplicationDbContext db)
    {
        db.UserGeographicScopes.Add(new UserGeographicScope { UserId = _userId, RegionId = _regionId });
        db.Regions.Add(new Region { Id = _childRegionId, ParentId = _regionId });
        await db.SaveChangesAsync();
    }

    [Theory]
    [InlineData(UserRoles.Technician, "TECHNICIAN")]
    [InlineData(UserRoles.Inspector, "INSPECTOR")]
    public async Task AssignedFieldMember_CanView_ButNotOtherMissionsInTheirRegion(string role, string assignmentRole)
    {
        await using var db = CreateContext();
        await GrantRegionAsync(db);
        var assigned = NewMission(_regionId);
        var sameRegion = NewMission(_regionId);
        await AssignAsync(db, assigned, assignmentRole);
        var policy = Policy(db, _userId, role);

        (await policy.CanViewAsync(assigned)).Should().BeTrue();
        (await policy.CanViewAsync(sameRegion)).Should().BeFalse();
        (await policy.CanReviewAsync(assigned)).Should().BeFalse();
    }

    [Fact]
    public async Task RegionAnalyst_CanViewAndReview_IncludingChildRegion_ButNotOtherRegions()
    {
        await using var db = CreateContext();
        await GrantRegionAsync(db);
        var policy = Policy(db, _userId, UserRoles.Analyst);

        (await policy.CanReviewAsync(NewMission(_regionId))).Should().BeTrue();
        (await policy.CanReviewAsync(NewMission(_childRegionId))).Should().BeTrue();
        (await policy.CanViewAsync(NewMission(Guid.NewGuid()))).Should().BeFalse();
        (await policy.CanReviewAsync(NewMission(Guid.NewGuid()))).Should().BeFalse();
    }

    [Fact]
    public async Task RegionManager_CanView_ButCannotReviewOrUpload()
    {
        await using var db = CreateContext();
        await GrantRegionAsync(db);
        var mission = NewMission(_regionId);
        var policy = Policy(db, _userId, UserRoles.Manager);

        (await policy.CanViewAsync(mission)).Should().BeTrue();
        (await policy.CanReviewAsync(mission)).Should().BeFalse();
        (await policy.CanUploadAsync(mission)).Should().BeFalse();
        (await policy.CanUploadAsync(null)).Should().BeFalse();
    }

    [Fact]
    public async Task Upload_AssignedInspector_RegionAnalyst_AndStandaloneOnlyForAnalyst()
    {
        await using var db = CreateContext();
        await GrantRegionAsync(db);
        var mission = NewMission(Guid.NewGuid());
        await AssignAsync(db, mission, "INSPECTOR");

        (await Policy(db, _userId, UserRoles.Inspector).CanUploadAsync(mission)).Should().BeTrue();
        (await Policy(db, _userId, UserRoles.Inspector).CanUploadAsync(null)).Should().BeFalse();
        (await Policy(db, _userId, UserRoles.Analyst).CanUploadAsync(NewMission(_regionId))).Should().BeTrue();
        (await Policy(db, _userId, UserRoles.Analyst).CanUploadAsync(null)).Should().BeTrue();
        (await Policy(db, _userId, UserRoles.Technician).CanUploadAsync(mission)).Should().BeFalse();
    }

    [Fact]
    public async Task SystemAdmin_HasEveryRight_AndSeesEverything()
    {
        await using var db = CreateContext();
        var policy = Policy(db, _userId, UserRoles.SystemAdmin);
        var mission = NewMission(Guid.NewGuid());

        (await policy.CanViewAsync(mission)).Should().BeTrue();
        (await policy.CanReviewAsync(mission)).Should().BeTrue();
        (await policy.CanUploadAsync(null)).Should().BeTrue();
        (await policy.GetVisibilityAsync()).All.Should().BeTrue();
    }

    [Fact]
    public async Task Visibility_RegionsOnlyForManagerAndAnalyst()
    {
        await using var db = CreateContext();
        await GrantRegionAsync(db);

        (await Policy(db, _userId, UserRoles.Analyst).GetVisibilityAsync()).RegionIds
            .Should().BeEquivalentTo(new[] { _regionId, _childRegionId });
        (await Policy(db, _userId, UserRoles.Technician).GetVisibilityAsync()).RegionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task AnomalyList_IsFilteredInSqlByVisibility()
    {
        await using var db = CreateContext();
        await GrantRegionAsync(db);
        var inRegion = NewMission(_regionId);
        var otherRegion = NewMission(Guid.NewGuid());
        // Repository Include các quan hệ bắt buộc (Category) nên seed đủ bản ghi liên kết.
        var category = new DefectCategory { Id = 1, CategoryCode = "BIRD_NEST", CategoryName = "Bird nest" };
        db.DefectCategories.Add(category);
        db.Missions.AddRange(inRegion, otherRegion);
        foreach (var mission in new[] { inRegion, otherRegion })
        {
            var media = new InspectionMedia { Id = Guid.NewGuid(), MissionId = mission.Id, Mission = mission, MediaType = "Image", FileUrl = "x" };
            db.InspectionMedia.Add(media);
            db.DetectedAnomalies.Add(new DetectedAnomaly
            {
                Id = Guid.NewGuid(), MediaId = media.Id, Media = media, CategoryId = category.Id,
                BoundingBox = "{}", ValidationStatus = DetectedAnomaly.StatusPending
            });
        }
        await db.SaveChangesAsync();

        var visibility = await Policy(db, _userId, UserRoles.Analyst).GetVisibilityAsync();
        var anomalies = await new AnomalyRepository(db).GetPendingWithDetailsAsync(visibility);

        anomalies.Should().ContainSingle().Which.Media!.MissionId.Should().Be(inRegion.Id);
    }
}
