using MediatR;
using UavPms.Application.Features.Assets.DTOs;

namespace UavPms.Application.Features.Assets.Queries.GetAssetById;

public record GetAssetByIdQuery(Guid Id) : IRequest<AssetDetailDto>;