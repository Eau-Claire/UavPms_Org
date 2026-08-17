using MediatR;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Drones.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Drones.Queries.GetDroneStatus;

public class GetDroneStatusQueryHandler : IRequestHandler<GetDroneStatusQuery, DroneDto>
{
    private readonly IUavRepository _uavRepository;
    private readonly IDroneLiveStateService _liveStateService;
    private readonly MqttOptions _options;

    public GetDroneStatusQueryHandler(
        IUavRepository uavRepository,
        IDroneLiveStateService liveStateService,
        IOptions<MqttOptions> options)
    {
        _uavRepository = uavRepository;
        _liveStateService = liveStateService;
        _options = options.Value;
    }

    public async Task<DroneDto> Handle(GetDroneStatusQuery request, CancellationToken cancellationToken)
    {
        var uav = await _uavRepository.GetByIdAsync(request.Id, false);
        if (uav == null)
            throw new NotFoundException("Drone", request.Id);

        var liveState = await _liveStateService.GetAsync(uav.UavCode, cancellationToken);
        var online = liveState != null
            && liveState.Online
            && DateTime.UtcNow - liveState.LastSeenAt <= TimeSpan.FromSeconds(_options.OfflineTimeoutSeconds);

        return new DroneDto(
            uav.Id,
            uav.UavCode,
            uav.Model,
            online,
            liveState?.Battery ?? uav.BatteryLevel,
            uav.Status.ToString(),
            liveState?.LastSeenAt,
            liveState?.Latitude,
            liveState?.Longitude,
            liveState?.Altitude);
    }
}
