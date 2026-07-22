using MediatR;
using UavPms.Application.Common.DTOs;
using UavPms.Application.Features.Substations.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Substations.Queries.GetSubstation;

public class GetSubstationQueryHandler : IRequestHandler<GetSubstaionQuery, PaginatedSubstationsResponse>
{
    private readonly ISubstationRepository _substationRepository;

    public GetSubstationQueryHandler(ISubstationRepository substationRepository)
    {
        _substationRepository = substationRepository;
    }
    
    public async Task<PaginatedSubstationsResponse> Handle(GetSubstaionQuery request, CancellationToken cancellationToken)
    {
        var (substrations, totalCount) = await _substationRepository.GetSubstationsPagedAsync(
            request.Page,
            request.PageSize,
            request.RegionAssetId,
            request.SearchTerm
        );
        
        var dtos = substrations.Select(s => new SubstationDto(
            s.Id,
            s.RegionAssetId,
            s.SubstationName,
            s.VoltageLevel,
            s.Geom?.AsText()
            )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagination = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);
        
        return new PaginatedSubstationsResponse(dtos, pagination);
    }
}