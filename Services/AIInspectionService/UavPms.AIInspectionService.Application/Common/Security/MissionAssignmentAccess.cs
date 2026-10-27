using UavPms.AIInspectionService.Domain.Entities;
using UavPms.AIInspectionService.Domain.Interfaces.Repositories;

namespace UavPms.AIInspectionService.Application.Common.Security;

/// <summary>
/// Missions no longer carry an InspectorId; the inspector/pilot of a mission comes from MissionAssignments.
/// </summary>
public static class MissionAssignmentAccess
{
    public static async Task<bool> IsAssignedFieldOperatorAsync(
        IGenericRepository<MissionAssignment> assignments,
        Guid missionId,
        Guid userId)
    {
        var rows = await assignments.FindAsync(a =>
            a.MissionId == missionId &&
            a.UserId == userId &&
            a.Status == MissionAssignment.ActiveStatus &&
            (a.AssignmentRole.ToUpper() == MissionAssignment.InspectorRole ||
             a.AssignmentRole.ToUpper() == MissionAssignment.PilotRole));
        return rows.Count > 0;
    }

    public static async Task<IReadOnlyList<Guid>> GetFieldOperatorIdsAsync(
        IGenericRepository<MissionAssignment> assignments,
        Guid missionId)
    {
        var rows = await assignments.FindAsync(a =>
            a.MissionId == missionId &&
            a.Status == MissionAssignment.ActiveStatus &&
            (a.AssignmentRole.ToUpper() == MissionAssignment.InspectorRole ||
             a.AssignmentRole.ToUpper() == MissionAssignment.PilotRole));
        return rows.Select(a => a.UserId).Distinct().ToList();
    }
}
