using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Utilities;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Tests.Domain;

public class RevisedDomainAlignmentTests
{
    [Fact]
    public void MissionTarget_ReferencesTower_AndHasUniqueMissionTowerIndex()
    {
        var target = new MissionTarget { MissionId = Guid.NewGuid(), TowerId = Guid.NewGuid() };
        target.TowerId.Should().NotBeEmpty();

        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(MissionTarget));
        entityType!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(p => p.Name)
                .SequenceEqual(new[] { nameof(MissionTarget.MissionId), nameof(MissionTarget.TowerId) }));
    }

    [Fact]
    public void InspectionMedia_ReferencesTower()
    {
        new InspectionMedia { TowerId = Guid.NewGuid() }.TowerId.Should().NotBeEmpty();
    }

    [Fact]
    public void DetectedAnomaly_UsesGuidTowerId_AndOptionalComponent()
    {
        var anomaly = new DetectedAnomaly { TowerId = Guid.NewGuid(), ComponentId = null };
        anomaly.TowerId.Should().NotBeEmpty();
        anomaly.ComponentId.Should().BeNull();
    }

    [Fact]
    public void TowerHealthHistory_UsesTowerId()
    {
        new TowerHealthHistory { TowerId = Guid.NewGuid() }.TowerId.Should().NotBeEmpty();
    }

    [Fact]
    public void MaintenanceTicket_AllowsUnassignedTechnician()
    {
        new MaintenanceTicket { TechnicianId = null }.TechnicianId.Should().BeNull();
    }

    [Fact]
    public void Point_UsesLongitudeForX_AndLatitudeForY()
    {
        var point = SpatialGeometryFactory.CreatePoint(106.80321, 10.84321);
        point.X.Should().Be(106.80321);
        point.Y.Should().Be(10.84321);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, null);
    }
}
