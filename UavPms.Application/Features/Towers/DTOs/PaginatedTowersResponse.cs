using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Towers.DTOs;

public record PaginatedTowersResponse
(
    List<TowerDto> Items,
    PaginationMetaData Pagination
);