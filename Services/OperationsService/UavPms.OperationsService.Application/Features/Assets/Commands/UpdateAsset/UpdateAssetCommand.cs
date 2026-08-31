using System;
using MediatR;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Commands.UpdateAssetComponent;

public record UpdateAssetComponentCommand(
    Guid Id,
    Guid TowerId,
    string ComponentType,
    string ComponentCode,
    string Status
) : IRequest<AssetComponentDto>;
