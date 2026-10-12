using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class PreMissionAssessmentExpiryJobTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var userMock = new Mock<ICurrentUserServices>();
        return new ApplicationDbContext(options, userMock.Object);
    }

    [Fact]
    public async Task SweepExpiredAssessments_MarksOverdueReadyAssessments_AsExpired()
    {
        var overdueReady = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddMinutes(-5),
            PlannedStart = DateTime.UtcNow.AddHours(1),
            PlannedEnd = DateTime.UtcNow.AddHours(3)
        };

        var activeReady = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddHours(2),
            PlannedStart = DateTime.UtcNow.AddHours(3),
            PlannedEnd = DateTime.UtcNow.AddHours(5)
        };

        var overdueDraft = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            Status = PreMissionAssessmentStatus.Draft,
            ValidUntil = DateTime.UtcNow.AddMinutes(-5),
            PlannedStart = DateTime.UtcNow.AddHours(1),
            PlannedEnd = DateTime.UtcNow.AddHours(3)
        };

        var overdueDeleted = new PreMissionAssessment
        {
            Id = Guid.NewGuid(),
            Status = PreMissionAssessmentStatus.Ready,
            ValidUntil = DateTime.UtcNow.AddMinutes(-5),
            PlannedStart = DateTime.UtcNow.AddHours(1),
            PlannedEnd = DateTime.UtcNow.AddHours(3),
            IsDeleted = true
        };

        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var userMock = new Mock<ICurrentUserServices>();

        await using (var db = new ApplicationDbContext(options, userMock.Object))
        {
            db.PreMissionAssessments.AddRange(overdueReady, activeReady, overdueDraft, overdueDeleted);
            await db.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped(_ => userMock.Object);
        var provider = services.BuildServiceProvider();

        var job = new PreMissionAssessmentExpiryJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PreMissionAssessmentExpiryJob>.Instance);

        // Act
        var expiredCount = await job.SweepExpiredAssessmentsAsync(CancellationToken.None);

        // Assert
        expiredCount.Should().Be(1);

        await using var verifyDb = new ApplicationDbContext(options, userMock.Object);
        var updatedOverdue = await verifyDb.PreMissionAssessments.FindAsync(overdueReady.Id);
        updatedOverdue!.Status.Should().Be(PreMissionAssessmentStatus.Expired);

        var unaffectedActive = await verifyDb.PreMissionAssessments.FindAsync(activeReady.Id);
        unaffectedActive!.Status.Should().Be(PreMissionAssessmentStatus.Ready);

        var unaffectedDraft = await verifyDb.PreMissionAssessments.FindAsync(overdueDraft.Id);
        unaffectedDraft!.Status.Should().Be(PreMissionAssessmentStatus.Draft);
    }
}
