using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class InspectionSchedule : BaseEntity
{
    public Guid RegionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RecurrenceRule { get; set; } = string.Empty;
    public TimeOnly PlannedTime { get; set; }
    public bool IsActive { get; set; } = true;
    public virtual Region? Region { get; set; }
}

public class MissionAssignment : BaseEntity
{
    /// <summary>Giá trị AssignmentRole lưu trong DB (MF02 chọn đích danh 3 role).</summary>
    public const string InspectorRole = "INSPECTOR";
    public const string AnalystRole = "ANALYST";
    public const string TechnicianRole = "TECHNICIAN";

    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public MissionAssignmentStatus Status { get; set; } = MissionAssignmentStatus.Active;
    public MissionAssignmentResponse ResponseStatus { get; set; } = MissionAssignmentResponse.Pending;
    public bool IsRequired { get; set; } = true;
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }
    public string? ResponseReason { get; set; }
    public uint Version { get; set; } = 1;
    public Guid AssignedById { get; set; }
    public DateTime? EndedAt { get; set; }
    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
    public virtual User? AssignedBy { get; set; }

    public bool HasRole(string role) => string.Equals(AssignmentRole, role, StringComparison.OrdinalIgnoreCase);
}

public sealed class ResourceBooking : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? DroneId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public ResourceBookingStatus Status { get; set; } = ResourceBookingStatus.Active;
    public Mission? Mission { get; set; }
    public User? User { get; set; }
    public Uav? Drone { get; set; }
}

public class MissionCheckIn : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public Geometry? CheckInLocation { get; set; }
    public DateTime CheckedInAt { get; set; }
    public MissionCheckInStatus Status { get; set; } = MissionCheckInStatus.CheckedIn;
    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
}
