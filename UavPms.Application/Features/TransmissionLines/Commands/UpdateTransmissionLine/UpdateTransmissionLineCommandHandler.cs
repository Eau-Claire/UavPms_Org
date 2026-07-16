using System.Runtime.InteropServices.JavaScript;
using MediatR;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using UavPms.Application.Common.Exceptions;
using UavPms.Application.Features.TransmissionLines.DTOs;
using UavPms.Core.Interfaces.Repositories;

namespace UavPms.Application.Features.TransmissionLines.Commands.UpdateTransmissionLine;

public class UpdateTransmissionLineCommandHandler : IRequestHandler<UpdateTransmissionLineCommand, TransmissionLineDto>
{
    private readonly ITransmissionLineRepository _transmissionLineRepository;
    private readonly ISubstationRepository _substationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateTransmissionLineCommandHandler(
        ITransmissionLineRepository transmissionLineRepository,
        ISubstationRepository substationRepository,
        IUnitOfWork unitOfWork)
    {
        _transmissionLineRepository = transmissionLineRepository;
        _substationRepository = substationRepository;
        _unitOfWork = unitOfWork;
    }
    
    public async Task<TransmissionLineDto> Handle(UpdateTransmissionLineCommand request, CancellationToken cancellationToken)
    {
        var line = await _transmissionLineRepository.GetByIdAsync(request.Id);
        if (line == null || line.IsDeleted)
        {
            throw new NotFoundException("TransmissionLine", request.Id);
        }

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
            catch (Exception e)
            {
                throw new ArgumentException($"Định dạng hinh học GeomWkt không hợp lệ : {e.Message}");
            }
        }

        line.SubstationAssetId = request.SubstationAssetId;
        line.LineName = request.LineName;
        line.IsCriticalEdge = request.IsCriticalEdge;
        line.Geom = geom;
        line.UpdatedAt = DateTime.UtcNow;
        
        await _transmissionLineRepository.UpdateAsync(line);
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