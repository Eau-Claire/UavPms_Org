using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Missions;
using UavPms.OperationsService.Application.Features.Missions.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Missions;

public class MissionIncidentReportTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<ICurrentUserServices> _currentMock;
    private readonly MissionLifecycleService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _managerId = Guid.NewGuid();
    private readonly Guid _regionId = Guid.NewGuid();
    private readonly Guid _assetId = Guid.NewGuid();

    public MissionIncidentReportTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"IncidentReportTests_{Guid.NewGuid()}")
            .Options;

        _currentMock = new Mock<ICurrentUserServices>();
        _currentMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentMock.Setup(c => c.UserId).Returns(_userId);
        _currentMock.Setup(c => c.Roles).Returns(new[] { UserRoles.Inspector });

        _db = new ApplicationDbContext(options, _currentMock.Object);

        _service = new MissionLifecycleService(_db, _currentMock.Object);

        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "inspector@test.com",
            FullName = "Field Inspector",
            Status = "Active"
        });

        _db.Users.Add(new User
        {
            Id = _managerId,
            Email = "manager@test.com",
            FullName = "Mission Manager",
            Status = "Active"
        });

        _db.Regions.Add(new Region
        {
            Id = _regionId,
            RegionName = "Central Highlands Area",
            IsDeleted = false
        });

        _db.UserGeographicScopes.Add(new UserGeographicScope
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            RegionId = _regionId
        });

        _db.Assets.Add(new Asset
        {
            Id = _assetId,
            AssetCode = "TWR-01-INS-01",
            AssetType = "Insulator",
            Status = "Active"
        });

        _db.SaveChanges();
    }

    [Fact]
    public async Task SubmitIncidentReport_ShouldSucceed_WhenInputIsValid()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-INC-01",
            Title = "Mission Incident Test",
            RegionId = _regionId,
            ManagerId = _managerId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        mission.MissionTargets.Add(new MissionTarget { MissionId = missionId, AssetId = _assetId });
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var request = new SubmitIncidentReportRequest(
            IncidentType: "VegetationEncroachment",
            Severity: "Medium",
            Description: "Dense bamboo tree growing into line corridor span 4-5.",
            AssetId: _assetId,
            FileUrl: "https://storage.local/incidents/photo1.jpg"
        );

        // Act
        var incident = await _service.SubmitIncidentReportAsync(missionId, request, CancellationToken.None);

        // Assert
        incident.Should().NotBeNull();
        incident.MissionId.Should().Be(missionId);
        incident.ReportedBy.Should().Be(_userId);
        incident.AssetId.Should().Be(_assetId);
        incident.IncidentType.Should().Be("VegetationEncroachment");
        incident.Severity.Should().Be("Medium");
        incident.Description.Should().Be("Dense bamboo tree growing into line corridor span 4-5.");
        incident.Status.Should().Be("Reported");

        var persisted = await _db.IncidentReports.FirstOrDefaultAsync(r => r.Id == incident.Id);
        persisted.Should().NotBeNull();

        var audit = await _db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == missionId && a.ActionType == "INCIDENT_REPORTED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task SubmitIncidentReport_ShouldCreateCriticalNotification_WhenSeverityIsCritical()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-INC-CRIT",
            Title = "Critical Incident Mission",
            RegionId = _regionId,
            ManagerId = _managerId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        mission.MissionTargets.Add(new MissionTarget { MissionId = missionId, AssetId = _assetId });
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var request = new SubmitIncidentReportRequest(
            IncidentType: "DroneCrash",
            Severity: "Critical",
            Description: "UAV lost GPS lock and crashed into vegetation corridor.",
            AssetId: _assetId
        );

        // Act
        var incident = await _service.SubmitIncidentReportAsync(missionId, request, CancellationToken.None);

        // Assert
        incident.Severity.Should().Be("Critical");

        var audit = await _db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == missionId && a.ActionType == "CRITICAL_INCIDENT_REPORTED");
        audit.Should().NotBeNull();

        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.UserId == _managerId);
        notification.Should().NotBeNull();
        notification!.Body.Should().Contain("DroneCrash");
    }

    [Theory]
    [InlineData("", "Medium", "Valid description text here")]
    [InlineData("DroneDamage", "InvalidSeverityLevel", "Valid description text here")]
    [InlineData("DroneDamage", "High", "TooShort")]
    public async Task SubmitIncidentReport_ShouldThrowBusinessRuleException_WhenValidationFails(
        string incidentType, string severity, string description)
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-INC-VAL",
            Title = "Validation Test Mission",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        mission.MissionTargets.Add(new MissionTarget { MissionId = missionId, AssetId = _assetId });
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var request = new SubmitIncidentReportRequest(
            IncidentType: incidentType,
            Severity: severity,
            Description: description,
            AssetId: _assetId
        );

        // Act
        Func<Task> act = async () => await _service.SubmitIncidentReportAsync(missionId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task GetIncidentReports_ShouldReturnReportsInDescendingOrder()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-INC-LIST",
            Title = "List Test Mission",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        _db.Missions.Add(mission);

        _db.IncidentReports.Add(new IncidentReport
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            ReportedBy = _userId,
            AssetId = _assetId,
            IncidentType = "MinorCorrosion",
            Severity = "Low",
            Description = "Initial observation note",
            ReportedAt = DateTime.UtcNow.AddMinutes(-20)
        });

        _db.IncidentReports.Add(new IncidentReport
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            ReportedBy = _userId,
            AssetId = _assetId,
            IncidentType = "CableDamage",
            Severity = "High",
            Description = "Follow-up broken strand note",
            ReportedAt = DateTime.UtcNow.AddMinutes(-2)
        });

        await _db.SaveChangesAsync();

        // Act
        var reports = await _service.GetIncidentReportsAsync(missionId, CancellationToken.None);

        // Assert
        reports.Should().HaveCount(2);
        reports[0].IncidentType.Should().Be("CableDamage");
        reports[1].IncidentType.Should().Be("MinorCorrosion");
    }
}
