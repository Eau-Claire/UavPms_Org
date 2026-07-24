using MediatR;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLines;

public record GetTransmissionLinesQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? SubstationAssetId = null,
    string? SearchTerm = null
) : IRequest<PaginatedTransmissionLinesResponse>;