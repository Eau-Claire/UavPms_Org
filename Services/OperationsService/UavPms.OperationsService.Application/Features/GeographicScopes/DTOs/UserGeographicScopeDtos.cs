using System;
using System.Collections.Generic;

namespace UavPms.OperationsService.Application.Features.GeographicScopes.DTOs;

public record UserGeographicScopeRegionDto(Guid RegionId, string Code, string Name, string Type);

/// <summary>Regions a user may access by default (direct child regions are included by the access filter).</summary>
public record UserGeographicScopesDto(Guid UserId, IReadOnlyList<UserGeographicScopeRegionDto> Regions);
