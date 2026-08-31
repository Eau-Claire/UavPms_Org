using MediatR;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetHealthSummary;

public record GetAssetHealthSummaryQuery : IRequest<AssetHealthSummary>;
