using MediatR;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Assets.Queries.GetAssetHealthSummary;

public class GetAssetHealthSummaryQueryHandler : IRequestHandler<GetAssetHealthSummaryQuery, AssetHealthSummary>
{
    private readonly IAssetRepository _assetRepository;

    public GetAssetHealthSummaryQueryHandler(IAssetRepository assetRepository)
    {
        _assetRepository = assetRepository;
    }

    public Task<AssetHealthSummary> Handle(
        GetAssetHealthSummaryQuery request,
        CancellationToken cancellationToken)
        => _assetRepository.GetAssetHealthSummaryAsync(cancellationToken);
}
