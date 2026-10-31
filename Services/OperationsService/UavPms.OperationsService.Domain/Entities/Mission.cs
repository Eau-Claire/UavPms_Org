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
    public Guid? UavId { get; set; }
    public MissionStatus Status { get; set; } = MissionStatus.Pending;
    public MissionPriority Priority { get; set; } = MissionPriority.Normal;
    public InspectionObjective Objective { get; set; } = InspectionObjective.PeriodicInspection;
    public bool IsImmediate { get; set; } = false;
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
    /// <summary>NULL = chưa gửi thông báo quá hạn xác nhận</summary>
    public DateTime? OverdueNotifiedAt { get; set; }
    public string? ManagerInstructions { get; set; }

    public virtual User? Manager { get; set; }
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

    public virtual ICollection<MissionPriorityDefect> PriorityDefects { get; set; } = new List<MissionPriorityDefect>();
    public virtual ICollection<MissionTarget> MissionTargets { get; set; } = new List<MissionTarget>();
    public virtual ICollection<MissionFlightLog> MissionFlightLogs { get; set; } = new List<MissionFlightLog>();
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<IncidentReport> IncidentReports { get; set; } = new List<IncidentReport>();
    
    #region Derived Data
    private static readonly string[] InspectorRoles = { "INSPECTOR", "PILOT" };

    /// <summary>
    /// Thanh tra viên chính của nhiệm vụ, suy ra từ MissionAssignments (vai trò INSPECTOR, sau đó PILOT).
    /// Cần nạp Assignments (và Assignments.User nếu dùng PrimaryInspector).
    /// </summary>
    public MissionAssignment? PrimaryInspectorAssignment() => Assignments
        .Where(a => !a.IsDeleted && a.Status == MissionAssignmentStatus.Active
            && InspectorRoles.Contains((a.AssignmentRole ?? string.Empty).ToUpperInvariant()))
        .OrderBy(a => string.Equals(a.AssignmentRole, "INSPECTOR", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
        .ThenBy(a => a.AssignedAt)
        .FirstOrDefault();

    public Guid? PrimaryInspectorId() => PrimaryInspectorAssignment()?.UserId;

    public User? PrimaryInspector() => PrimaryInspectorAssignment()?.User;

    /// <summary>Mã các loại lỗi ưu tiên (cần nạp PriorityDefects.Category).</summary>
    public List<string> PriorityDefectCodes() => PriorityDefects
        .Select(p => p.Category?.CategoryCode)
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Select(code => code!)
        .ToList();
    #endregion

    #region Rich Domain Methods

    public void Start(DateTime? startTime = null)
    {
        if (Status is not (MissionStatus.Ready or MissionStatus.Assigned or MissionStatus.Preparing))
            throw new InvalidOperationException($"Cannot start mission with status {Status}. Mission must be Ready, Assigned, or Preparing.");
        if (!AllRequiredAccepted())
            throw new InvalidOperationException("Cannot start mission before every required member has accepted.");

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

    public void Suspend(string reason)
    {
        if (Status is not (MissionStatus.Assigned or MissionStatus.Preparing or MissionStatus.Ready or MissionStatus.InProgress))
            throw new InvalidOperationException($"Cannot suspend mission with status {Status}. Only active or preparing missions can be suspended.");

        Status = MissionStatus.Suspended;
        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspended → In Progress when the mission had started. Otherwise the status is recomputed: Pending Acceptance
    /// while a required member has not accepted (BR-30, BR-32), else Assigned, Preparing or Ready (BR-37).
    /// </summary>
    public void Resume(string? reason = null)
    {
        if (Status != MissionStatus.Suspended)
            throw new InvalidOperationException($"Cannot resume mission with status {Status}. Only Suspended missions can be resumed.");

        if (StartedAt != null)
        {
            Status = MissionStatus.InProgress;
        }
        else
        {
            Status = MissionStatus.Assigned;
            RecalculateReadiness();
        }
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

    /// <summary>Active, not deleted assignments.</summary>
    public IEnumerable<MissionAssignment> ActiveAssignments() =>
        Assignments.Where(x => !x.IsDeleted && x.Status == MissionAssignmentStatus.Active);

    /// <summary>BR-30: at least one active required assignment, and all of them Accepted.</summary>
    public bool AllRequiredAccepted()
    {
        var required = ActiveAssignments().Where(a => a.IsRequired).ToList();
        return required.Count > 0 && required.All(a => a.ResponseStatus == MissionAssignmentResponse.Accepted);
    }

    /// <summary>
    /// Pending Acceptance → Assigned when every active required assignment is Accepted (BR-30). Readiness is then
    /// recomputed so check-ins and handovers made before a reassignment still count (BR-37).
    /// </summary>
    public bool CheckAcceptance()
    {
        if (Status != MissionStatus.PendingAcceptance) return false;
        if (!AllRequiredAccepted()) return false;

        Status = MissionStatus.Assigned;
        AcceptedAt = DateTime.UtcNow;
        RecalculateReadiness();
        Version++;
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    /// <summary>
    /// BR-37 readiness of a mission that has not started. Required members must accept and check in; an optional
    /// member must check in only once they have accepted. A required member who has not accepted sends the mission
    /// back to Pending Acceptance (BR-32). Pending Acceptance itself only changes through <see cref="CheckAcceptance"/>;
    /// Suspended, Postponed and final missions are left unchanged.
    /// </summary>
    public bool RecalculateReadiness()
    {
        if (Status is MissionStatus.Cancelled or MissionStatus.Completed or MissionStatus.InProgress or MissionStatus.Suspended or MissionStatus.Postponed)
            return false;
        if (StartedAt != null)
            return false;

        var active = ActiveAssignments().ToList();
        var hasUav = UavId.HasValue && UavId.Value != Guid.Empty;
        if (active.Count == 0)
        {
            Status = MissionStatus.Draft;
            return false;
        }

        if (!AllRequiredAccepted())
        {
            Status = MissionStatus.PendingAcceptance;
            return false;
        }
        if (Status == MissionStatus.PendingAcceptance)
            return CheckAcceptance() && Status == MissionStatus.Ready;
        if (!hasUav)
        {
            // Without a UAV the team can be complete but nothing can be prepared (no handover).
            Status = MissionStatus.Assigned;
            return false;
        }

        bool CheckedIn(Guid userId) => CheckIns.Any(c => !c.IsDeleted && c.UserId == userId && c.Status == MissionCheckInStatus.CheckedIn);
        var mustCheckIn = active.Where(a => a.IsRequired || a.ResponseStatus == MissionAssignmentResponse.Accepted).ToList();
        var openHandover = DroneHandovers.Any(h => !h.IsDeleted && h.DroneId == UavId!.Value && h.Status == DroneHandoverStatus.Accepted && h.ReturnedAt == null);

        var ready = mustCheckIn.All(a => CheckedIn(a.UserId))
            && MissionTargets.Count > 0
            && openHandover;

        var anyProgress = openHandover || active.Any(a => CheckedIn(a.UserId));
        Status = ready
            ? MissionStatus.Ready
            : anyProgress ? MissionStatus.Preparing : MissionStatus.Assigned;
        return ready;
    }
    #endregion
}
