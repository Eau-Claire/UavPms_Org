using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.AssetComponents.DTOs;

public record PaginatedAssetComponentsResponse(
    List<AssetComponentDto> Items,
    PaginationMetaData Pagination
);