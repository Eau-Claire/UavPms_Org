using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.Commands.ConfirmAssessment;
using UavPms.OperationsService.Application.Features.Assessments.Commands.CreatePreMissionAssessment;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Assessments.Queries.EvaluateResourceReadiness;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Assessments;

public class Phase2AssessmentCqrsTests
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
    public async Task CreatePreMissionAssessmentCommandHandler_DelegatesAndReturnsDto()
    {
        var managerId = Guid.NewGuid();
        var regionId = Guid.NewGuid();
        var towerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        db.Users.Add(new User
        {
            Id = managerId,
            FullName = "Manager One",
            Email = "manager@example.com",
            Status = "Active"
        });

        db.UserGeographicScopes.Add(new UserGeographicScope
        {
            Id = Guid.NewGuid(),
            UserId = managerId,
            RegionId = regionId
        });

        var region = new Region { Id = regionId, Code = "REG-01", Name = "Region 1" };
        db.Regions.Add(region);
        db.Towers.Add(new Tower { Id = towerId, RegionId = regionId, TowerCode = "T-01" });
        await db.SaveChangesAsync();

        var service = new PreMissionAssessmentService(db, user.Object);
        var handler = new CreatePreMissionAssessmentCommandHandler(service);

        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddHours(3);
        var command = new CreatePreMissionAssessmentCommand(regionId, start, end, new[] { towerId });

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.RegionId.Should().Be(regionId);
        result.Status.Should().Be("NOT_READY");
    }

    [Fact]
    public async Task EvaluateResourceReadinessQueryHandler_ReturnsCandidateSummary()
    {
        var managerId = Guid.NewGuid();
        var user = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(user.Object);

        var assessmentId = Guid.NewGuid();
        var assessment = new PreMissionAssessment
        {
            Id = assessmentId,
            ManagerId = managerId,
            RegionId = Guid.NewGuid(),
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(3),
            Status = PreMissionAssessmentStatus.Ready,
            SiteFeasibilityStatus = ReadinessCheckStatus.Passed,
            OverallTechnicalHealth = TechnicalHealth.Healthy
        };
        db.PreMissionAssessments.Add(assessment);
        await db.SaveChangesAsync();

        var serviceMock = new Mock<IPreMissionAssessmentService>();
        serviceMock.Setup(s => s.EvaluateAsync(assessmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assessment);

        var handler = new EvaluateResourceReadinessQueryHandler(serviceMock.Object);
        var query = new EvaluateResourceReadinessQuery(assessmentId);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.AssessmentId.Should().Be(assessmentId);
        result.IsReady.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAssessmentCommandHandler_ThrowsWhenNotReady()
    {
        var assessmentId = Guid.NewGuid();
        var assessment = new PreMissionAssessment
        {
            Id = assessmentId,
            Status = PreMissionAssessmentStatus.NotReady
        };

        var serviceMock = new Mock<IPreMissionAssessmentService>();
        serviceMock.Setup(s => s.GetAsync(assessmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assessment);

        var handler = new ConfirmAssessmentCommandHandler(serviceMock.Object);
        var command = new ConfirmAssessmentCommand(assessmentId);

        var act = async () => await handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*ASSESSMENT_NOT_READY*");
    }

    [Fact]
    public async Task ConfirmAssessmentCommandHandler_ConfirmsWhenReady()
    {
        var assessmentId = Guid.NewGuid();
        var assessment = new PreMissionAssessment
        {
            Id = assessmentId,
            Status = PreMissionAssessmentStatus.Ready,
            PlannedStart = DateTime.UtcNow.AddDays(1),
            PlannedEnd = DateTime.UtcNow.AddDays(1).AddHours(2)
        };

        var serviceMock = new Mock<IPreMissionAssessmentService>();
        serviceMock.Setup(s => s.GetAsync(assessmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(assessment);

        var handler = new ConfirmAssessmentCommandHandler(serviceMock.Object);
        var command = new ConfirmAssessmentCommand(assessmentId);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(assessmentId);
        result.Status.Should().Be("READY");
    }
}
