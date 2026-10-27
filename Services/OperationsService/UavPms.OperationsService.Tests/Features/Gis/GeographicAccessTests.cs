using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Gis.Infrastructure;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;

namespace UavPms.OperationsService.Tests.Features.Gis;

/// <summary>
/// GIS access rule (ERD v2.2): a user sees towers of the regions in their UserGeographicScopes
/// (plus the direct child regions of those regions) OR towers reachable through an active
/// assignment (mission target tower / open maintenance ticket).
/// </summary>
public class GeographicAccessTests
{
    [Theory]
    [InlineData("Inspector")]
    [InlineData("Manager")]
    public async Task Region_scope_includes_scoped_region_and_its_child_regions_but_not_other_regions(string role)
    {
        var user = CurrentUser(role);
        await using var context = Context(user.Object);
        var parent = new Region { Id = Guid.NewGuid(), Code = "EVNSPC", Name = "Southern" };
        var child = new Region { Id = Guid.NewGuid(), Code = "EVNSPC-CAMAU", Name = "Ca Mau", ParentId = parent.Id };
        var other = new Region { Id = Guid.NewGuid(), Code = "EVNNPC", Name = "Northern" };
        context.Regions.AddRange(parent, child, other);

        var parentTower = NewTower(parent.Id, "T-PARENT");
        var childTower = NewTower(child.Id, "T-CHILD");
        var otherTower = NewTower(other.Id, "T-OTHER");
        context.Towers.AddRange(parentTower, childTower, otherTower);
        context.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Object.UserId, RegionId = parent.Id });
        await context.SaveChangesAsync();

        var access = new GeographicAccessFilter(context, user.Object);
        var visibleTowers = await access.ApplyToTowers(context.Towers).Select(t => t.Id).ToListAsync();
        Assert.Equal(new[] { parentTower.Id, childTower.Id }.OrderBy(x => x), visibleTowers.OrderBy(x => x));

        var visibleRegions = await access.ApplyToRegions(context.Regions).Select(r => r.Id).ToListAsync();
        Assert.Equal(new[] { parent.Id, child.Id }.OrderBy(x => x), visibleRegions.OrderBy(x => x));

        // Generic repository reads (including tracked direct-id reads) go through the same predicate.
        Assert.NotNull(await new GenericRepository<Tower>(context).GetByIdAsync(childTower.Id));
        Assert.Null(await new GenericRepository<Tower>(context).GetByIdAsync(otherTower.Id));
        Assert.Null(await new GenericRepository<Region>(context).GetByIdAsync(other.Id));
    }

    [Fact]
    public async Task Child_region_scope_does_not_expose_parent_or_sibling_regions()
    {
        var user = CurrentUser("Inspector");
        await using var context = Context(user.Object);
        var parent = new Region { Id = Guid.NewGuid(), Code = "EVNSPC", Name = "Southern" };
        var child = new Region { Id = Guid.NewGuid(), Code = "EVNSPC-CAMAU", ParentId = parent.Id };
        var sibling = new Region { Id = Guid.NewGuid(), Code = "EVNSPC-DONGNAI", ParentId = parent.Id };
        context.Regions.AddRange(parent, child, sibling);
        var childTower = NewTower(child.Id, "T-CHILD");
        context.Towers.AddRange(NewTower(parent.Id, "T-PARENT"), childTower, NewTower(sibling.Id, "T-SIBLING"));
        context.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Object.UserId, RegionId = child.Id });
        await context.SaveChangesAsync();

        var access = new GeographicAccessFilter(context, user.Object);
        Assert.Equal(new[] { childTower.Id }, await access.ApplyToTowers(context.Towers).Select(t => t.Id).ToArrayAsync());
    }

    [Theory]
    [InlineData("INSPECTOR")]
    [InlineData("PILOT")]
    public async Task Active_mission_assignment_exposes_only_target_towers_and_their_region(string assignmentRole)
    {
        var user = CurrentUser("Inspector");
        await using var context = Context(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "EVNSPC-CAMAU" };
        var otherRegion = new Region { Id = Guid.NewGuid(), Code = "EVNSPC-DONGNAI" };
        context.Regions.AddRange(region, otherRegion);
        var assigned = NewTower(region.Id, "T-ASSIGNED");
        var sibling = NewTower(region.Id, "T-SIBLING");
        context.Towers.AddRange(assigned, sibling, NewTower(otherRegion.Id, "T-OTHER"));

        var mission = new Mission { Id = Guid.NewGuid(), Status = MissionStatus.Assigned };
        mission.Assignments.Add(new MissionAssignment { UserId = user.Object.UserId, AssignmentRole = assignmentRole, Status = MissionAssignmentStatus.Active });
        mission.MissionTargets.Add(new MissionTarget { TowerId = assigned.Id, Sequence = 1 });
        context.Missions.Add(mission);
        await context.SaveChangesAsync();

        var access = new GeographicAccessFilter(context, user.Object);
        Assert.Equal(new[] { assigned.Id }, await access.ApplyToTowers(context.Towers).Select(t => t.Id).ToArrayAsync());
        Assert.Equal(new[] { region.Id }, await access.ApplyToRegions(context.Regions).Select(r => r.Id).ToArrayAsync());
        // Tracked entities must not bypass the predicate through the repository.
        Assert.Null(await new GenericRepository<Tower>(context).GetByIdAsync(sibling.Id));
    }

    [Fact]
    public async Task Ended_assignment_completed_mission_and_missing_authentication_grant_no_access()
    {
        var user = CurrentUser("Inspector");
        await using var context = Context(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "R" };
        context.Regions.Add(region);
        var tower = NewTower(region.Id, "T-1");
        var tower2 = NewTower(region.Id, "T-2");
        context.Towers.AddRange(tower, tower2);

        var completed = new Mission { Id = Guid.NewGuid(), Status = MissionStatus.Completed };
        completed.Assignments.Add(new MissionAssignment { UserId = user.Object.UserId, AssignmentRole = "INSPECTOR" });
        completed.MissionTargets.Add(new MissionTarget { TowerId = tower.Id });
        var replaced = new Mission { Id = Guid.NewGuid(), Status = MissionStatus.Assigned };
        replaced.Assignments.Add(new MissionAssignment { UserId = user.Object.UserId, AssignmentRole = "INSPECTOR", Status = MissionAssignmentStatus.Revoked });
        replaced.MissionTargets.Add(new MissionTarget { TowerId = tower2.Id });
        context.Missions.AddRange(completed, replaced);
        await context.SaveChangesAsync();

        Assert.Empty(await new GenericRepository<Tower>(context).GetAllAsync());

        user.SetupGet(u => u.Roles).Returns(new[] { "SystemAdmin" });
        Assert.Equal(2, (await new GenericRepository<Tower>(context).GetAllAsync()).Count);

        user.SetupGet(u => u.IsAuthenticated).Returns(false);
        Assert.Empty(await new GenericRepository<Tower>(context).GetAllAsync());
    }

    [Fact]
    public async Task Technician_ticket_access_follows_anomaly_tower_and_expires_when_ticket_is_closed()
    {
        var user = CurrentUser("Technician");
        await using var context = Context(user.Object);
        var region = new Region { Id = Guid.NewGuid(), Code = "R" };
        context.Regions.Add(region);
        var tower = NewTower(region.Id, "T-1");
        context.Towers.Add(tower);
        var anomaly = new DetectedAnomaly { Id = Guid.NewGuid(), TowerId = tower.Id, ValidationStatus = "Confirmed" };
        context.DetectedAnomalies.Add(anomaly);
        var ticket = new MaintenanceTicket { Id = Guid.NewGuid(), AnomalyId = anomaly.Id, TechnicianId = user.Object.UserId, Status = TicketStatus.Open };
        context.MaintenanceTickets.Add(ticket);
        await context.SaveChangesAsync();

        var repository = new GenericRepository<Tower>(context);
        Assert.NotNull(await repository.GetByIdAsync(tower.Id));

        ticket.Status = TicketStatus.Closed;
        await context.SaveChangesAsync();
        Assert.Null(await repository.GetByIdAsync(tower.Id));
    }

    [Fact]
    public async Task Gis_infrastructure_by_region_returns_towers_of_region_and_child_regions()
    {
        var user = CurrentUser("Manager");
        await using var context = Context(user.Object);
        var parent = new Region { Id = Guid.NewGuid(), Code = "EVNSPC" };
        var child = new Region { Id = Guid.NewGuid(), Code = "EVNSPC-NINHTHUAN", ParentId = parent.Id };
        var other = new Region { Id = Guid.NewGuid(), Code = "EVNNPC" };
        context.Regions.AddRange(parent, child, other);
        var parentTower = NewTower(parent.Id, "T-P", new Point(105.9, 18.35) { SRID = 4326 });
        var childTower = NewTower(child.Id, "T-C", new Point(105.8, 18.30) { SRID = 4326 });
        var noGeom = NewTower(child.Id, "T-NOGEOM");
        context.Towers.AddRange(parentTower, childTower, noGeom, NewTower(other.Id, "T-O", new Point(105.0, 21.0) { SRID = 4326 }));
        context.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Object.UserId, RegionId = parent.Id });
        await context.SaveChangesAsync();

        var repository = new GisRepository(context, new GeographicAccessFilter(context, user.Object));
        var response = await repository.GetInfrastructureAsync(new GisInfrastructureQuery(parent.Id), default);

        Assert.Equal(new[] { childTower.Id, parentTower.Id }.OrderBy(x => x), response.Towers.Select(t => t.Id).OrderBy(x => x));
        var dto = response.Towers.Single(t => t.Id == parentTower.Id);
        Assert.Equal(18.35, dto.Latitude);
        Assert.Equal(105.9, dto.Longitude);
    }

    [Fact]
    public void Geographic_queries_translate_to_postgres_sql()
    {
        var user = CurrentUser("Inspector");
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only", o => o.UseNetTopologySuite()).Options, user.Object);
        var access = new GeographicAccessFilter(context, user.Object);
        var towerSql = access.ApplyToTowers(context.Towers).ToQueryString();
        Assert.Contains("UserGeographicScopes", towerSql);
        Assert.Contains("MissionTargets", towerSql);
        Assert.Contains("UserGeographicScopes", access.ApplyToRegions(context.Regions).ToQueryString());
    }

    private static Tower NewTower(Guid regionId, string code, Point? geom = null) =>
        new() { Id = Guid.NewGuid(), RegionId = regionId, TowerCode = code, Geom = geom };

    private static Mock<ICurrentUserServices> CurrentUser(string role)
    {
        var user = new Mock<ICurrentUserServices>();
        user.SetupGet(u => u.UserId).Returns(Guid.NewGuid());
        user.SetupGet(u => u.IsAuthenticated).Returns(true);
        user.SetupGet(u => u.Roles).Returns(new[] { role });
        return user;
    }

    private static ApplicationDbContext Context(ICurrentUserServices user) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user);
}
