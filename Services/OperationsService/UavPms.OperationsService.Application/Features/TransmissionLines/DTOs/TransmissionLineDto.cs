namespace UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

public record TransmissionLineDto
(
    Guid Id,
    Guid SubstationAssetId,
    string LineName,
    string Code,
    string VoltageLevel,
    bool IsCriticalEdge,
    string? GeomWkt
);