using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneTelemetry;

public class ProcessDroneTelemetryCommandHandler : IRequestHandler<ProcessDroneTelemetryCommand, bool>
{
    private readonly IUavRepository _uavRepository;
    private readonly IDroneLiveStateService _liveStateService;
    private readonly MqttOptions _options;
    private readonly ILogger<ProcessDroneTelemetryCommandHandler> _logger;

    public ProcessDroneTelemetryCommandHandler(
        IUavRepository uavRepository,
        IDroneLiveStateService liveStateService,
        IOptions<MqttOptions> options,
        ILogger<ProcessDroneTelemetryCommandHandler> logger)
    {
        _uavRepository = uavRepository;
        _liveStateService = liveStateService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessDroneTelemetryCommand request, CancellationToken cancellationToken)
    {
        var droneCode = ResolveDroneCode(request.TopicDroneCode, request.PayloadDroneCode);
        if (droneCode == null)
        {
            _logger.LogWarning("Rejected MQTT drone telemetry because topic code {TopicDroneCode} does not match payload code {PayloadDroneCode}",
                request.TopicDroneCode, request.PayloadDroneCode);
            return false;
        }

        if (!IsValidTelemetry(request))
        {
            _logger.LogWarning("Rejected MQTT drone telemetry for {DroneCode} because payload validation failed", droneCode);
            return false;
        }

        var uav = await _uavRepository.GetByUavCodeAsync(droneCode);
        if (uav == null)
        {
            _logger.LogWarning("Unknown drone code {DroneCode} in MQTT telemetry message", droneCode);
            return false;
        }

        await _liveStateService.UpdateTelemetryAsync(
            new DroneLiveTelemetry(
                uav.Id,
                uav.UavCode,
                request.Timestamp ?? DateTime.UtcNow,
                request.Latitude,
                request.Longitude,
                request.Altitude,
                request.Battery,
                request.Speed,
                request.Heading),
            TimeSpan.FromSeconds(_options.LiveStateTtlSeconds),
            cancellationToken);

        return true;
    }

    private static bool IsValidTelemetry(ProcessDroneTelemetryCommand request)
    {
        return IsFinite(request.Latitude)
            && IsFinite(request.Longitude)
            && request.Latitude is >= -90 and <= 90
            && request.Longitude is >= -180 and <= 180
            && IsValidOptional(request.Altitude)
            && IsValidOptional(request.Speed)
            && IsValidOptional(request.Heading)
            && (request.Battery is null || request.Battery is >= 0 and <= 100 && IsFinite(request.Battery.Value));
    }

    private static bool IsValidOptional(double? value)
    {
        return value is null || IsFinite(value.Value);
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private static string? ResolveDroneCode(string topicDroneCode, string? payloadDroneCode)
    {
        if (string.IsNullOrWhiteSpace(topicDroneCode))
            return null;

        if (string.IsNullOrWhiteSpace(payloadDroneCode))
            return topicDroneCode.Trim();

        return string.Equals(topicDroneCode.Trim(), payloadDroneCode.Trim(), StringComparison.OrdinalIgnoreCase)
            ? topicDroneCode.Trim()
            : null;
    }
}
