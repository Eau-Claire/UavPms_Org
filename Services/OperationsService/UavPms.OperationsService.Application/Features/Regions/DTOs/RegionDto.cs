using System;

namespace UavPms.OperationsService.Application.Features.Regions.DTOs;

public record RegionDto(
    Guid Id,
    string RegionName,
    string? GeomWkt
);