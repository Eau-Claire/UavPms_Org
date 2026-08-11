using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Assets.DTOs;

namespace UavPms.OperationsService.Application.Features.Assets.Commands.CreateAsset;

public record CreateAssetCommand(
    Guid TowerId,
    string AssetType,
    string AssetCode
) : IRequest<AssetDto>;
