using System;
using MediatR;
using UavPms.Application.Features.Assets.DTOs;

namespace UavPms.Application.Features.Assets.Commands.CreateAsset;

public record CreateAssetCommand(
    Guid TowerId,
    string AssetType,
    string AssetCode
) : IRequest<AssetDto>;
