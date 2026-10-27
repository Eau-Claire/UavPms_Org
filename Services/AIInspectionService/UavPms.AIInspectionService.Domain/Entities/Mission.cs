using System;
using System.Collections.Generic;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

/// <summary>
/// Subset of the shared Missions table. The inspector/pilot of a mission comes from <see cref="Assignments"/>.
/// </summary>
public class Mission : BaseEntity
{
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public Guid? UavId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? Description { get; set; }

    public virtual User? Manager { get; set; }
    public virtual Uav? Uav { get; set; }

    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<MissionAssignment> Assignments { get; set; } = new List<MissionAssignment>();
}
