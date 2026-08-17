using MediatR;
using UavPms.OperationsService.Application.Features.Drones.DTOs;

namespace UavPms.OperationsService.Application.Features.Drones.Queries.GetDroneStatus;

public sealed record GetDroneStatusQuery(Guid Id) : IRequest<DroneDto>;
