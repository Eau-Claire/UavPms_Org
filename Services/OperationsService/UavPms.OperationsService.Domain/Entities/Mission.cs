using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class Mission : BaseEntity
{
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public Guid RegionId { get; set; }
    public Guid InspectorId { get; set; }
    public Guid UavId { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.Pending;
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Description { get; set; } = string.Empty;

    public virtual User? Manager { get; set; }
    public virtual Region? Region { get; set; }
    public virtual User? Inspector { get; set; }
    public virtual Uav? Uav { get; set; }

    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<MissionFlightLog> MissionFlightLogs { get; set; } = new List<MissionFlightLog>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    
    #region Rich Domain Methods

    public void Start(DateTime? startTime = null)
    {
        if(Status == MissionStatus.Completed || Status == MissionStatus.Cancelled)
            throw new InvalidOperationException($"Cannot start mission with status {Status}.");

        Status = MissionStatus.Executing;
        StartedAt = startTime ?? DateTime.UtcNow;
    }

    public void Complete(DateTime? endTime = null)
    {
        if(Status != MissionStatus.Executing)
            throw new InvalidOperationException($"Cannot complete mission with status {Status}.");
        
        Status = MissionStatus.Completed;
        EndedAt = endTime ?? DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status == MissionStatus.Completed || Status == MissionStatus.Cancelled)
            throw new InvalidOperationException($"Cannot cancel mission with status {Status}.");
        
        Status = MissionStatus.Cancelled;
        EndedAt = DateTime.UtcNow;
    }
    #endregion
}
