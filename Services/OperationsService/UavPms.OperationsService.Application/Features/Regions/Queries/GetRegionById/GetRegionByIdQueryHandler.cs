using MediatR;
using System.Threading;
using System.Threading.Tasks;
using UavPms.OperationsService.Application.Features.Regions.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Application.Common.Exceptions;

namespace UavPms.OperationsService.Application.Features.Regions.Queries.GetRegionById;

public class GetRegionByIdQueryHandler : IRequestHandler<GetRegionByIdQuery, RegionDto>
{
    private readonly IRegionRepository _regionRepository;

    public GetRegionByIdQueryHandler(IRegionRepository regionRepository)
    {
        _regionRepository = regionRepository;
    }

    public async Task<RegionDto> Handle(GetRegionByIdQuery request, CancellationToken cancellationToken)
    {
        var region = await _regionRepository.GetByIdAsync(request.Id);
        if (region == null || region.IsDeleted)
        {
            throw new NotFoundException("Region", request.Id);
        }

        return new RegionDto(region.Id, region.RegionName, region.Geom?.AsText());
    }
}
