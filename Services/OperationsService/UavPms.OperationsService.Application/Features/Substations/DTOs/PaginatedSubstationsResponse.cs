using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.Substations.DTOs;

public record PaginatedSubstationsResponse
(
    List<SubstationDto> Items,
    PaginationMetaData Pagination
);