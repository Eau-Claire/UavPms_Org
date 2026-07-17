using MediatR;
using UavPms.Application.Features.Towers.DTOs;

namespace UavPms.Application.Features.Towers.Commands.UpdateTower;

public record UpdateTowerCommand(
    Guid Id,
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
) : IRequest<TowerDto>;