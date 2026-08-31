using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Substations.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Substations.Commands.CreateSubstation;

public class CreateSubstationCommandHandler : IRequestHandler<CreateSubstationCommand, SubstationDto>
{
    private readonly ISubstationRepository _substationRepository;
    private readonly IRegionRepository _regionRepository;
    private readonly IUnitOfWork _unitOfWork;


    public CreateSubstationCommandHandler(
        ISubstationRepository substationRepository,
        IRegionRepository regionRepository,
        IUnitOfWork unitOfWork)
    {
        _substationRepository = substationRepository;
        _regionRepository = regionRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<SubstationDto> Handle(CreateSubstationCommand request, CancellationToken cancellationToken)
    {
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

        var substation = new Substation
        {
            Id = Guid.NewGuid(),
            RegionAssetId = request.RegionAssetId,
            SubstationName = request.SubstationName,
            VoltageLevel = request.VoltageLevel,
            Geom = geom,
            CreatedAt = DateTime.UtcNow,
        };

        await _substationRepository.AddAsync(substation);
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
