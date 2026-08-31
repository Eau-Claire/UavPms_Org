using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;

namespace UavPms.OperationsService.Tests.Features.AssetComponents;

public class AssetHealthDashboardRepositoryTests
{
    [Fact]
    public async Task GetAssetComponentsPagedAsync_ShouldFilterByRiskRegionLineAndSortByHealthScore()
    {
        await using var context = CreateContext();
        var (regionId, lineId) = SeedAssetDashboardData(context);
        var repository = new AssetComponentRepository(context);

        var (items, totalCount) = await repository.GetAssetComponentsPagedAsync(
            page: 1,
            pageSize: 10,
            towerId: null,
            assetType: "Insulator",
            status: "Operational",
            riskLevels: new[] { "Critical Risk", "High Risk" },
            minHealthScore: 0,
            maxHealthScore: 50,
            regionId: regionId,
            lineId: lineId,
            sortBy: "healthScore",
            sortOrder: "asc");

        totalCount.Should().Be(2);
        items.Select(i => i.ComponentCode).Should().Equal("INS-CRIT-01", "INS-HIGH-01");
    }

    [Fact]
    public async Task GetAssetHealthSummaryAsync_ShouldReturnRiskCountsAverageAndCriticalAssets()
    {
        await using var context = CreateContext();
        SeedAssetDashboardData(context);
        var repository = new AssetComponentRepository(context);

        var summary = await repository.GetAssetHealthSummaryAsync(CancellationToken.None);

        summary.TotalAssets.Should().Be(4);
        summary.AverageHealthScore.Should().Be(56.88);
        summary.CriticalRiskCount.Should().Be(1);
        summary.HighRiskCount.Should().Be(1);
        summary.MediumRiskCount.Should().Be(1);
        summary.LowRiskCount.Should().Be(1);
        summary.CriticalAssets.Should().ContainSingle();
        summary.CriticalAssets[0].AssetCode.Should().Be("INS-CRIT-01");
        summary.CriticalAssets[0].DefectCount.Should().Be(1);
    }

    private static (Guid RegionId, Guid LineId) SeedAssetDashboardData(ApplicationDbContext context)
    {
        var regionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var categoryId = 1;

        var region = new Region { Id = regionId, RegionName = "North" };
        var substation = new Substation
        {
            Id = Guid.NewGuid(),
            RegionAssetId = regionId,
            SubstationName = "Substation A",
            Region = region
        };
        var line = new TransmissionLine
        {
            Id = lineId,
            SubstationAssetId = substation.Id,
            LineName = "Line 500kV",
            Substation = substation
        };
        var tower = new Tower
        {
            Id = towerId,
            LineAssetId = lineId,
            TowerCode = "TOW-N1-01",
            TransmissionLine = line
        };
        var media = new InspectionMedia
        {
            Id = mediaId,
            MissionId = Guid.NewGuid(),
            TowerId = towerId,
            MediaType = "Image",
            FileUrl = "https://example.test/image.jpg",
            Tower = tower
        };
        var category = new DefectCategory
        {
            Id = categoryId,
            CategoryCode = "CRACK",
            CategoryName = "Crack",
            Description = "Crack",
            SeverityWeight = 0.9
        };

        var critical = new AssetComponent
        {
            Id = Guid.NewGuid(),
            TowerId = towerId,
            Tower = tower,
            ComponentType = "Insulator",
            ComponentCode = "INS-CRIT-01",
            Status = "Operational",
            CurrentHealthScore = 25,
            RiskLevel = "Critical Risk",
            LastInspectedAt = DateTime.UtcNow.AddDays(-1)
        };
        critical.DetectedAnomalies.Add(new DetectedAnomaly
        {
            Id = Guid.NewGuid(),
            MediaId = mediaId,
            Media = media,
            TowerId = towerId,
            Tower = tower,
            ComponentId = critical.Id,
            Component = critical,
            CategoryId = categoryId,
            Category = category,
            BoundingBox = "{}",
            ConfidenceScore = 0.91,
            ValidationStatus = "Confirmed",
            AiSource = "Mock"
        });

        context.AddRange(
            region,
            substation,
            line,
            tower,
            media,
            category,
            critical,
            new AssetComponent
            {
                Id = Guid.NewGuid(),
                TowerId = towerId,
                Tower = tower,
                ComponentType = "Insulator",
                ComponentCode = "INS-HIGH-01",
                Status = "Operational",
                CurrentHealthScore = 45,
                RiskLevel = "High Risk"
            },
            new AssetComponent
            {
                Id = Guid.NewGuid(),
                TowerId = towerId,
                Tower = tower,
                ComponentType = "Cable",
                ComponentCode = "CBL-MED-01",
                Status = "Operational",
                CurrentHealthScore = 67.5,
                RiskLevel = "Medium Risk"
            },
            new AssetComponent
            {
                Id = Guid.NewGuid(),
                TowerId = towerId,
                Tower = tower,
                ComponentType = "Vibration Damper",
                ComponentCode = "DMP-LOW-01",
                Status = "Operational",
                CurrentHealthScore = 90,
                RiskLevel = "Low Risk"
            });
        context.SaveChanges();

        return (regionId, lineId);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());
    }
}
