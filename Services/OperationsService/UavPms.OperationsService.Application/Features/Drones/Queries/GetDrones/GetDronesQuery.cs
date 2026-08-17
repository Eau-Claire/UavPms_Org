using MediatR;
using UavPms.OperationsService.Application.Features.Drones.DTOs;

namespace UavPms.OperationsService.Application.Features.Drones.Queries.GetDrones;

public sealed record GetDronesQuery(bool AvailableOnly = false) : IRequest<IReadOnlyList<DroneDto>>;
