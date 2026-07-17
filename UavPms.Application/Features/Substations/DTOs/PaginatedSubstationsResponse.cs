using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.Substations.DTOs;

public record PaginatedSubstationsResponse
(
    List<SubstationDto> Items,
    PaginationMetaData Pagination
);