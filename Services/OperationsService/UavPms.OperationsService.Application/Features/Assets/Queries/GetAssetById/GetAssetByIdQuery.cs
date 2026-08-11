using MediatR;
using UavPms.OperationsService.Application.Features.Assets.DTOs;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetById;

public record GetAssetByIdQuery(Guid Id) : IRequest<AssetDetailDto>;