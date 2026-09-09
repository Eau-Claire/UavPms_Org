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
    public Guid CreatedByUserId { get; set; }
    public virtual Region? Region { get; set; }
}

public class MissionAssignment : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public string AssignmentRole { get; set; } = string.Empty;
    public MissionAssignmentStatus Status { get; set; } = MissionAssignmentStatus.Active;
    public Guid AssignedByUserId { get; set; }
    public DateTime? EndedAt { get; set; }
    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
}

public class MissionCheckIn : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CheckedInAt { get; set; }
    public MissionCheckInStatus Status { get; set; } = MissionCheckInStatus.CheckedIn;
    public virtual Mission? Mission { get; set; }
    public virtual User? User { get; set; }
}

public class DroneHandover : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid DroneId { get; set; }
    public Guid HandedOverBy { get; set; }
    public Guid ReceivedBy { get; set; }
    public DateTime ReceivedAt { get; set; }
    public string Condition { get; set; } = string.Empty;
    public DroneHandoverStatus Status { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public virtual Mission? Mission { get; set; }
    public virtual Uav? Drone { get; set; }
}
