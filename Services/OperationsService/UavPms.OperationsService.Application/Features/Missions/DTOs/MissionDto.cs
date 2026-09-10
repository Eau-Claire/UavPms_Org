namespace UavPms.OperationsService.Application.Features.Missions.DTOs;

public class MissionDto
{
    public Guid Id { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RouteData { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public string AssignedToEmail { get; set; } = string.Empty;
    public string DroneCode { get; set; } = string.Empty;
    public Guid? InspectorId { get; set; }
    public string InspectorEmail { get; set; } = string.Empty;
    public Guid? UavId { get; set; }
    public DateTime? ScheduledStartAt { get; set; }
    public Guid? RegionId { get; set; }
    public string? RegionName { get; set; }
    public Guid? ScheduleId { get; set; }
    public string? ScheduleName { get; set; }
    public string MissionType { get; set; } = string.Empty;
    public string? TriggerReason { get; set; }
    public DateTime? PlannedStart { get; set; }
    public DateTime? PlannedEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualCompleted { get; set; }
    public string? BoundaryWkt { get; set; }
    public List<MissionAssignmentDto> Team { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
    public string ManagerEmail { get; set; } = string.Empty;
    public List<MissionTargetDto> Targets { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public record MissionAssignmentDto(Guid Id, Guid UserId, string UserName, string AssignmentRole, string Status, DateTime? CheckedInAt);

public class MissionTargetDto
{
    public Guid TowerId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public Guid? AssetId { get; set; }
    public string? AssetCode { get; set; }
    public string? AssetType { get; set; }
    public int Sequence { get; set; }
    public string InspectionStatus { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public Guid? PowerLineId { get; set; }
    public string? PowerLineCode { get; set; }
    public string? PowerLineName { get; set; }
}
