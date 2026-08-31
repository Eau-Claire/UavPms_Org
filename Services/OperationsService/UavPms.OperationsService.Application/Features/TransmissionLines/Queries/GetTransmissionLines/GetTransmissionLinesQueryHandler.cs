using MediatR;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLines;

public class GetTransmissionLinesQueryHandler : IRequestHandler<GetTransmissionLinesQuery, PaginatedTransmissionLinesResponse>
{
    private readonly ITransmissionLineRepository _transmissionLineRepository;

    public GetTransmissionLinesQueryHandler(
        ITransmissionLineRepository transmissionLineRepository)
    {
        _transmissionLineRepository = transmissionLineRepository;
    }
    
    public async Task<PaginatedTransmissionLinesResponse> Handle(GetTransmissionLinesQuery request, CancellationToken cancellationToken)
    {
        var (lines, totalCount) = await _transmissionLineRepository.GetTransmissionLinesPagedAsync(
            request.Page,
            request.PageSize,
            request.SubstationAssetId,
            request.SearchTerm);

        var dtos = lines.Select(l => new TransmissionLineDto(
            l.Id,
            l.SubstationAssetId,
            l.LineName,
            l.IsCriticalEdge,
            l.Geom?.AsText()
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);

        var pagination = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);
        
        return new PaginatedTransmissionLinesResponse(dtos, pagination);
    }
}
