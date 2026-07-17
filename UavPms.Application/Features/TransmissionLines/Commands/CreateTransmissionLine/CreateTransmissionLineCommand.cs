using MediatR;
using UavPms.Application.Features.TransmissionLines.DTOs;

namespace UavPms.Application.Features.TransmissionLines.Commands.CreateTransmissionLine;

public record CreateTransmissionLineCommand(
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
) : IRequest<TransmissionLineDto>;