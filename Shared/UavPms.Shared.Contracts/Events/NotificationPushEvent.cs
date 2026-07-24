namespace UavPms.Shared.Contracts.Events;

public class NotificationPushEvent
{
    public Guid UserId { get; set; }
    public Guid NotificationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
}
