using MediatR;
using UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetComponentById;

public record GetAssetComponentByIdQuery(Guid Id) : IRequest<AssetComponentDetailDto>;