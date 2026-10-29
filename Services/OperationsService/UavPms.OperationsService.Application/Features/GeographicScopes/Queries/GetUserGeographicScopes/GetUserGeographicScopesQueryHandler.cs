using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.GeographicScopes.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.GeographicScopes.Queries.GetUserGeographicScopes;

public class GetUserGeographicScopesQueryHandler : IRequestHandler<GetUserGeographicScopesQuery, UserGeographicScopesDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<UserGeographicScope> _scopeRepository;
    private readonly IGenericRepository<Region> _regionRepository;
    private readonly ICurrentUserServices _currentUser;

    public GetUserGeographicScopesQueryHandler(
        IUserRepository userRepository,
        IGenericRepository<UserGeographicScope> scopeRepository,
        IGenericRepository<Region> regionRepository,
        ICurrentUserServices currentUser)
    {
        _userRepository = userRepository;
        _scopeRepository = scopeRepository;
        _regionRepository = regionRepository;
        _currentUser = currentUser;
    }

    public async Task<UserGeographicScopesDto> Handle(GetUserGeographicScopesQuery request, CancellationToken cancellationToken)
    {
        // SystemAdmin/Manager can read anyone's scope; other roles only their own.
        var canReadOthers = _currentUser.Roles.Any(r =>
            string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));
        if (!canReadOthers && request.UserId != _currentUser.UserId)
        {
            throw new ForbiddenException("You can only view your own geographic scope.");
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, track: false);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var scopes = await _scopeRepository.FindAsync(s => s.UserId == request.UserId);
        var regionIds = scopes.Select(s => s.RegionId).Distinct().ToList();
        var regions = await _regionRepository.FindAsync(r => regionIds.Contains(r.Id));

        return new UserGeographicScopesDto(
            request.UserId,
            regions.OrderBy(r => r.Code)
                .Select(r => new UserGeographicScopeRegionDto(r.Id, r.Code, r.Name, r.Type))
                .ToList());
    }
}
