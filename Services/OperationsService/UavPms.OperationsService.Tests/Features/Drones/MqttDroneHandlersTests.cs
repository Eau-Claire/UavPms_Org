using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneStatus;
using UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneTelemetry;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Messaging;

namespace UavPms.OperationsService.Tests.Features.Drones;

public class MqttDroneHandlersTests
{
    private static IOptions<MqttOptions> Options => Microsoft.Extensions.Options.Options.Create(new MqttOptions
    {
        Enabled = true,
        LiveStateTtlSeconds = 30,
        OfflineTimeoutSeconds = 30
    });

    [Fact]
    public async Task StatusHandler_ShouldUpdateLiveState_WhenDroneIsKnown()
    {
        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-001" };
        var uavRepository = new Mock<IUavRepository>();
        var liveStateService = new Mock<IDroneLiveStateService>();
        uavRepository.Setup(x => x.GetByUavCodeAsync("UAV-001")).ReturnsAsync(uav);

        var handler = new ProcessDroneStatusCommandHandler(
            uavRepository.Object,
            liveStateService.Object,
            Options,
            NullLogger<ProcessDroneStatusCommandHandler>.Instance);

        var result = await handler.Handle(
            new ProcessDroneStatusCommand("UAV-001", "UAV-001", "online", 92, DateTime.UtcNow),
            CancellationToken.None);

        result.Should().BeTrue();
        liveStateService.Verify(x => x.UpdateStatusAsync(
            It.Is<DroneLiveStatus>(s => s.DroneId == uav.Id && s.DroneCode == "UAV-001" && s.Online && s.Battery == 92),
            TimeSpan.FromSeconds(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StatusHandler_ShouldIgnoreUnknownDroneCode()
    {
        var uavRepository = new Mock<IUavRepository>();
        var liveStateService = new Mock<IDroneLiveStateService>();
        uavRepository.Setup(x => x.GetByUavCodeAsync("UAV-999")).ReturnsAsync((Uav?)null);

        var handler = new ProcessDroneStatusCommandHandler(
            uavRepository.Object,
            liveStateService.Object,
            Options,
            NullLogger<ProcessDroneStatusCommandHandler>.Instance);

        var result = await handler.Handle(
            new ProcessDroneStatusCommand("UAV-999", "UAV-999", "online", 80, DateTime.UtcNow),
            CancellationToken.None);

        result.Should().BeFalse();
        liveStateService.Verify(x => x.UpdateStatusAsync(
            It.IsAny<DroneLiveStatus>(),
            It.IsAny<TimeSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task StatusHandler_ShouldRejectTopicPayloadMismatch()
    {
        var handler = new ProcessDroneStatusCommandHandler(
            new Mock<IUavRepository>().Object,
            new Mock<IDroneLiveStateService>().Object,
            Options,
            NullLogger<ProcessDroneStatusCommandHandler>.Instance);

        var result = await handler.Handle(
            new ProcessDroneStatusCommand("UAV-001", "UAV-002", "online", 80, DateTime.UtcNow),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TelemetryHandler_ShouldRejectInvalidCoordinates()
    {
        var handler = new ProcessDroneTelemetryCommandHandler(
            new Mock<IUavRepository>().Object,
            new Mock<IDroneLiveStateService>().Object,
            Options,
            NullLogger<ProcessDroneTelemetryCommandHandler>.Instance);

        var result = await handler.Handle(
            new ProcessDroneTelemetryCommand("UAV-001", "UAV-001", DateTime.UtcNow, 91, 106.8098, 35, 87, 8.4, 120.5),
            CancellationToken.None);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TelemetryHandler_ShouldUpdateLiveState_WhenPayloadIsValid()
    {
        var uav = new Uav { Id = Guid.NewGuid(), UavCode = "UAV-001" };
        var uavRepository = new Mock<IUavRepository>();
        var liveStateService = new Mock<IDroneLiveStateService>();
        uavRepository.Setup(x => x.GetByUavCodeAsync("UAV-001")).ReturnsAsync(uav);

        var handler = new ProcessDroneTelemetryCommandHandler(
            uavRepository.Object,
            liveStateService.Object,
            Options,
            NullLogger<ProcessDroneTelemetryCommandHandler>.Instance);

        var result = await handler.Handle(
            new ProcessDroneTelemetryCommand("UAV-001", "UAV-001", DateTime.UtcNow, 10.8411, 106.8098, 35, 87, 8.4, 120.5),
            CancellationToken.None);

        result.Should().BeTrue();
        liveStateService.Verify(x => x.UpdateTelemetryAsync(
            It.Is<DroneLiveTelemetry>(t => t.DroneId == uav.Id && t.DroneCode == "UAV-001" && t.Latitude == 10.8411),
            TimeSpan.FromSeconds(30),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void Parser_ShouldReturnFalse_ForInvalidPayload()
    {
        var result = MqttDroneMessageParser.TryParseStatus("{ invalid json", out var payload);

        result.Should().BeFalse();
        payload.Should().BeNull();
    }

    [Fact]
    public void Parser_ShouldExtractDroneCode_FromStatusTopic()
    {
        var result = MqttDroneMessageParser.TryGetTopicDroneCode("uav/UAV-001/status", "status", out var droneCode);

        result.Should().BeTrue();
        droneCode.Should().Be("UAV-001");
    }
}
