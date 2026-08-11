using System;

namespace UavPms.AIInspectionService.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? UserId { get; set; }
    public string TableName { get; set; } = string.Empty;
    public Guid RecordId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string OldValues { get; set; } = string.Empty; // Will be mapped to jsonb
    public string NewValues { get; set; } = string.Empty; // Will be mapped to jsonb
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User? User { get; set; }
}
