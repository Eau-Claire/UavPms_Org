using MediatR;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.Substations.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.Substations.Queries.GetSubstationById;

public class GetSubstationByIdQueryHandler : IRequestHandler<GetSubstationByIdQuery, SubstationDto>
{
    private readonly ISubstationRepository _substationRepository;

    public GetSubstationByIdQueryHandler(ISubstationRepository substationRepository)
    {
        _substationRepository = substationRepository;
    }
    
    public async Task<SubstationDto> Handle(GetSubstationByIdQuery request, CancellationToken cancellationToken)
    {
        var substation = await _substationRepository.GetByIdAsync(request.Id);

        if (substation == null || substation.IsDeleted)
        {
            throw new NotFoundException("Substation", request.Id);
        }

        return new SubstationDto(
            substation.Id,
            substation.RegionAssetId,
            substation.SubstationName,
            substation.VoltageLevel,
            substation.Geom?.AsText()
        );
    }
}