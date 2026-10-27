using System;

namespace UavPms.NotificationService.Domain.Entities;

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null = unread. Read state is derived from this timestamp.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Null = not yet pushed. Push state is derived from this timestamp.</summary>
    public DateTime? PushedAt { get; set; }

    public virtual User? User { get; set; }


    public void MarkAsRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }

    public void MarkAsPushed()
    {
        PushedAt ??= DateTime.UtcNow;
    }
}
