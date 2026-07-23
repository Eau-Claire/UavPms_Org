using System;
using System.Collections.Generic;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class DetectedAnomaly : BaseEntity
{
    public Guid MediaId { get; set; }
    public Guid? AssetId { get; set; }
    public int CategoryId { get; set; }
    public Guid? AnalystId { get; set; }
    public string BoundingBox { get; set; } = string.Empty; // Will be mapped to jsonb
    public string? AiDetectionId { get; set; }
    public int? FrameIndex { get; set; }
    public double? Timestamp { get; set; }
    public string? ImageUrl { get; set; }
    public string? CropUrl { get; set; }
    public string? Gps { get; set; } // Will be mapped to jsonb
    public string? TowerId { get; set; }
    public double? VideoDuration { get; set; }
    public double? VideoFps { get; set; }
    public int? VideoWidth { get; set; }
    public int? VideoHeight { get; set; }
    public double ConfidenceScore { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public string AnalystNotes { get; set; } = string.Empty;
    public DateTime? ValidatedAt { get; set; }

    public virtual InspectionMedia? Media { get; set; }
    public virtual Asset? Asset { get; set; }
    public virtual DefectCategory? Category { get; set; }
    public virtual User? Analyst { get; set; }

    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    public virtual ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();
}
