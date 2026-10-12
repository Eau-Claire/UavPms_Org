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

public class MissionFlightLogTests
{
    private readonly ApplicationDbContext _db;
    private readonly Mock<ICurrentUserServices> _currentMock;
    private readonly MissionLifecycleService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _regionId = Guid.NewGuid();

    public MissionFlightLogTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"FlightLogTests_{Guid.NewGuid()}")
            .Options;

        _currentMock = new Mock<ICurrentUserServices>();
        _currentMock.Setup(c => c.IsAuthenticated).Returns(true);
        _currentMock.Setup(c => c.UserId).Returns(_userId);
        _currentMock.Setup(c => c.Roles).Returns(new[] { UserRoles.Inspector });

        _db = new ApplicationDbContext(options, _currentMock.Object);

        _service = new MissionLifecycleService(_db, _currentMock.Object);

        // Seed default user and region
        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "inspector@test.com",
            FullName = "Test Inspector",
            Status = "Active"
        });

        _db.Regions.Add(new Region
        {
            Id = _regionId,
            RegionName = "Southern Transmission Area",
            IsDeleted = false
        });

        _db.UserGeographicScopes.Add(new UserGeographicScope
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            RegionId = _regionId
        });

        _db.SaveChanges();
    }

    [Fact]
    public async Task UploadFlightLog_ShouldSucceed_WhenMissionIsInProgressAndPayloadIsValid()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-LOG-01",
            Title = "Mission For Flight Log",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress,
            StartedAt = DateTime.UtcNow.AddHours(-1)
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var request = new UploadFlightLogRequest(
            GpsTrack: "[{\"lat\":10.76,\"lng\":106.66,\"alt\":120.5,\"speed\":15.2,\"time\":\"2026-10-02T10:00:00Z\"}]",
            MinBatteryRecorded: 42.5,
            MaxAltitudeM: 155.0,
            FlightDurationSeconds: 1800,
            ConnectionStatus: "Stable"
        );

        // Act
        var log = await _service.UploadFlightLogAsync(missionId, request, CancellationToken.None);

        // Assert
        log.Should().NotBeNull();
        log.MissionId.Should().Be(missionId);
        log.MinBatteryRecorded.Should().Be(42.5);
        log.MaxAltitudeM.Should().Be(155.0);
        log.FlightDurationSeconds.Should().Be(1800);
        log.ConnectionStatus.Should().Be("Stable");

        var persisted = await _db.MissionFlightLogs.FirstOrDefaultAsync(l => l.Id == log.Id);
        persisted.Should().NotBeNull();
        persisted!.GpsTrack.Should().Contain("106.66");

        var audit = await _db.AuditLogs.FirstOrDefaultAsync(a => a.RecordId == missionId && a.ActionType == "FLIGHT_LOG_UPLOADED");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task UploadFlightLog_ShouldThrowBusinessRuleException_WhenMissionIsInDraftState()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-LOG-DRAFT",
            Title = "Draft Mission",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.Draft
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var request = new UploadFlightLogRequest(
            GpsTrack: "[]",
            MinBatteryRecorded: 50,
            MaxAltitudeM: 100,
            FlightDurationSeconds: 600,
            ConnectionStatus: "Good"
        );

        // Act
        Func<Task> act = async () => await _service.UploadFlightLogAsync(missionId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*FLIGHT_LOG_INVALID_STATE*");
    }

    [Fact]
    public async Task UploadFlightLog_ShouldThrowBusinessRuleException_WhenGpsTrackIsMalformedJson()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-LOG-MALFORMED",
            Title = "Mission Malformed",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var request = new UploadFlightLogRequest(
            GpsTrack: "this is not valid json {{{",
            MinBatteryRecorded: 50,
            MaxAltitudeM: 100,
            FlightDurationSeconds: 600,
            ConnectionStatus: "Good"
        );

        // Act
        Func<Task> act = async () => await _service.UploadFlightLogAsync(missionId, request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*INVALID_GPS_TRACK_JSON*");
    }

    [Fact]
    public async Task UploadFlightLog_ShouldThrowBusinessRuleException_WhenBatteryIsNegativeOrOver100()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-LOG-BATTERY",
            Title = "Mission Battery",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        _db.Missions.Add(mission);
        await _db.SaveChangesAsync();

        var requestOver = new UploadFlightLogRequest(
            GpsTrack: "[]",
            MinBatteryRecorded: 105,
            MaxAltitudeM: 100,
            FlightDurationSeconds: 600,
            ConnectionStatus: "Good"
        );

        Func<Task> actOver = async () => await _service.UploadFlightLogAsync(missionId, requestOver, CancellationToken.None);
        await actOver.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*INVALID_BATTERY_LEVEL*");

        var requestNeg = new UploadFlightLogRequest(
            GpsTrack: "[]",
            MinBatteryRecorded: -5,
            MaxAltitudeM: 100,
            FlightDurationSeconds: 600,
            ConnectionStatus: "Good"
        );

        Func<Task> actNeg = async () => await _service.UploadFlightLogAsync(missionId, requestNeg, CancellationToken.None);
        await actNeg.Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*INVALID_BATTERY_LEVEL*");
    }

    [Fact]
    public async Task GetFlightLogs_ShouldReturnLogsInDescendingOrder()
    {
        // Arrange
        var missionId = Guid.NewGuid();
        var mission = new Mission
        {
            Id = missionId,
            MissionCode = "MSN-LOG-LIST",
            Title = "Mission List",
            RegionId = _regionId,
            InspectorId = _userId,
            Status = MissionStatus.InProgress
        };
        _db.Missions.Add(mission);

        _db.MissionFlightLogs.Add(new MissionFlightLog
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            GpsTrack = "[]",
            MinBatteryRecorded = 80,
            RecordedAt = DateTime.UtcNow.AddMinutes(-30)
        });

        _db.MissionFlightLogs.Add(new MissionFlightLog
        {
            Id = Guid.NewGuid(),
            MissionId = missionId,
            GpsTrack = "[]",
            MinBatteryRecorded = 40,
            RecordedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        await _db.SaveChangesAsync();

        // Act
        var logs = await _service.GetFlightLogsAsync(missionId, CancellationToken.None);

        // Assert
        logs.Should().HaveCount(2);
        logs[0].MinBatteryRecorded.Should().Be(40);
        logs[1].MinBatteryRecorded.Should().Be(80);
    }
}
