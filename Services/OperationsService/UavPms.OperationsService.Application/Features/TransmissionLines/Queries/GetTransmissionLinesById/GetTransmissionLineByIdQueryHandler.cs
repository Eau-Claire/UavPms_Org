using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Queries.GetTransmissionLinesById;

public class GetTransmissionLineByIdQueryHandler : IRequestHandler<GetTransmissionLineByIdQuery, TransmissionLineDto>
{
    private readonly ITransmissionLineRepository _transmissionLineRepository;

    public GetTransmissionLineByIdQueryHandler(ITransmissionLineRepository transmissionLineRepository)
    {
        _transmissionLineRepository = transmissionLineRepository;
    }
    
    public async Task<TransmissionLineDto> Handle(GetTransmissionLineByIdQuery request, CancellationToken cancellationToken)
    {
        var line = await _transmissionLineRepository.GetByIdAsync(request.Id);

        if (line == null || line.IsDeleted)
        {
            throw new NotFoundException("TransmissionLine", request.Id);
        }

        return new TransmissionLineDto(
            line.Id,
            line.SubstationAssetId,
            line.LineName,
            line.IsCriticalEdge,
            line.Geom?.AsText()
        );
    }
}
