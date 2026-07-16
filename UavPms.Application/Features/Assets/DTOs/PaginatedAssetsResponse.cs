using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Assets.DTOs;

public record PaginatedAssetsResponse(
    List<AssetDto> Items,
    PaginationMetaData Pagination
);