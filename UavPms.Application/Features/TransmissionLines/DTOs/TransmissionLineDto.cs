namespace UavPms.Application.Features.TransmissionLineDto.DTOs;

public record TransmissionLineDto
(
    Guid Id,
    Guid SubstationAssetId,
    string LineName,
    bool IsCriticalEdge,
    string? GeomWkt
);