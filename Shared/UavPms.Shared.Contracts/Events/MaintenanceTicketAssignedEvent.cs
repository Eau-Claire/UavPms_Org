namespace UavPms.Shared.Contracts.Events;

public sealed class MaintenanceTicketAssignedEvent
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public Guid TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid TowerId { get; set; }
    public Guid AssignedToUserId { get; set; }
    public string Priority { get; set; } = "High";
    public string? AnomalyDescription { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
