using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.API.Hubs;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Telemetry.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Messaging;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Tests.Features.Drones;

public class MqttDroneHandlersTests
{
    private const string SampleValidPayload = """
    {
      "drone_id": "UAV-HCM-01",
      "timestamp": "2026-11-23T04:26:48.338Z",
      "flight_mode": "AUTO",
      "armed": true,
      "lat": 10.7770369,
      "lon": 106.705548,
      "alt_relative_m": 40.0,
      "alt_msl_m": 50.0,
      "satellites": 14,
      "gps_fix_type": 3,
      "hdop": 0.8,
      "roll_deg": 2.95,
      "pitch_deg": -2.0,
      "yaw_deg": 95.03,
      "ground_speed_ms": 5.0,
      "air_speed_ms": 0.0,
      "climb_rate_ms": 0.1,
      "heading_deg": 95.0,
      "throttle_pct": 52.0,
      "battery_voltage_v": 22.2,
      "battery_current_a": 12.5,
      "battery_remaining_pct": 99
    }
    """;

    private static ApplicationDbContext CreateDbContext(ICurrentUserServices? currentUser = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"telemetry-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, currentUser);
    }

    private static ICurrentUserServices MockUser(Guid userId, params string[] roles)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.Roles).Returns(roles);
        return mock.Object;
    }

    [Fact]
    public void Parser_ShouldAcceptValid22FieldSnakeCasePayload_AndRejectMismatchedOrInvalidInputs()
    {
        var ok = MqttDroneMessageParser.TryParseAndValidateTelemetry(
            "drone/UAV-HCM-01/telemetry",
            SampleValidPayload,
            out var parsed,
            out var timestampUtc,
            out var error);

        ok.Should().BeTrue();
        error.Should().BeNull();
        parsed.Should().NotBeNull();
        parsed!.DroneId.Should().Be("UAV-HCM-01");
        parsed.FlightMode.Should().Be("AUTO");
        parsed.Armed.Should().BeTrue();
        parsed.Lat.Should().Be(10.7770369);
        parsed.Lon.Should().Be(106.705548);
        parsed.BatteryRemainingPct.Should().Be(99);
        timestampUtc.Kind.Should().Be(DateTimeKind.Utc);

        // Topic segment mismatch
        MqttDroneMessageParser.TryParseAndValidateTelemetry(
            "drone/UAV-OTHER/telemetry",
            SampleValidPayload,
            out _,
            out _,
            out var mismatchError).Should().BeFalse();
        mismatchError.Should().Contain("does not match");

        // Half-null coordinates (lat present, lon null)
        const string halfNullGps = """{"drone_id":"UAV-HCM-01","timestamp":"2026-11-23T04:26:48.338Z","lat":10.7,"lon":null}""";
        MqttDroneMessageParser.TryParseAndValidateTelemetry(
            "drone/UAV-HCM-01/telemetry",
            halfNullGps,
            out _,
            out _,
            out var gpsError).Should().BeFalse();
        gpsError.Should().Contain("both be present or both be null");

        // Out-of-range coordinates
        const string outOfRangeGps = """{"drone_id":"UAV-HCM-01","timestamp":"2026-11-23T04:26:48.338Z","lat":95.0,"lon":106.7}""";
        MqttDroneMessageParser.TryParseAndValidateTelemetry(
            "drone/UAV-HCM-01/telemetry",
            outOfRangeGps,
            out _,
            out _,
            out var rangeError).Should().BeFalse();
        rangeError.Should().Contain("out of valid range");
    }

    [Fact]
    public async Task IngestAsync_WithActiveInProgressMission_SavesTelemetry_UpdatesUav_AndBroadcastsOnlineAndTelemetry()
    {
        var managerId = Guid.NewGuid();
        var currentUser = MockUser(managerId, UserRoles.Manager);
        await using var db = CreateDbContext(currentUser);

        var uav = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-HCM-01",
            Model = "DJI M300",
            BatteryLevel = 80
        };
        var inProgressMission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-INPROG-01",
            Title = "Active Mission",
            ManagerId = managerId,
            UavId = uav.Id,
            Status = MissionStatus.InProgress
        };
        var completedMission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-DONE-01",
            Title = "Completed Mission",
            ManagerId = managerId,
            UavId = uav.Id,
            Status = MissionStatus.Completed
        };
        db.Uavs.Add(uav);
        db.Missions.AddRange(inProgressMission, completedMission);
        await db.SaveChangesAsync();

        var liveState = new InMemoryDroneLiveStateService();
        var notifierMock = new Mock<ITelemetryRealtimeNotifier>();
        var service = new DroneTelemetryService(
            db,
            currentUser,
            liveState,
            Options.Create(new MqttOptions { OfflineTimeoutSeconds = 30, RetentionDays = 30 }),
            NullLogger<DroneTelemetryService>.Instance,
            notifierMock.Object);

        var dto = await service.IngestAsync("drone/UAV-HCM-01/telemetry", SampleValidPayload);

        dto.Should().NotBeNull();
        dto!.DroneId.Should().Be("UAV-HCM-01");
        dto.MissionId.Should().Be(inProgressMission.Id);
        dto.Lat.Should().Be(10.7770369);
        dto.Lon.Should().Be(106.705548);
        dto.BatteryRemainingPct.Should().Be(99);
        dto.IsOnline.Should().BeTrue();

        var savedRow = await db.DroneTelemetry.SingleAsync();
        savedRow.UavId.Should().Be(uav.Id);
        savedRow.MissionId.Should().Be(inProgressMission.Id);
        savedRow.Location.Should().NotBeNull();
        savedRow.Location!.Y.Should().Be(10.7770369);
        savedRow.Location.X.Should().Be(106.705548);

        var updatedUav = await db.Uavs.SingleAsync(x => x.Id == uav.Id);
        updatedUav.BatteryLevel.Should().Be(99);
        updatedUav.CurrentLocation.Should().NotBeNull();
        updatedUav.CurrentLocation!.Y.Should().Be(10.7770369);

        notifierMock.Verify(n => n.NotifyDroneOnlineAsync(
            It.Is<DroneConnectionEventDto>(e => e.DroneCode == "UAV-HCM-01" && e.MissionId == inProgressMission.Id && e.IsOnline),
            It.IsAny<CancellationToken>()), Times.Once);
        notifierMock.Verify(n => n.NotifyTelemetryUpdatedAsync(
            It.Is<DroneTelemetryDto>(t => t.DroneId == "UAV-HCM-01" && t.MissionId == inProgressMission.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_DeduplicatesQoS1Retries_PreventsOutOfOrderOverwrite_AndPreservesGpsWhenNull()
    {
        var adminUser = MockUser(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateDbContext(adminUser);

        var uav = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-HCM-01",
            Model = "DJI M300",
            BatteryLevel = 50
        };
        db.Uavs.Add(uav);
        await db.SaveChangesAsync();

        var liveState = new InMemoryDroneLiveStateService();
        var service = new DroneTelemetryService(
            db,
            adminUser,
            liveState,
            Options.Create(new MqttOptions { OfflineTimeoutSeconds = 30, RetentionDays = 30 }),
            NullLogger<DroneTelemetryService>.Instance);

        // 1. Newer message with valid GPS at T = 04:26:48Z
        var first = await service.IngestAsync("drone/UAV-HCM-01/telemetry", SampleValidPayload);
        first.Should().NotBeNull();
        first!.MissionId.Should().BeNull(); // No active mission -> MissionId = null

        // 2. Duplicate QoS 1 message with identical timestamp -> ignored
        var duplicate = await service.IngestAsync("drone/UAV-HCM-01/telemetry", SampleValidPayload);
        duplicate.Should().BeNull();
        (await db.DroneTelemetry.CountAsync()).Should().Be(1);

        // 3. Out-of-order older message (T = 04:25:00Z) with different GPS & battery -> saved to history, but does NOT overwrite latest state
        const string olderPayload = """
        {
          "drone_id": "UAV-HCM-01",
          "timestamp": "2026-11-23T04:25:00.000Z",
          "lat": 10.0000,
          "lon": 106.0000,
          "battery_remaining_pct": 40
        }
        """;
        var older = await service.IngestAsync("drone/UAV-HCM-01/telemetry", olderPayload);
        older.Should().NotBeNull();
        (await db.DroneTelemetry.CountAsync()).Should().Be(2);

        var uavAfterOlder = await db.Uavs.SingleAsync(x => x.Id == uav.Id);
        uavAfterOlder.BatteryLevel.Should().Be(99);
        uavAfterOlder.CurrentLocation!.Y.Should().Be(10.7770369);

        // 4. Newer message with null GPS (no GPS fix at T = 04:27:00Z) -> history row has Location = null, but UAVs.CurrentLocation is preserved
        const string noGpsPayload = """
        {
          "drone_id": "UAV-HCM-01",
          "timestamp": "2026-11-23T04:27:00.000Z",
          "lat": null,
          "lon": null,
          "battery_remaining_pct": 97
        }
        """;
        var noGps = await service.IngestAsync("drone/UAV-HCM-01/telemetry", noGpsPayload);
        noGps.Should().NotBeNull();
        noGps!.Lat.Should().BeNull();
        noGps.Lon.Should().BeNull();

        var uavAfterNoGps = await db.Uavs.SingleAsync(x => x.Id == uav.Id);
        uavAfterNoGps.BatteryLevel.Should().Be(97);
        uavAfterNoGps.CurrentLocation.Should().NotBeNull();
        uavAfterNoGps.CurrentLocation!.Y.Should().Be(10.7770369);

        // Latest REST endpoint returns the latest row while preserving the last known GPS coordinates
        var latestDto = await service.GetLatestByDroneCodeAsync("UAV-HCM-01");
        latestDto.BatteryRemainingPct.Should().Be(97);
        latestDto.Lat.Should().Be(10.7770369);
        latestDto.Lon.Should().Be(106.705548);
    }

    [Fact]
    public async Task OfflineDetection_AndRetentionPurge_ShouldEmitDroneOfflineAndDeleteExpiredHistory()
    {
        var adminUser = MockUser(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateDbContext(adminUser);

        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-HCM-01", Model = "DJI M300" };
        db.Uavs.Add(uav);
        db.DroneTelemetry.Add(new DroneTelemetry
        {
            Id = Guid.NewGuid(),
            UavId = uav.Id,
            SourceTimestamp = DateTime.UtcNow.AddDays(-45),
            ReceivedAt = DateTime.UtcNow.AddDays(-45)
        });
        await db.SaveChangesAsync();

        var liveState = new InMemoryDroneLiveStateService();
        var notifierMock = new Mock<ITelemetryRealtimeNotifier>();
        var service = new DroneTelemetryService(
            db,
            adminUser,
            liveState,
            Options.Create(new MqttOptions { OfflineTimeoutSeconds = 1, RetentionDays = 30 }),
            NullLogger<DroneTelemetryService>.Instance,
            notifierMock.Object);

        await service.IngestAsync("drone/UAV-HCM-01/telemetry", SampleValidPayload);
        liveState.MarkMessageReceived("UAV-HCM-01", uav.Id, null, DateTime.UtcNow.AddSeconds(-5));

        var offlineCount = await service.CheckOfflineDronesAsync();
        offlineCount.Should().Be(1);
        notifierMock.Verify(n => n.NotifyDroneOfflineAsync(
            It.Is<DroneConnectionEventDto>(e => e.DroneCode == "UAV-HCM-01" && !e.IsOnline),
            It.IsAny<CancellationToken>()), Times.Once);

        var purgedCount = await service.PurgeExpiredTelemetryAsync();
        purgedCount.Should().Be(1);
        (await db.DroneTelemetry.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AccessControl_RestAndSignalRHub_ShouldRejectUnauthorizedUsersWith403()
    {
        var managerId = Guid.NewGuid();
        var assignedInspectorId = Guid.NewGuid();
        var outsiderInspectorId = Guid.NewGuid();

        await using var db = CreateDbContext(MockUser(outsiderInspectorId, UserRoles.Inspector));

        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-HCM-01", Model = "DJI M300" };
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-SEC-01",
            Title = "Secured Mission",
            ManagerId = managerId,
            UavId = uav.Id,
            Status = MissionStatus.InProgress
        };
        db.Uavs.Add(uav);
        db.Missions.Add(mission);
        db.MissionAssignments.Add(new MissionAssignment
        {
            Id = Guid.NewGuid(),
            MissionId = mission.Id,
            UserId = assignedInspectorId,
            AssignmentRole = "INSPECTOR",
            Status = MissionAssignmentStatus.Active
        });
        await db.SaveChangesAsync();

        var liveState = new InMemoryDroneLiveStateService();
        var outsiderService = new DroneTelemetryService(
            db,
            MockUser(outsiderInspectorId, UserRoles.Inspector),
            liveState,
            Options.Create(new MqttOptions()),
            NullLogger<DroneTelemetryService>.Instance);

        await outsiderService.IngestAsync("drone/UAV-HCM-01/telemetry", SampleValidPayload);

        // Outsider inspector -> 403 ForbiddenException on both mission playback and drone latest telemetry
        var actMission = () => outsiderService.GetMissionTelemetryAsync(mission.Id, null, null);
        await actMission.Should().ThrowAsync<ForbiddenException>();

        var actDrone = () => outsiderService.GetLatestByDroneCodeAsync("UAV-HCM-01");
        await actDrone.Should().ThrowAsync<ForbiddenException>();

        // Manager không có vùng của Drone (và không được giao nhiệm vụ) -> cũng bị chặn, không còn thấy mọi vùng
        var otherRegionManagerService = new DroneTelemetryService(
            db,
            MockUser(Guid.NewGuid(), UserRoles.Manager),
            liveState,
            Options.Create(new MqttOptions()),
            NullLogger<DroneTelemetryService>.Instance);
        var actManager = () => otherRegionManagerService.GetLatestByDroneCodeAsync("UAV-HCM-01");
        await actManager.Should().ThrowAsync<ForbiddenException>().WithMessage("DRONE_TELEMETRY_ACCESS_DENIED");

        // Assigned inspector -> allowed
        var assignedService = new DroneTelemetryService(
            db,
            MockUser(assignedInspectorId, UserRoles.Inspector),
            liveState,
            Options.Create(new MqttOptions()),
            NullLogger<DroneTelemetryService>.Instance);

        var playback = await assignedService.GetMissionTelemetryAsync(mission.Id, null, null);
        playback.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task CompleteMission_WithPeriodicDroneTelemetry_ShouldAutoAggregateMissionFlightLog()
    {
        var managerId = Guid.NewGuid();
        var currentUser = MockUser(managerId, UserRoles.Manager);
        await using var db = CreateDbContext(currentUser);

        var region = new Region { Id = Guid.NewGuid(), Code = "REG-HCM", Name = "HCM Region" };
        var uav = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = "UAV-HCM-01",
            Model = "DJI M300",
            FlightStatus = DroneStatus.Flying,
            OperationalStatus = DroneOperationalStatus.Available,
            TechnicalHealth = TechnicalHealth.Healthy,
            BatteryLevel = 95
        };
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-FLIGHTLOG-01",
            Title = "Telemetry Aggregation Mission",
            ManagerId = managerId,
            RegionId = region.Id,
            UavId = uav.Id,
            Status = MissionStatus.InProgress,
            StartedAt = new DateTime(2026, 11, 23, 4, 25, 0, DateTimeKind.Utc)
        };

        db.Users.Add(new User { Id = managerId, FullName = "Manager", Email = "mgr@uavpms.local", Status = "Active", IsEmailVerified = true });
        db.Regions.Add(region);
        db.Uavs.Add(uav);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var liveState = new InMemoryDroneLiveStateService();
        var telemetryService = new DroneTelemetryService(
            db,
            currentUser,
            liveState,
            Options.Create(new MqttOptions()),
            NullLogger<DroneTelemetryService>.Instance);

        const string frame1 = """
        {
          "drone_id": "UAV-HCM-01",
          "timestamp": "2026-11-23T04:26:00.000Z",
          "lat": 10.777000,
          "lon": 106.705500,
          "alt_relative_m": 35.5,
          "battery_remaining_pct": 95
        }
        """;
        const string frame2 = """
        {
          "drone_id": "UAV-HCM-01",
          "timestamp": "2026-11-23T04:28:30.000Z",
          "lat": 10.778000,
          "lon": 106.706500,
          "alt_relative_m": 62.4,
          "battery_remaining_pct": 78
        }
        """;

        await telemetryService.IngestAsync("drone/UAV-HCM-01/telemetry", frame1);
        await telemetryService.IngestAsync("drone/UAV-HCM-01/telemetry", frame2);

        var lifecycleService = new MissionLifecycleService(db, currentUser);
        await lifecycleService.CompleteAsync(mission.Id, CancellationToken.None);

        var flightLog = await db.MissionFlightLogs.SingleOrDefaultAsync(l => l.MissionId == mission.Id);
        flightLog.Should().NotBeNull();
        flightLog!.MinBatteryRecorded.Should().Be(78);
        flightLog.MaxAltitudeM.Should().Be(62.4);
        flightLog.FlightDurationSeconds.Should().Be(150);
        flightLog.ConnectionStatus.Should().Be("Normal");
        flightLog.GpsTrack.Should().Contain("10.777").And.Contain("10.778");
    }

    [Fact]
    public async Task LiveHiveMqCloud_ShouldConnectTls_Subscribe_AndIngestPublishedTelemetry()
    {
        var mqttPassword = Environment.GetEnvironmentVariable("LIVE_MQTT_TEST_PASSWORD");
        var mqttUser = Environment.GetEnvironmentVariable("LIVE_MQTT_TEST_USER");
        if (string.IsNullOrWhiteSpace(mqttUser) || string.IsNullOrWhiteSpace(mqttPassword))
            return; // Only runs when live credentials are provided via env vars

        var adminUser = MockUser(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateDbContext(adminUser);

        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-HCM-01", Model = "DJI M300", BatteryLevel = 80 };
        var mission = new Mission
        {
            Id = Guid.NewGuid(),
            MissionCode = "MS-LIVE-01",
            Title = "Live HiveMQ Test Mission",
            ManagerId = adminUser.UserId,
            UavId = uav.Id,
            Status = MissionStatus.InProgress
        };
        db.Uavs.Add(uav);
        db.Missions.Add(mission);
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton(adminUser);
        services.AddSingleton<InMemoryDroneLiveStateService>();
        services.AddSingleton<IDroneTelemetryService, DroneTelemetryService>();
        services.AddLogging();
        var mqttOpts = Options.Create(new MqttOptions
        {
            Enabled = true,
            Host = "d130275373b6451bac4640918d94bb1c.s1.eu.hivemq.cloud",
            Port = 8883,
            UseTls = true,
            Username = mqttUser,
            Password = mqttPassword,
            ClientId = $"uav-pms-live-test-sub-{Guid.NewGuid():N}",
            TelemetryTopic = "drone/+/telemetry"
        });
        services.AddSingleton(mqttOpts);
        var sp = services.BuildServiceProvider();

        var consumer = new MqttDroneConsumer(
            sp.GetRequiredService<IServiceScopeFactory>(),
            mqttOpts,
            NullLogger<MqttDroneConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await consumer.StartAsync(cts.Token);

        try
        {
            // Give consumer time to connect & subscribe
            await Task.Delay(2500, cts.Token);

            // Wait up to 6 seconds to see if a real drone/publisher is already streaming on drone/+/telemetry;
            // and also publish a test frame on drone/UAV-HCM-01/telemetry via a separate MQTT client to prove full round-trip.
            using var publisher = new MQTTnet.MqttClientFactory().CreateMqttClient();
            var pubOptions = new MQTTnet.MqttClientOptionsBuilder()
                .WithClientId($"uav-pms-live-test-pub-{Guid.NewGuid():N}")
                .WithTcpServer(mqttOpts.Value.Host, mqttOpts.Value.Port)
                .WithCredentials(mqttUser, mqttPassword)
                .WithTlsOptions(t => t.UseTls())
                .WithCleanSession()
                .Build();

            var connectResult = await publisher.ConnectAsync(pubOptions, cts.Token);
            connectResult.ResultCode.Should().Be(MQTTnet.MqttClientConnectResultCode.Success);

            var nowIso = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var testPayload = $$"""
            {
              "drone_id": "UAV-HCM-01",
              "timestamp": "{{nowIso}}",
              "flight_mode": "AUTO",
              "armed": true,
              "lat": 10.7770369,
              "lon": 106.705548,
              "alt_relative_m": 42.5,
              "alt_msl_m": 52.5,
              "satellites": 15,
              "gps_fix_type": 3,
              "hdop": 0.7,
              "roll_deg": 1.2,
              "pitch_deg": -0.8,
              "yaw_deg": 90.0,
              "ground_speed_ms": 6.4,
              "air_speed_ms": 6.5,
              "climb_rate_ms": 0.2,
              "heading_deg": 90.0,
              "throttle_pct": 55.0,
              "battery_voltage_v": 22.8,
              "battery_current_a": 11.2,
              "battery_remaining_pct": 94
            }
            """;

            var msg = new MQTTnet.MqttApplicationMessageBuilder()
                .WithTopic("drone/UAV-HCM-01/telemetry")
                .WithPayload(testPayload)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await publisher.PublishAsync(msg, cts.Token);

            // Wait for MqttDroneConsumer to receive from HiveMQ Cloud and persist to DB
            for (var i = 0; i < 20; i++)
            {
                if (await db.DroneTelemetry.AnyAsync(cts.Token))
                    break;
                await Task.Delay(250, cts.Token);
            }

            await publisher.DisconnectAsync(new MQTTnet.MqttClientDisconnectOptionsBuilder().Build(), cts.Token);

            var saved = await db.DroneTelemetry.OrderByDescending(t => t.SourceTimestamp).FirstOrDefaultAsync(cts.Token);
            saved.Should().NotBeNull("MqttDroneConsumer must receive the telemetry frame from HiveMQ Cloud over TLS and persist it");
            saved!.MissionId.Should().Be(mission.Id);
            saved.BatteryRemainingPct.Should().Be(94);
            saved.AltRelativeM.Should().Be(42.5);
        }
        finally
        {
            await consumer.StopAsync(CancellationToken.None);
        }
    }
}
