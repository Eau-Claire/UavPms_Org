using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Towers.DTOs;

public record PaginatedTowersResponse
(
    List<TowerDto> Items,
    PaginationMetaData Pagination
);