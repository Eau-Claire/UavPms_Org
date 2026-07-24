using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Assets.DTOs;

public record PaginatedAssetsResponse(
    List<AssetDto> Items,
    PaginationMetaData Pagination
);