using System;
using System.Collections.Generic;

namespace UavPms.OperationsService.Application.Features.Maintenance.DTOs;

public class CreateMaintenanceTicketRequest
{
    public Guid AnomalyId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Priority { get; set; } = "High";
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
}

public class UpdateTicketStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class LogMaterialRequest
{
    public string ComponentName { get; set; } = string.Empty;
    public string? ComponentCode { get; set; }
    public int QuantityUsed { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? FieldObservations { get; set; }
}

public class MaintenanceTicketDto
{
    public Guid Id { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public Guid AnomalyId { get; set; }
    public Guid? TowerId { get; set; }
    public Guid ManagerId { get; set; }
    public Guid TechnicianId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public object? Tower { get; set; }
    public object? Anomaly { get; set; }
    public object? Manager { get; set; }
    public object? Technician { get; set; }
    public List<MaintenanceProofDto> MaintenanceProofs { get; set; } = new();
    public List<MaterialLogDto> MaterialLogs { get; set; } = new();
}

public class MaintenanceProofDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid UploadedBy { get; set; }
    public string ProofType { get; set; } = "After";
    public string FileUrl { get; set; } = string.Empty;
    public string AfterRepairImageUrl { get; set; } = string.Empty;
    public string TechnicianNotes { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}

public class MaterialLogDto
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid LoggedBy { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string ComponentCode { get; set; } = string.Empty;
    public int QuantityUsed { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string FieldObservations { get; set; } = string.Empty;
    public DateTime LoggedAt { get; set; }
}

public class PagedTicketsDto
{
    public IReadOnlyList<MaintenanceTicketDto> Items { get; set; } = Array.Empty<MaintenanceTicketDto>();
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
