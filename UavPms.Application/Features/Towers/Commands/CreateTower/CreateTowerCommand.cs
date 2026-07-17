using MediatR;
using UavPms.Application.Features.Towers.DTOs;

namespace UavPms.Application.Features.Towers.Commands.CreateTower;

public record CreateTowerCommand(
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
) : IRequest<TowerDto>;