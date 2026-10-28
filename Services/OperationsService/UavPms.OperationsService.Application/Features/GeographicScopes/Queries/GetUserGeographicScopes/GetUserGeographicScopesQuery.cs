using System;
using MediatR;
using UavPms.OperationsService.Application.Features.GeographicScopes.DTOs;

namespace UavPms.OperationsService.Application.Features.GeographicScopes.Queries.GetUserGeographicScopes;

public record GetUserGeographicScopesQuery(Guid UserId) : IRequest<UserGeographicScopesDto>;
