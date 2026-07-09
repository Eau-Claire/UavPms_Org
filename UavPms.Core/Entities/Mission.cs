using System;
using System.Collections.Generic;
using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class Mission : BaseEntity
{
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RouteData { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public string DroneCode { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public Guid InspectorId { get; set; }
    public Guid UavId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Description { get; set; } = string.Empty;

    public virtual User? Manager { get; set; }
    public virtual User? Inspector { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual Uav? Uav { get; set; }

    public virtual ICollection<MissionTargetLine> MissionTargetLines { get; set; } = new List<MissionTargetLine>();
    public virtual ICollection<MissionFlightLog> MissionFlightLogs { get; set; } = new List<MissionFlightLog>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
}
