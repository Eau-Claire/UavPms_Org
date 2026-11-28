using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UavPms.AIInspectionService.Domain.Common;
using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;
using UavPms.AIInspectionService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.AIInspectionService.Application.Common.Security;

/// <summary>
/// Quyền trên nhiệm vụ và defect của nhiệm vụ (spec §4), dùng chung cho mọi API của AIInspectionService.
/// "Trong vùng" = vùng được gán trong UserGeographicScopes và vùng con trực tiếp.
/// </summary>
public interface IMissionAccessPolicy
{
    /// <summary>Phạm vi để lọc danh sách defect trong SQL.</summary>
    Task<MissionVisibility> GetVisibilityAsync();

    /// <summary>
    /// Xem defect: SystemAdmin, Manager phụ trách, thành viên được giao, Manager/Analyst của vùng.
    /// Defect không gắn nhiệm vụ (<paramref name="mission"/> null) chỉ SystemAdmin xem.
    /// </summary>
    Task<bool> CanViewAsync(Mission? mission);

    /// <summary>Duyệt defect và đổi cột: SystemAdmin, Analyst được giao hoặc Analyst của vùng.</summary>
    Task<bool> CanReviewAsync(Mission? mission);

    /// <summary>
    /// Upload ảnh/video: vào nhiệm vụ là SystemAdmin, Inspector/Analyst được giao hoặc Analyst của vùng;
    /// standalone (<paramref name="mission"/> null) là SystemAdmin và Analyst. Manager không upload.
    /// </summary>
    Task<bool> CanUploadAsync(Mission? mission);
}

public sealed class MissionAccessPolicy : IMissionAccessPolicy
{
    private readonly IGenericRepository<MissionAssignment> _assignments;
    private readonly IGenericRepository<UserGeographicScope> _scopes;
    private readonly IGenericRepository<Region> _regions;
    private readonly ICurrentUserServices _currentUser;
    private IReadOnlyCollection<Guid>? _regionIds;

    public MissionAccessPolicy(
        IGenericRepository<MissionAssignment> assignments,
        IGenericRepository<UserGeographicScope> scopes,
        IGenericRepository<Region> regions,
        ICurrentUserServices currentUser)
    {
        _assignments = assignments;
        _scopes = scopes;
        _regions = regions;
        _currentUser = currentUser;
    }

    public async Task<MissionVisibility> GetVisibilityAsync()
    {
        if (HasRole(UserRoles.SystemAdmin))
            return MissionVisibility.Everything;

        // Chỉ Manager và Analyst được thấy defect theo vùng; Inspector/Technician chỉ thấy nhiệm vụ được giao.
        var regionIds = HasRole(UserRoles.Manager) || HasRole(UserRoles.Analyst)
            ? await RegionIdsAsync()
            : Array.Empty<Guid>();
        return new MissionVisibility(false, _currentUser.UserId, regionIds);
    }

    public async Task<bool> CanViewAsync(Mission? mission)
    {
        if (HasRole(UserRoles.SystemAdmin))
            return true;
        if (mission == null)
            return false;
        if (mission.ManagerId == _currentUser.UserId)
            return true;
        if (await IsAssignedAsync(mission.Id, role: null))
            return true;
        return (HasRole(UserRoles.Manager) || HasRole(UserRoles.Analyst)) && await IsInRegionAsync(mission);
    }

    public async Task<bool> CanReviewAsync(Mission? mission)
    {
        if (HasRole(UserRoles.SystemAdmin))
            return true;
        if (mission == null || !HasRole(UserRoles.Analyst))
            return false;
        return await IsAssignedAsync(mission.Id, MissionAssignment.AnalystRole) || await IsInRegionAsync(mission);
    }

    public async Task<bool> CanUploadAsync(Mission? mission)
    {
        if (HasRole(UserRoles.SystemAdmin))
            return true;
        if (mission == null)
            return HasRole(UserRoles.Analyst);

        // Vai trò phân công phải khớp role của tài khoản (giống OperationsService).
        var isInspector = HasRole(UserRoles.Inspector);
        var isAnalyst = HasRole(UserRoles.Analyst);
        if ((isInspector && await IsAssignedAsync(mission.Id, MissionAssignment.InspectorRole)) ||
            (isAnalyst && await IsAssignedAsync(mission.Id, MissionAssignment.AnalystRole)))
            return true;
        return isAnalyst && await IsInRegionAsync(mission);
    }

    private bool HasRole(string role) => _currentUser.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    private async Task<bool> IsAssignedAsync(Guid missionId, string? role)
    {
        var userId = _currentUser.UserId;
        var rows = await _assignments.FindAsync(a =>
            a.MissionId == missionId &&
            a.UserId == userId &&
            a.Status == MissionAssignment.ActiveStatus);
        return rows.Any(a => role == null || string.Equals(a.AssignmentRole, role, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<bool> IsInRegionAsync(Mission mission) =>
        mission.RegionId is Guid regionId && (await RegionIdsAsync()).Contains(regionId);

    /// <summary>Vùng được gán và vùng con trực tiếp; tính một lần cho mỗi request.</summary>
    private async Task<IReadOnlyCollection<Guid>> RegionIdsAsync()
    {
        if (_regionIds != null)
            return _regionIds;

        var userId = _currentUser.UserId;
        var granted = (await _scopes.FindAsync(s => s.UserId == userId)).Select(s => s.RegionId).ToList();
        var children = await _regions.FindAsync(r => r.ParentId != null && granted.Contains(r.ParentId.Value));
        _regionIds = granted.Concat(children.Select(r => r.Id)).ToHashSet();
        return _regionIds;
    }
}
