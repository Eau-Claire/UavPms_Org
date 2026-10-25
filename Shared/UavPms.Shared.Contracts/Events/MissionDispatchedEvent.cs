namespace UavPms.Shared.Contracts.Events;

public sealed class MissionDispatchedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid MissionId { get; set; }
    public string MissionCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid ManagerId { get; set; }
    public List<Guid> AssignedPilotIds { get; set; } = new();
    public Guid? UavId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public DateTime DispatchedAt { get; set; } = DateTime.UtcNow;
}
