using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>Mỗi tệp bằng chứng là một dòng, phân biệt bằng ProofType (Before/After/Document).</summary>
public class MaintenanceProof : BaseEntity
{
    public Guid TicketId { get; set; }
    public Guid UploadedBy { get; set; }
    public string ProofType { get; set; } = "After";
    public string FileUrl { get; set; } = string.Empty;
    public string TechnicianNotes { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public virtual MaintenanceTicket? Ticket { get; set; }
    public virtual User? Uploader { get; set; }
}
