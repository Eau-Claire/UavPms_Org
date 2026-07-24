using MediatR;
using UavPms.OperationsService.Application.Features.Towers.DTOs;

namespace UavPms.OperationsService.Application.Features.Towers.Queries.GetTowerById;

public record GetTowerByIdQuery(Guid Id) : IRequest<TowerDto>;

