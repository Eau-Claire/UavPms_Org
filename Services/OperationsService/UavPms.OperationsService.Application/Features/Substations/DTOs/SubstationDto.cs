namespace UavPms.OperationsService.Application.Features.Substations.DTOs;

public record SubstationDto
(
    Guid Id,
    Guid RegionAssetId,
    string SubstationName,
    string VoltageLevel,
    string? GeomWkt
);