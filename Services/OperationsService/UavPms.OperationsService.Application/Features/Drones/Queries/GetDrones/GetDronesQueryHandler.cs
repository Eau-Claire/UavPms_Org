using MediatR;
using Microsoft.Extensions.Options;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Drones.DTOs;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;

namespace UavPms.OperationsService.Application.Features.Drones.Queries.GetDrones;

public class GetDronesQueryHandler : IRequestHandler<GetDronesQuery, IReadOnlyList<DroneDto>>
{
    private readonly IUavRepository _uavRepository;
    private readonly IDroneLiveStateService _liveStateService;
    private readonly MqttOptions _options;

    public GetDronesQueryHandler(
        IUavRepository uavRepository,
        IDroneLiveStateService liveStateService,
        IOptions<MqttOptions> options)
    {
        _uavRepository = uavRepository;
        _liveStateService = liveStateService;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<DroneDto>> Handle(GetDronesQuery request, CancellationToken cancellationToken)
    {
        var uavs = await _uavRepository.GetAllAsync();
        var drones = new List<DroneDto>();

        foreach (var uav in uavs)
        {
            var liveState = await _liveStateService.GetAsync(uav.UavCode, cancellationToken);
            var online = liveState != null
                && liveState.Online
                && DateTime.UtcNow - liveState.LastSeenAt <= TimeSpan.FromSeconds(_options.OfflineTimeoutSeconds);

            if (request.AvailableOnly && (!online || uav.Status != DroneStatus.Idle))
                continue;

            drones.Add(new DroneDto(
                uav.Id,
                uav.UavCode,
                uav.Model,
                online,
                liveState?.Battery ?? uav.BatteryLevel,
                uav.Status.ToString(),
                liveState?.LastSeenAt,
                liveState?.Latitude,
                liveState?.Longitude,
                liveState?.Altitude));
        }

        return drones;
    }
}
