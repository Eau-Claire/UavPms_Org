using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Regions.DTOs;

public record PaginatedRegionsResponse
(
    List<RegionDto> Items,
    PaginationMetaData Pagination
);