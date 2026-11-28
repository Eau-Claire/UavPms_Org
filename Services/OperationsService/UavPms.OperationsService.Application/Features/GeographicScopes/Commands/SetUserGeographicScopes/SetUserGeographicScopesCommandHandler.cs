using System;
using System.Collections.Generic;
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

namespace UavPms.OperationsService.Application.Features.GeographicScopes.Commands.SetUserGeographicScopes;

public class SetUserGeographicScopesCommandHandler : IRequestHandler<SetUserGeographicScopesCommand, UserGeographicScopesDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<UserGeographicScope> _scopeRepository;
    private readonly IGenericRepository<Region> _regionRepository;
    private readonly ICurrentUserServices _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public SetUserGeographicScopesCommandHandler(
        IUserRepository userRepository,
        IGenericRepository<UserGeographicScope> scopeRepository,
        IGenericRepository<Region> regionRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _scopeRepository = scopeRepository;
        _regionRepository = regionRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserGeographicScopesDto> Handle(SetUserGeographicScopesCommand request, CancellationToken cancellationToken)
    {
        var isSystemAdmin = _currentUser.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
        // A Manager without scope sees every region, so letting one edit their own scope is an escalation path.
        if (!isSystemAdmin && request.UserId == _currentUser.UserId)
        {
            throw new ForbiddenException("Only a SystemAdmin can change their own geographic scope.");
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, track: false);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var requested = (request.RegionIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (requested.Contains(Guid.Empty))
        {
            throw new BusinessRuleException("INVALID_REGION", "Region ID is invalid.");
        }

        // Manager chỉ gán / gỡ các vùng thuộc phạm vi của chính mình (vùng được gán và vùng con trực tiếp);
        // vùng khác của người dùng do Manager khác hoặc SystemAdmin quản lý nên được giữ nguyên.
        // Kiểm tra trước khi tra cứu vùng vì repository chỉ trả về vùng mà Manager được thấy.
        var managedRegionIds = isSystemAdmin ? null : await ManagedRegionIdsAsync();
        if (managedRegionIds != null && requested.Any(id => !managedRegionIds.Contains(id)))
        {
            throw new ForbiddenException("REGION_OUTSIDE_MANAGER_SCOPE");
        }

        var regions = await _regionRepository.FindAsync(r => requested.Contains(r.Id));
        var missing = requested.Except(regions.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"Region(s) not found: {string.Join(", ", missing)}");
        }

        // Soft-deleted rows are invisible here and excluded from the unique (UserId, RegionId) index,
        // so re-adding a removed region simply inserts a new row.
        var current = await _scopeRepository.FindAsync(s => s.UserId == request.UserId, track: true);
        foreach (var scope in current.Where(s => !requested.Contains(s.RegionId)
                     && (managedRegionIds == null || managedRegionIds.Contains(s.RegionId))))
        {
            await _scopeRepository.DeleteAsync(scope);
        }

        var existingRegionIds = current.Select(s => s.RegionId).ToHashSet();
        foreach (var regionId in requested.Where(id => !existingRegionIds.Contains(id)))
        {
            await _scopeRepository.AddAsync(new UserGeographicScope
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                RegionId = regionId,
                CreatedBy = _currentUser.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserGeographicScopesDto(
            request.UserId,
            regions.OrderBy(r => r.Code)
                .Select(r => new UserGeographicScopeRegionDto(r.Id, r.Code, r.Name, r.Type))
                .ToList());
    }

    /// <summary>Vùng Manager đang gọi được quản lý: vùng được gán cho chính họ và vùng con trực tiếp.</summary>
    private async Task<HashSet<Guid>> ManagedRegionIdsAsync()
    {
        var own = (await _scopeRepository.FindAsync(s => s.UserId == _currentUser.UserId))
            .Select(s => s.RegionId)
            .ToList();
        var children = await _regionRepository.FindAsync(r => r.ParentId != null && own.Contains(r.ParentId.Value));
        return own.Concat(children.Select(r => r.Id)).ToHashSet();
    }
}
