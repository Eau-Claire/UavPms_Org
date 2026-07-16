using System;

namespace UavPms.Application.Features.Regions.DTOs;

public record RegionDto(
    Guid Id,
    string RegionName,
    string? GeomWkt
);