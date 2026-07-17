using System;
using System.Collections.Generic;
using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class DetectedAnomaly : BaseEntity
{
    public Guid MediaId { get; set; }
    public Guid? AssetId { get; set; }
    public int CategoryId { get; set; }
    public Guid? AnalystId { get; set; }
    public string BoundingBox { get; set; } = string.Empty; // Will be mapped to jsonb
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
