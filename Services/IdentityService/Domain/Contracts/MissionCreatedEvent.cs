using System;

namespace UavPms.IdentityService.Domain.Contracts;

public class MissionCreatedEvent
{
    public Guid MissionId { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RouteData { get; set; } = string.Empty;
    public Guid AssignedToUserId { get; set; }
    public Guid ManagerId { get; set; }
    public string DroneCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
