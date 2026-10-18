using System;

namespace UavPms.OperationsService.Application.Features.Alerts.DTOs;

public class AlertLocationDto
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? TowerCode { get; set; }
}

public class AlertEvidenceDto
{
    public string? MediaUrl { get; set; }
    public string? BoundingBox { get; set; }
    public double? ConfidenceScore { get; set; }
}

public class ActiveEmergencyAlertDto
{
    public Guid Id { get; set; }
    public Guid AnomalyId { get; set; }
    public Guid? AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DeliveryLatencySeconds { get; set; }
    public DateTime TriggeredAt { get; set; }
    public AlertLocationDto Location { get; set; } = new();
    public AlertEvidenceDto Evidence { get; set; } = new();
}

public class EmergencyAlertHistoryDto
{
    public Guid Id { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? Notes { get; set; }
}

public class PaginatedAlertsResponse
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<EmergencyAlertHistoryDto> Items { get; set; } = new();
}

public class AlertEscalationDto
{
    public Guid Id { get; set; }
    public Guid EscalatedBy { get; set; }
    public string EscalatedByName { get; set; } = string.Empty;
    public Guid EscalatedTo { get; set; }
    public string EscalatedToName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime EscalatedAt { get; set; }
}

public class EmergencyAlertDetailDto
{
    public Guid Id { get; set; }
    public Guid AnomalyId { get; set; }
    public Guid? AssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public Guid MissionId { get; set; }
    public string AlertType { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int DeliveryLatencySeconds { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public string? Notes { get; set; }
    public AlertLocationDto Location { get; set; } = new();
    public AlertEvidenceDto Evidence { get; set; } = new();
    public List<AlertEscalationDto> Escalations { get; set; } = new();
}

public class ReviewEmergencyAlertRequest
{
    public string Status { get; set; } = "Confirmed";
    public string? Notes { get; set; }
}

public class EscalateEmergencyAlertRequest
{
    public Guid EscalatedToUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class TriggerEmergencyAlertRequest
{
    public Guid? MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? AnomalyId { get; set; }
    public string? AlertType { get; set; } = "Wildfire / Flashover";
    public string? Priority { get; set; } = "Critical";
    public int DeliveryLatencySeconds { get; set; } = 3;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? TowerCode { get; set; }
    public string? MediaUrl { get; set; }
    public string? BoundingBox { get; set; }
    public double? ConfidenceScore { get; set; }
}
