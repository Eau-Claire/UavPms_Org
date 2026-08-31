using System;
using System.Collections.Generic;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class MaintenanceTicket : BaseEntity
{
    public string TicketCode { get; set; } = string.Empty;
    public Guid AnomalyId { get; set; }
    public Guid TowerId { get; set; }
    public Guid? ComponentId { get; set; }
    public Guid ManagerId { get; set; }
    public Guid? TechnicianId { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public virtual DetectedAnomaly? Anomaly { get; set; }
    public virtual Tower? Tower { get; set; }
    public virtual AssetComponent? Component { get; set; }
    public virtual User? Manager { get; set; }
    public virtual User? Technician { get; set; }

    public virtual ICollection<MaintenanceProof> MaintenanceProofs { get; set; } = new List<MaintenanceProof>();
    public virtual ICollection<MaterialLog> MaterialLogs { get; set; } = new List<MaterialLog>();
    
    #region Rich Domain Methods
    public void AssignToTechnician(Guid technicianId)
    {
        TechnicianId = technicianId;
        AssignedAt = DateTime.UtcNow;
    }
    public void StartProgress()
    {
        if (Status != TicketStatus.Open)
        {
            throw new InvalidOperationException($"Cannot start progress on ticket in status '{Status}'. Must be Open.");
        }
        Status = TicketStatus.InProgress;
        StartedAt = DateTime.UtcNow;
    }
    public void SubmitForVerification()
    {
        if (Status != TicketStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot submit ticket for verification from status '{Status}'. Must be InProgress.");
        }
        Status = TicketStatus.PendingVerification;
    }
    public void Resolve()
    {
        Status = TicketStatus.Resolved;
        ResolvedAt = DateTime.UtcNow;
    }
    public void Close()
    {
        Status = TicketStatus.Closed;
    }
    #endregion
}
