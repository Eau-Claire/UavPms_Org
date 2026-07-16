using MediatR;
using UavPms.Application.Features.Towers.DTOs;

namespace UavPms.Application.Features.Towers.Queries.GetTowerById;

public record GetTowerByIdQuery(Guid Id) : IRequest<TowerDto>;

