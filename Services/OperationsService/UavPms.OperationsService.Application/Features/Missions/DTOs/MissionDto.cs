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
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
    public string ManagerEmail { get; set; } = string.Empty;
    public List<MissionTargetDto> Targets { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MissionTargetDto
{
    public Guid AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public string InspectionStatus { get; set; } = string.Empty;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
