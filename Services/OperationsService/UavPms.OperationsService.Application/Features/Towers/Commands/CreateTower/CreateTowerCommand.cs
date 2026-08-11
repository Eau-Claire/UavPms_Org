using MediatR;
using UavPms.OperationsService.Application.Features.Towers.DTOs;

namespace UavPms.OperationsService.Application.Features.Towers.Commands.CreateTower;

public record CreateTowerCommand(
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
) : IRequest<TowerDto>;