using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Commands.CreateTransmissionLine;

public class CreateTransmissionLineCommandHandler : IRequestHandler<CreateTransmissionLineCommand, TransmissionLineDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITransmissionLineRepository _transmissionLineRepository;
    private readonly ISubstationRepository _substationRepository;

    public CreateTransmissionLineCommandHandler(
        IUnitOfWork unitOfWork,
        ITransmissionLineRepository transmissionLineRepository,
        ISubstationRepository substationRepository)
    {
        _unitOfWork = unitOfWork;
        _transmissionLineRepository = transmissionLineRepository;
        _substationRepository = substationRepository;
    }
    
    public async Task<TransmissionLineDto> Handle(CreateTransmissionLineCommand request, CancellationToken cancellationToken)
    {
        var substation = await _substationRepository.GetByIdAsync(request.SubstationAssetId);
        if (substation == null || substation.IsDeleted)
        {
            throw new NotFoundException("Substation", request.SubstationAssetId);
        }

        Geometry? geom = null;
        if (!string.IsNullOrEmpty(request.GeomWkt))
        {
            var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
            
            var wktReader = new WKTReader(geometryFactory);

            try
            {
                geom = wktReader.Read(request.GeomWkt);
            }
            catch (Exception exception)
            {
                throw new AggregateException($"Định dạng hình học GeomWkt không hợp lệ: {exception.Message}");
            }
        }

        var line = new TransmissionLine
        {
            Id = Guid.NewGuid(),
            SubstationAssetId = request.SubstationAssetId,
            LineName = request.LineName,
            IsCriticalEdge = request.IsCriticalEdge,
            Geom = geom,
            CreatedAt = DateTime.UtcNow
        };
        
        await _transmissionLineRepository.AddAsync(line);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TransmissionLineDto(
            line.Id,
            line.SubstationAssetId,
            line.LineName,
            line.IsCriticalEdge,
            line.Geom?.AsText()
        );
    }
}