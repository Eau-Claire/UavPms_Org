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
    public Guid? InspectorId { get; set; }
    public Guid? UavId { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.Pending;
    public MissionPriority Priority { get; set; } = MissionPriority.Normal;
    public InspectionObjective Objective { get; set; } = InspectionObjective.PeriodicInspection;
    public string PriorityDefectsJson { get; set; } = "[]";
    public string? EmergencyReason { get; set; }
    public bool IsImmediate { get; set; } = false;
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? RegionId { get; set; }
    public Guid? ScheduleId { get; set; }
    public MissionType MissionType { get; set; } = MissionType.AdHoc;
    public string? TriggerReason { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public Geometry? Boundary { get; set; }
    public uint Version { get; set; }
    public Guid? PreMissionAssessmentId { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime? PostponedAt { get; set; }
    public string? PostponeReason { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime? ConfirmationDeadline { get; set; }
    public bool IsOverdueNotified { get; set; } = false;
    public string? ManagerInstructions { get; set; }

    public virtual User? Manager { get; set; }
    public virtual User? Inspector { get; set; }
    public virtual User? AssignedToUser { get; set; }
    public virtual Uav? Uav { get; set; }
    public virtual Region? Region { get; set; }
    public virtual InspectionSchedule? Schedule { get; set; }
    public virtual PreMissionAssessment? PreMissionAssessment { get; set; }
    public virtual ICollection<MissionAssignment> Assignments { get; set; } = new List<MissionAssignment>();
    public virtual ICollection<MissionCheckIn> CheckIns { get; set; } = new List<MissionCheckIn>();
    public virtual ICollection<DroneHandover> DroneHandovers { get; set; } = new List<DroneHandover>();
    public virtual ICollection<ResourceBooking> ResourceBookings { get; set; } = new List<ResourceBooking>();
    public virtual ICollection<MissionCommunicationLog> CommunicationLogs { get; set; } = new List<MissionCommunicationLog>();

    public virtual ICollection<MissionTargetLine> MissionTargetLines { get; set; } = new List<MissionTargetLine>();
    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<MissionFlightLog> MissionFlightLogs { get; set; } = new List<MissionFlightLog>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    
    #region Rich Domain Methods

    public void Start(DateTime? startTime = null)
    {
        if (Status != MissionStatus.Ready)
            throw new InvalidOperationException("MISSION_NOT_READY");

        Status = MissionStatus.InProgress;
        StartedAt = startTime ?? DateTime.UtcNow;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Complete(DateTime? endTime = null)
    {
        if (Status != MissionStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete mission with status {Status}. Mission must be InProgress.");
        
        Status = MissionStatus.Completed;
        EndedAt = endTime ?? DateTime.UtcNow;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Confirm(string? reason = null)
    {
        if (Status is MissionStatus.InProgress or MissionStatus.Completed or MissionStatus.Cancelled)
            throw new InvalidOperationException($"Cannot confirm mission with status {Status}.");

        Status = MissionStatus.Assigned;
        AcceptedAt = DateTime.UtcNow;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Suspend(string reason)
    {
        if (Status is not (MissionStatus.Assigned or MissionStatus.Preparing or MissionStatus.Ready or MissionStatus.InProgress))
            throw new InvalidOperationException($"Cannot suspend mission with status {Status}. Only active or preparing missions can be suspended.");

        Status = MissionStatus.Suspended;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Resume(string? reason = null)
    {
        if (Status != MissionStatus.Suspended)
            throw new InvalidOperationException($"Cannot resume mission with status {Status}. Only Suspended missions can be resumed.");

        Status = StartedAt != null
            ? MissionStatus.InProgress
            : (RecalculateReadiness() ? MissionStatus.Ready : MissionStatus.Assigned);
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Postpone(string reason)
    {
        if (Status is not (MissionStatus.PendingAcceptance or MissionStatus.Assigned or MissionStatus.Preparing or MissionStatus.Ready))
            throw new InvalidOperationException($"Cannot postpone mission with status {Status}.");

        Status = MissionStatus.Postponed;
        PostponedAt = DateTime.UtcNow;
        PostponeReason = reason;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status is not (MissionStatus.Draft or MissionStatus.PendingAcceptance or MissionStatus.Assigned or MissionStatus.Preparing or MissionStatus.Ready or MissionStatus.Postponed or MissionStatus.Suspended))
            throw new InvalidOperationException($"Cannot cancel mission with status {Status}.");
        
        Status = MissionStatus.Cancelled;
        EndedAt = DateTime.UtcNow;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool CheckAcceptance()
    {
        if (Status != MissionStatus.PendingAcceptance) return false;
        var requiredAssignments = Assignments.Where(x => x.Status == MissionAssignmentStatus.Active && x.IsRequired).ToList();
        if (requiredAssignments.Count > 0 && requiredAssignments.All(a => a.ResponseStatus == MissionAssignmentResponse.Accepted))
        {
            Status = MissionStatus.Assigned;
            AcceptedAt = DateTime.UtcNow;
            Version++;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }
        return false;
    }

    public bool RecalculateReadiness()
    {
        if (Status is MissionStatus.Cancelled or MissionStatus.Completed or MissionStatus.InProgress or MissionStatus.PendingAcceptance or MissionStatus.Suspended or MissionStatus.Postponed)
            return false;

        var active = Assignments.Where(x => x.Status == MissionAssignmentStatus.Active).ToList();
        var hasUav = UavId.HasValue && UavId.Value != Guid.Empty;
        var ready = active.Count > 0
            && active.All(a => (!a.IsRequired || a.ResponseStatus == MissionAssignmentResponse.Accepted) && CheckIns.Any(c => c.UserId == a.UserId && c.Status == MissionCheckInStatus.CheckedIn))
            && hasUav
            && MissionTargets.Count > 0
            && DroneHandovers.Any(h => h.DroneId == UavId!.Value && h.Status == DroneHandoverStatus.Accepted && h.ReturnedAt == null);

        Status = ready
            ? MissionStatus.Ready
            : active.Count > 0 && hasUav
                ? CheckIns.Count > 0 || DroneHandovers.Count > 0 ? MissionStatus.Preparing : MissionStatus.Assigned
                : MissionStatus.Draft;
        return ready;
    }
    #endregion
}
