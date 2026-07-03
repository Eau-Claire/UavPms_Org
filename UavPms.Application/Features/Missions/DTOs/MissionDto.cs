namespace UavPms.Application.Features.Missions.DTOs;

public class MissionDto
{
    public Guid Id { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RouteData { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public string AssignedToUsername { get; set; } = string.Empty;
    public string DroneCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? ManagerId { get; set; }
    public string ManagerUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}