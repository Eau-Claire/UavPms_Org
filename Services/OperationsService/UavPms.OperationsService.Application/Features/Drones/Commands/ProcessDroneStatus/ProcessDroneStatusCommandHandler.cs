using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneStatus;

public class ProcessDroneStatusCommandHandler : IRequestHandler<ProcessDroneStatusCommand, bool>
{
    private readonly IUavRepository _uavRepository;
    private readonly IDroneLiveStateService _liveStateService;
    private readonly MqttOptions _options;
    private readonly ILogger<ProcessDroneStatusCommandHandler> _logger;

    public ProcessDroneStatusCommandHandler(
        IUavRepository uavRepository,
        IDroneLiveStateService liveStateService,
        IOptions<MqttOptions> options,
        ILogger<ProcessDroneStatusCommandHandler> logger)
    {
        _uavRepository = uavRepository;
        _liveStateService = liveStateService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessDroneStatusCommand request, CancellationToken cancellationToken)
    {
        var droneCode = ResolveDroneCode(request.TopicDroneCode, request.PayloadDroneCode);
        if (droneCode == null)
        {
            _logger.LogWarning("Rejected MQTT drone status because topic code {TopicDroneCode} does not match payload code {PayloadDroneCode}",
                request.TopicDroneCode, request.PayloadDroneCode);
            return false;
        }

        var uav = await _uavRepository.GetByUavCodeAsync(droneCode);
        if (uav == null)
        {
            _logger.LogWarning("Unknown drone code {DroneCode} in MQTT status message", droneCode);
            return false;
        }

        if (request.Battery is < 0 or > 100)
        {
            _logger.LogWarning("Rejected MQTT drone status for {DroneCode} because battery {Battery} is outside 0-100",
                droneCode, request.Battery);
            return false;
        }

        var isOnline = !string.Equals(request.Status, "offline", StringComparison.OrdinalIgnoreCase);
        await _liveStateService.UpdateStatusAsync(
            new DroneLiveStatus(
                uav.Id,
                uav.UavCode,
                isOnline,
                request.Battery,
                request.Timestamp ?? DateTime.UtcNow),
            TimeSpan.FromSeconds(_options.LiveStateTtlSeconds),
            cancellationToken);

        return true;
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
