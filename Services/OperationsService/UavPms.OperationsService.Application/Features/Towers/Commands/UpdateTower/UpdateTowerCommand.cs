using MediatR;
using UavPms.OperationsService.Application.Features.Towers.DTOs;

namespace UavPms.OperationsService.Application.Features.Towers.Commands.UpdateTower;

public record UpdateTowerCommand(
    Guid Id,
    Guid LineAssetId,
    string TowerCode,
    double Latitude,
    double Longitude
) : IRequest<TowerDto>;