using System;
using System.Collections.Generic;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

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
    public DateTime? ConfirmationDeadline { get; set; }
    public bool IsOverdueNotified { get; set; } = false;
    public string? ManagerInstructions { get; set; }

    public virtual User? Manager { get; set; }
    public virtual User? Inspector { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual ICollection<MissionCommunicationLog> CommunicationLogs { get; set; } = new List<MissionCommunicationLog>();
    public virtual ICollection<MissionAssignment> Assignments { get; set; } = new List<MissionAssignment>();
}
