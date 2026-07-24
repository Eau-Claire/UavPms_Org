using UavPms.OperationsService.Application.Common.DTOs;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

public record PaginatedTransmissionLinesResponse
(
    List<TransmissionLineDto> Items,
    PaginationMetaData Pagination
);