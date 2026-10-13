using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;

namespace UavPms.OperationsService.Application.Features.FlightControllers;

public interface IFlightControllerService
{
    Task<FlightControllerResolveResponseDto> ResolveByUidAsync(string uid, CancellationToken ct = default);
    Task<FlightControllerBindingDto> PairFlightControllerAsync(Guid droneId, PairFlightControllerRequest request, CancellationToken ct = default);
    Task<bool> UnpairFlightControllerAsync(Guid droneId, CancellationToken ct = default);
    Task<FlightControllerBindingDto?> GetActiveBindingForDroneAsync(Guid droneId, CancellationToken ct = default);
    Task<IReadOnlyList<FlightControllerBindingDto>> GetBindingHistoryForDroneAsync(Guid droneId, CancellationToken ct = default);
}
