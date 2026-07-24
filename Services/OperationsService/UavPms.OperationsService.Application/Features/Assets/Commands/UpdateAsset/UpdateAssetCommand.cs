using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Assets.DTOs;

namespace UavPms.OperationsService.Application.Features.Assets.Commands.UpdateAsset;

public record UpdateAssetCommand(
    Guid Id,
    Guid TowerId,
    string AssetType,
    string AssetCode,
    string Status,
    double CurrentHealthScore,
    string RiskLevel
) : IRequest<AssetDto>;
