using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;

namespace UavPms.OperationsService.Tests.Features.Gis;

public class GeographicAccessTests
{
    [Theory]
    [InlineData("Inspector")]
    [InlineData("Technician")]
    [InlineData("Admin")]
    [InlineData("Manager")]
    public async Task Assignment_does_not_expose_sibling_towers_or_assets(string role)
    {
        var user = CurrentUser(role);
        await using var context = Context(user.Object);
        var region = new Region { Code = "South" };
        var station = new Substation { Region = region };
        var line = new TransmissionLine { Substation = station };
        var assigned = new Asset { Tower = new Tower { TransmissionLine = line }, PowerLine = line };
        var sibling = new Asset { Tower = new Tower { TransmissionLine = line }, PowerLine = line };
        context.Assets.AddRange(assigned, sibling);
        context.MissionTargets.Add(new MissionTarget { Asset = assigned, Mission = new Mission { InspectorId = user.Object.UserId, Status = MissionStatus.Pending } });
        await context.SaveChangesAsync();
        var access = new GeographicAccessFilter(context, user.Object);
        Assert.Equal(new[] { assigned.Id }, await access.ApplyToAssets(context.Assets).Select(a => a.Id).ToArrayAsync());
        Assert.Equal(new[] { assigned.TowerId }, await access.ApplyToTowers(context.Towers).Select(t => t.Id).ToArrayAsync());
        Assert.Single(await access.ApplyToLines(context.TransmissionLines).ToListAsync());
        // Already tracked entities must not bypass the database predicate through FindAsync.
        Assert.Null(await new GenericRepository<Asset>(context).GetByIdAsync(sibling.Id));
        Assert.Null(await new GenericRepository<Tower>(context).GetByIdAsync(sibling.TowerId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Region_and_management_unit_scopes_allow_only_their_resources(bool organizationScope)
    {
        var user = CurrentUser("Manager");
        await using var context = Context(user.Object);
        var unit = new ManagementUnit { Code = "EVNHCMC" };
        var region = new Region { Code = "South" };
        var allowed = new Asset { ManagementUnit = unit, Tower = new Tower { TransmissionLine = new TransmissionLine { ManagementUnit = unit, Substation = new Substation { Region = region } } } };
        var denied = new Asset { Tower = new Tower { TransmissionLine = new TransmissionLine { Substation = new Substation { Region = new Region { Code = "North" } } } } };
        context.Assets.AddRange(allowed, denied);
        context.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Object.UserId, RegionId = organizationScope ? null : region.Id, ManagementUnitId = organizationScope ? unit.Id : null });
        await context.SaveChangesAsync();
        Assert.Equal(new[] { allowed.Id }, (await new GenericRepository<Asset>(context).GetAllAsync()).Select(a => a.Id));
        Assert.Null(await new GenericRepository<Region>(context).GetByIdAsync(denied.Tower!.TransmissionLine!.Substation!.RegionAssetId));
    }

    [Fact]
    public async Task Completed_assignment_and_missing_authentication_grant_no_access()
    {
        var user = CurrentUser("Inspector");
        await using var context = Context(user.Object);
        var asset = new Asset { Tower = new Tower { TransmissionLine = new TransmissionLine { Substation = new Substation { Region = new Region() } } } };
        context.MissionTargets.Add(new MissionTarget { Asset = asset, Mission = new Mission { InspectorId = user.Object.UserId, Status = MissionStatus.Completed } });
        await context.SaveChangesAsync();
        Assert.Empty(await new GenericRepository<Asset>(context).GetAllAsync());
        user.SetupGet(u => u.Roles).Returns(new[] { "SystemAdmin" });
        Assert.Single(await new GenericRepository<Asset>(context).GetAllAsync());
        user.SetupGet(u => u.IsAuthenticated).Returns(false);
        Assert.Empty(await new GenericRepository<Asset>(context).GetAllAsync());
    }

    [Fact]
    public void Geographic_queries_translate_to_postgres_sql()
    {
        var user = CurrentUser("Inspector");
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only", o => o.UseNetTopologySuite()).Options, user.Object);
        var access = new GeographicAccessFilter(context, user.Object);
        Assert.Contains("UserGeographicScopes", access.ApplyToAssets(context.Assets).ToQueryString());
        Assert.Contains("MissionTargets", access.ApplyToTowers(context.Towers).ToQueryString());
        Assert.Contains("UserGeographicScopes", access.ApplyToRegions(context.Regions).ToQueryString());
    }

    [Fact]
    public async Task Region_without_polygon_filters_by_domain_relationships()
    {
        var user = CurrentUser("Manager");
        await using var context = Context(user.Object);
        var region = new Region { Code = "North" };
        var asset = new Asset { Location = new NetTopologySuite.Geometries.Point(105.9, 18.35) { SRID = 4326 }, Tower = new Tower { TransmissionLine = new TransmissionLine { Substation = new Substation { Region = region } } } };
        context.Assets.Add(asset);
        context.UserGeographicScopes.Add(new UserGeographicScope { UserId = user.Object.UserId, RegionId = region.Id });
        await context.SaveChangesAsync();
        var repository = new GisRepository(context, new GeographicAccessFilter(context, user.Object));
        var response = await repository.GetInfrastructureAsync(new(region.Id, null, null, null, null, null), default);
        Assert.Equal(asset.Id, Assert.Single(response.Assets).Id);
    }

    [Fact]
    public async Task Technician_ticket_access_expires_when_ticket_is_closed()
    {
        var user = CurrentUser("Technician");
        await using var context = Context(user.Object);
        var asset = new Asset { Tower = new Tower { TransmissionLine = new TransmissionLine { Substation = new Substation { Region = new Region() } } } };
        var ticket = new MaintenanceTicket { Asset = asset, TechnicianId = user.Object.UserId };
        context.MaintenanceTickets.Add(ticket);
        await context.SaveChangesAsync();
        var repository = new GenericRepository<Asset>(context);
        Assert.NotNull(await repository.GetByIdAsync(asset.Id));
        ticket.Status = TicketStatus.Closed;
        await context.SaveChangesAsync();
        Assert.Null(await repository.GetByIdAsync(asset.Id));
    }

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
