using MediatR;
using UavPms.Application.Features.TransmissionLines.DTOs;

namespace UavPms.Application.Features.TransmissionLines.Commands.UpdateTransmissionLine;

public record UpdateTransmissionLineCommand(
    Guid Id,
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
) : IRequest<TransmissionLineDto>;