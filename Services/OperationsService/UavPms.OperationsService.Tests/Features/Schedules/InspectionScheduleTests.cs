using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Schedules;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Authorization;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Schedules;

public class InspectionScheduleTests
{
    private static Mock<ICurrentUserServices> CreateAdminMock(Guid userId)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns([UserRoles.SystemAdmin]);
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

    [Theory]
    [InlineData("FREQ=DAILY", true)]
    [InlineData("FREQ=WEEKLY;INTERVAL=1;BYDAY=MO,WE,FR", true)]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=15", true)]
    [InlineData("", false)]
    [InlineData("INVALID_RULE", false)]
    [InlineData("FREQ=WEEKLY;BYDAY=XX", false)]
    public void RecurrenceRuleEvaluator_TryValidate_ShouldValidateCorrectly(string rrule, bool expected)
    {
        RecurrenceRuleEvaluator.TryValidate(rrule, out _).Should().Be(expected);
    }

    [Fact]
    public async Task InspectionScheduleService_CrudAndTrigger_ShouldCreateAssessmentAndAuditLog()
    {
        var adminId = Guid.NewGuid();
        var userMock = CreateAdminMock(adminId);
        await using var db = CreateContext(userMock.Object);

        var region = new Region { Id = Guid.NewGuid(), Code = "HP", Name = "Hai Phong" };
        db.Regions.Add(region);
        db.Towers.AddRange(
            new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = "T-01", Geom = new Point(106.6, 20.8) { SRID = 4326 } },
            new Tower { Id = Guid.NewGuid(), RegionId = region.Id, TowerCode = "T-02", Geom = new Point(106.61, 20.81) { SRID = 4326 } });
        await db.SaveChangesAsync();

        var service = new InspectionScheduleService(
            db, userMock.Object, new GeographicAccessFilter(db, userMock.Object));

        var created = await service.CreateAsync(
            region.Id,
            "Kiểm tra định kỳ thứ 2",
            "FREQ=WEEKLY;BYDAY=MO",
            new TimeOnly(8, 0),
            true,
            CancellationToken.None);

        created.Name.Should().Be("Kiểm tra định kỳ thứ 2");
        created.IsActive.Should().BeTrue();

        var toggled = await service.ToggleActiveAsync(created.Id, false, CancellationToken.None);
        toggled.IsActive.Should().BeFalse();

        var triggerResult = await service.TriggerAsync(created.Id, CancellationToken.None);
        triggerResult.TowerCount.Should().Be(2);

        var assessment = await db.PreMissionAssessments
            .Include(a => a.Towers)
            .SingleAsync(a => a.Id == triggerResult.AssessmentId);
        assessment.Towers.Should().HaveCount(2);

        var audit = await db.AuditLogs.SingleAsync(a => a.TableName == "InspectionSchedules" && a.RecordId == created.Id && a.ActionType == "SCHEDULE_TRIGGERED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task InspectionScheduleRunnerJob_ShouldTriggerDueScheduleOncePerDay()
    {
        var adminId = Guid.NewGuid();
        var userMock = CreateAdminMock(adminId);
        var dbName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddDbContext<ApplicationDbContext>(opt => opt.UseInMemoryDatabase(dbName));
        services.AddSingleton(userMock.Object);
        services.AddScoped<GeographicAccessFilter>();
        services.AddScoped<InspectionScheduleService>();
        await using var provider = services.BuildServiceProvider();

        var regionId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 11, 12, 8, 5, 0, DateTimeKind.Utc);

        using (var seedScope = provider.CreateScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Regions.Add(new Region { Id = regionId, Code = "HN", Name = "Ha Noi" });
            db.Towers.Add(new Tower { Id = Guid.NewGuid(), RegionId = regionId, TowerCode = "HN-01", Geom = new Point(105.8, 21.0) { SRID = 4326 } });
            db.InspectionSchedules.Add(new InspectionSchedule
            {
                Id = Guid.NewGuid(),
                RegionId = regionId,
                Name = "Daily 08:00",
                RecurrenceRule = "FREQ=DAILY",
                PlannedTime = new TimeOnly(8, 0),
                IsActive = true,
                CreatedBy = adminId,
                CreatedAt = utcNow.AddDays(-1)
            });
            await db.SaveChangesAsync();
        }

        var job = new InspectionScheduleRunnerJob(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InspectionScheduleRunnerJob>.Instance);

        var firstRun = await job.RunOnceAsync(utcNow, CancellationToken.None);
        var secondRun = await job.RunOnceAsync(utcNow.AddMinutes(1), CancellationToken.None);

        firstRun.Should().Be(1);
        secondRun.Should().Be(0);
    }
}

