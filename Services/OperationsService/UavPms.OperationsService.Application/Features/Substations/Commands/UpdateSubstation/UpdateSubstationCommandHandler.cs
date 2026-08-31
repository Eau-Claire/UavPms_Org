using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Substations.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Substations.Commands.UpdateSubstation;

public class UpdateSubstationCommandHandler : IRequestHandler<UpdateSubstationCommand, SubstationDto>
{
    private readonly ISubstationRepository _substationRepository;
    private readonly IRegionRepository _regionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSubstationCommandHandler(
        ISubstationRepository substationRepository,
        IRegionRepository regionRepository,
        IUnitOfWork unitOfWork)
    {
        _substationRepository = substationRepository;
        _regionRepository = regionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<SubstationDto> Handle(UpdateSubstationCommand request, CancellationToken cancellationToken)
    {
        var substation = await _substationRepository.GetByIdAsync(request.Id);
        if (substation == null || substation.IsDeleted)
        {
            throw new NotFoundException("Substation", request.Id);
        }

        var region = await _regionRepository.GetByIdAsync(request.RegionAssetId);
        if (region == null || region.IsDeleted)
        {
            throw new NotFoundException("Region", request.RegionAssetId);
        }

        Point? geom = null;
        if (request.Latitude.HasValue && request.Longitude.HasValue)
        {
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            geom = geometryFactory.CreatePoint(new Coordinate(request.Longitude.Value, request.Latitude.Value));
        }

        substation.RegionAssetId = request.RegionAssetId;
        substation.SubstationName = request.SubstationName;
        substation.VoltageLevel = request.VoltageLevel;
        substation.Geom = geom;
        substation.UpdatedAt = DateTime.UtcNow;
        
        await _substationRepository.UpdateAsync(substation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return new SubstationDto(
            substation.Id,
            substation.RegionAssetId,
            substation.SubstationName,
            substation.VoltageLevel,
            substation.Geom?.AsText()
        );
    }
}
