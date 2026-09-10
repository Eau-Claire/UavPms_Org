using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;
using NetTopologySuite.Geometries;

namespace UavPms.OperationsService.Domain.Entities;

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
    public MissionStatus Status { get; set; } = MissionStatus.Pending;
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid RegionId { get; set; }
    public Guid? ScheduleId { get; set; }
    public MissionType MissionType { get; set; } = MissionType.AdHoc;
    public string? TriggerReason { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public Geometry? Boundary { get; set; }
    public uint Version { get; set; }

    public virtual User? Manager { get; set; }
    public virtual User? Inspector { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual Uav? Uav { get; set; }
    public virtual Region? Region { get; set; }
    public virtual InspectionSchedule? Schedule { get; set; }
    public virtual ICollection<MissionAssignment> Assignments { get; set; } = new List<MissionAssignment>();
    public virtual ICollection<MissionCheckIn> CheckIns { get; set; } = new List<MissionCheckIn>();
    public virtual ICollection<DroneHandover> DroneHandovers { get; set; } = new List<DroneHandover>();

    public virtual ICollection<MissionTargetLine> MissionTargetLines { get; set; } = new List<MissionTargetLine>();
    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<MissionFlightLog> MissionFlightLogs { get; set; } = new List<MissionFlightLog>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    
    #region Rich Domain Methods

    public void Start(DateTime? startTime = null)
    {
        if(Status != MissionStatus.Ready)
            throw new InvalidOperationException("MISSION_NOT_READY");

        Status = MissionStatus.InProgress;
        StartedAt = startTime ?? DateTime.UtcNow;
    }

    public void Complete(DateTime? endTime = null)
    {
        if(Status != MissionStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete mission with status {Status}.");
        
        Status = MissionStatus.Completed;
        EndedAt = endTime ?? DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is not (MissionStatus.Draft or MissionStatus.Assigned or MissionStatus.Preparing or MissionStatus.Ready))
            throw new InvalidOperationException($"Cannot cancel mission with status {Status}.");
        
        Status = MissionStatus.Cancelled;
        EndedAt = DateTime.UtcNow;
    }

    public bool RecalculateReadiness()
    {
        if (Status is MissionStatus.Cancelled or MissionStatus.Completed or MissionStatus.InProgress) return false;
        var active = Assignments.Where(x => x.Status == MissionAssignmentStatus.Active).ToList();
        var ready = active.Count > 0
            && active.All(a => CheckIns.Any(c => c.UserId == a.UserId && c.Status == MissionCheckInStatus.CheckedIn))
            && UavId != Guid.Empty
            && MissionTargets.Count > 0
            && DroneHandovers.Any(h => h.DroneId == UavId && h.Status == DroneHandoverStatus.Accepted && h.ReturnedAt == null);
        Status = ready
            ? MissionStatus.Ready
            : active.Count > 0 && UavId != Guid.Empty
                ? CheckIns.Count > 0 || DroneHandovers.Count > 0 ? MissionStatus.Preparing : MissionStatus.Assigned
                : MissionStatus.Draft;
        return ready;
    }
    #endregion
}
