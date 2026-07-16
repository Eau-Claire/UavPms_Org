using UavPms.Application.Common.DTOs;

namespace UavPms.Application.Features.TransmissionLineDto.DTOs;

public record PaginatedTransmissionLinesResponse
(
    List<TransmissionLineDto> Items,
    PaginationMetaData Pagination
);