using MediatR;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetHealthSummary;

public record GetAssetHealthSummaryQuery : IRequest<AssetHealthSummary>;
