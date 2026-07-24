using System;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class MaintenanceProof : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid UploadedBy { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string AfterRepairImageUrl { get; set; } = string.Empty;
    public string TechnicianNotes { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public virtual MaintenanceTicket? Ticket { get; set; }
    public virtual User? Uploader { get; set; }
}
