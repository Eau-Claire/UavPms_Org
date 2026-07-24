using MediatR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Common.DTOs;
using UavPms.OperationsService.Application.Features.Regions.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Regions.Queries.GetRegions;

public class GetRegionsQueryHandler : IRequestHandler<GetRegionsQuery, PaginatedRegionsResponse>
{
    private readonly IRegionRepository _regionRepository;

    public GetRegionsQueryHandler(IRegionRepository regionRepository)
    {
        _regionRepository = regionRepository;
    }

    public async Task<PaginatedRegionsResponse> Handle(GetRegionsQuery request, CancellationToken cancellationToken)
    {
        var (regions, totalCount) = await _regionRepository.GetRegionsPagedAsync(
            request.Page,
            request.PageSize,
            request.SearchTerm
        );

        var dtos = regions.Select(r => new RegionDto(
            r.Id,
            r.RegionName,
            r.Geom?.AsText()
        )).ToList();

        var totalPages = (int)Math.Ceiling((double)totalCount / request.PageSize);
        var pagination = new PaginationMetaData(request.Page, request.PageSize, totalCount, totalPages);

        return new PaginatedRegionsResponse(dtos, pagination);
    }
}
