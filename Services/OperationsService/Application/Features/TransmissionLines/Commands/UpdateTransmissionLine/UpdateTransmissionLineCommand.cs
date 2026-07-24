using MediatR;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Commands.UpdateTransmissionLine;

public record UpdateTransmissionLineCommand(
    Guid Id,
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
) : IRequest<TransmissionLineDto>;