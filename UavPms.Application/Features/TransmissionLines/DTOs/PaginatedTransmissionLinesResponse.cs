using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.TransmissionLines.DTOs;

public record PaginatedTransmissionLinesResponse
(
    List<TransmissionLineDto> Items,
    PaginationMetaData Pagination
);