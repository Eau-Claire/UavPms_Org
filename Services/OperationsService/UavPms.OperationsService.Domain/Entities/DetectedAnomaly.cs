using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class DetectedAnomaly : BaseEntity
{
    public const string ManualSource = "Manual";

    public Guid MediaId { get; set; }

    /// <summary>Lần chạy AI sinh ra bất thường; NULL = tạo tay</summary>
    public Guid? AnalysisRunId { get; set; }

    /// <summary>Bản edge được kết quả cloud xác nhận/thay thế sẽ trỏ sang bản cloud</summary>
    public Guid? SupersededById { get; set; }

    /// <summary>Bắt buộc trước khi xác nhận (Confirmed)</summary>
    public Guid? TowerId { get; set; }
    public int CategoryId { get; set; }
    public Guid? AnalystId { get; set; }
    public string BoundingBox { get; set; } = string.Empty; // jsonb
    public string? AiDetectionId { get; set; }
    public int? FrameIndex { get; set; }
    public double? FrameTimeSeconds { get; set; }
    public Point? Location { get; set; }
    public string? ImageUrl { get; set; }
    public string? CropUrl { get; set; }
    public double ConfidenceScore { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string AnalystNotes { get; set; } = string.Empty;
    public string? AnalystFeedback { get; set; } = "{}";
    public DateTime? ValidatedAt { get; set; }

    public virtual InspectionMedia? Media { get; set; }
    public virtual AIAnalysisRun? AnalysisRun { get; set; }
    public virtual DetectedAnomaly? SupersededBy { get; set; }
    public virtual Tower? Tower { get; set; }
    public virtual DefectCategory? Category { get; set; }
    public virtual User? Analyst { get; set; }
    public virtual ICollection<EmergencyAlert> EmergencyAlerts { get; set; } = new List<EmergencyAlert>();
    public virtual ICollection<MaintenanceTicket> MaintenanceTickets { get; set; } = new List<MaintenanceTicket>();

    #region Rich Domain Methods
    public void Validate(Guid analystId, string notes, bool isConfirmed)
    {
        if (isConfirmed && !TowerId.HasValue)
            throw new InvalidOperationException("Anomaly must be linked to a tower before it can be confirmed.");

        AnalystId = analystId;
        AnalystNotes = notes ?? string.Empty;
        ValidationStatus = isConfirmed ? "Confirmed" : "Rejected";
        ValidatedAt = DateTime.UtcNow;
    }
    #endregion
}
