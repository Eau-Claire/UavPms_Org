using System;

namespace UavPms.AIInspectionService.Domain.Entities;

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

    /// <summary>NULL = unread.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>NULL = not pushed.</summary>
    public DateTime? PushedAt { get; set; }

    public virtual User? User { get; set; }
}
