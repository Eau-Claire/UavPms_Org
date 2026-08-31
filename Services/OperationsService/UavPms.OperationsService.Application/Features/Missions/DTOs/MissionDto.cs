namespace UavPms.OperationsService.Application.Features.Missions.DTOs;

public class MissionDto
{
    public Guid Id { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid RegionId { get; set; }
    public Guid InspectorId { get; set; }
    public string InspectorEmail { get; set; } = string.Empty;
    public Guid UavId { get; set; }
    public IReadOnlyList<Guid> TargetTowerIds { get; set; } = Array.Empty<Guid>();
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
    public string ManagerEmail { get; set; } = string.Empty;
    public DateTime? ScheduledStartAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
