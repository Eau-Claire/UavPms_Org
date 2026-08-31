using MediatR;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.AssetComponents.Queries.GetAssetHealthSummary;

public class GetAssetHealthSummaryQueryHandler : IRequestHandler<GetAssetHealthSummaryQuery, AssetHealthSummary>
{
    private readonly IAssetComponentRepository _assetRepository;

    public GetAssetHealthSummaryQueryHandler(IAssetComponentRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public Task<AssetHealthSummary> Handle(
        GetAssetHealthSummaryQuery request,
        CancellationToken cancellationToken)
        => _assetRepository.GetAssetHealthSummaryAsync(cancellationToken);
}
