using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using UavPms.NotificationService.Domain.Entities;
using UavPms.NotificationService.Infrastructure.Persistence;
using UavPms.NotificationService.Infrastructure.Repositories;

namespace UavPms.NotificationService.Tests.Features.Monitor;

public class MonitorRepositoryTests
{
    private static ApplicationDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, null);
    }

    [Fact]
    public async Task GetMissionStatusOverviewAndSummary_ShouldCountMissionsByStatusExcludingDeleted()
    {
        await using var context = CreateInMemoryContext();
        context.Missions.AddRange(
            new Mission { Id = Guid.NewGuid(), Title = "Draft", Status = "Draft", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "Assigned", Status = "Assigned", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "InProgress", Status = "InProgress", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "Suspended", Status = "Suspended", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "Completed 1", Status = "Completed", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "Completed 2", Status = "Completed", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "Cancelled", Status = "Cancelled", IsDeleted = false },
            new Mission { Id = Guid.NewGuid(), Title = "Deleted", Status = "Completed", IsDeleted = true }
        );
        await context.SaveChangesAsync();

        var repo = new MonitorRepository(context);

        var overview = await repo.GetMissionStatusOverviewAsync(CancellationToken.None);
        var summary = await repo.GetSummaryAsync(CancellationToken.None);

        overview.Pending.Should().Be(2);
        overview.InProgress.Should().Be(2);
        overview.Completed.Should().Be(2);

        summary.PendingMissions.Should().Be(2);
        summary.InProgressMissions.Should().Be(2);
        summary.CompletedMissions.Should().Be(2);
        summary.TotalMissions.Should().Be(7);
    }

    [Fact]
    public async Task NonRelationalFallback_ShouldReturnEmptyCrossTableResultsSafely()
    {
        await using var context = CreateInMemoryContext();
        var repo = new MonitorRepository(context);

        var stats = await repo.GetDefectStatisticsAsync(CancellationToken.None);
        var (recentItems, recentTotal) = await repo.GetRecentDefectsAsync(1, 10, CancellationToken.None);
        var (historyItems, historyTotal) = await repo.GetInspectionHistoryAsync(null, null, null, null, 1, 10, CancellationToken.None);

        stats.TotalDefects.Should().Be(0);
        stats.ByType.Should().BeEmpty();
        recentItems.Should().BeEmpty();
        recentTotal.Should().Be(0);
        historyItems.Should().BeEmpty();
        historyTotal.Should().Be(0);
    }
}
