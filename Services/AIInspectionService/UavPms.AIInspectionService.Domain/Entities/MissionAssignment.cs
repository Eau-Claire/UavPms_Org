using System;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

/// <summary>
/// Minimal read model of the shared MissionAssignments table (owned by OperationsService).
/// Used to resolve the inspector/pilot of a mission.
/// </summary>
public class MissionAssignment : BaseEntity
{
    public const string ActiveStatus = "Active";
    public const string InspectorRole = "INSPECTOR";
    public const string PilotRole = "PILOT";

    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>PILOT/INSPECTOR/OBSERVER/ANALYST/TECHNICIAN</summary>
    public string AssignmentRole { get; set; } = string.Empty;

    /// <summary>Active/Unavailable/Revoked</summary>
    public string Status { get; set; } = ActiveStatus;

    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
}
