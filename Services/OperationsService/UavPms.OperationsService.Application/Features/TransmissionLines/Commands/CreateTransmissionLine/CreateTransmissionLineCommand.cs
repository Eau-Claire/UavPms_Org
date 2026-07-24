using MediatR;
using UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

namespace UavPms.OperationsService.Application.Features.TransmissionLines.Commands.CreateTransmissionLine;

public record CreateTransmissionLineCommand(
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
) : IRequest<TransmissionLineDto>;