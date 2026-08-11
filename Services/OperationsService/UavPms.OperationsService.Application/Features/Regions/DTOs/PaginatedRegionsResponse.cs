using System.Collections.Generic;
using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Regions.DTOs;

public record PaginatedRegionsResponse(
    List<RegionDto> Items,
    PaginationMetaData Pagination
);