using System;
using MediatR;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Commands.CreateAssetComponent;

public record CreateAssetComponentCommand(
    Guid TowerId,
    string ComponentType,
    string ComponentCode
) : IRequest<AssetComponentDto>;
