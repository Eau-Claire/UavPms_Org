using System;
using UavPms.IdentityService.Domain.Common;

namespace UavPms.IdentityService.Domain.Entities;

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
