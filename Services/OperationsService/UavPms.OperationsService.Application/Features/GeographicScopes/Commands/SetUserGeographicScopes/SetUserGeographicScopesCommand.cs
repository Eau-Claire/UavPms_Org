using System;
using System.Collections.Generic;
using MediatR;
using UavPms.OperationsService.Application.Features.GeographicScopes.DTOs;

namespace UavPms.OperationsService.Application.Features.GeographicScopes.Commands.SetUserGeographicScopes;

/// <summary>Replaces the user's geographic scope with exactly <see cref="RegionIds"/> (empty = remove all).</summary>
public record SetUserGeographicScopesCommand(Guid UserId, IReadOnlyList<Guid> RegionIds) : IRequest<UserGeographicScopesDto>;
