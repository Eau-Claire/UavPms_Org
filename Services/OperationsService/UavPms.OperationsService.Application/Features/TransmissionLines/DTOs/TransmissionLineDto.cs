namespace UavPms.OperationsService.Application.Features.TransmissionLines.DTOs;

public record TransmissionLineDto
(
    Guid Id,
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
);