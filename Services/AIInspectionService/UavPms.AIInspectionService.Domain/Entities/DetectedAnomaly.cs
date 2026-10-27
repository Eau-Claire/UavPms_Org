using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class DetectedAnomaly : BaseEntity
{
    public const string ManualSource = "Manual";

    public Guid MediaId { get; set; }

    /// <summary>AI run that produced this anomaly; NULL = manual.</summary>
    public Guid? AnalysisRunId { get; set; }

    /// <summary>Anomaly (Cloud result) that replaced this one (Edge result).</summary>
    public Guid? SupersededById { get; set; }

    /// <summary>Tower the defect is on; required before the anomaly can be Confirmed.</summary>
    public Guid? TowerId { get; set; }
    public int CategoryId { get; set; }
    public Guid? AnalystId { get; set; }
    public string BoundingBox { get; set; } = string.Empty; // Will be mapped to jsonb

    /// <summary>Detection id from the AI worker; Edge = TrackId.</summary>
    public string? AiDetectionId { get; set; }
    public int? FrameIndex { get; set; }
    public double? FrameTimeSeconds { get; set; }
    public string? ImageUrl { get; set; }
    public string? CropUrl { get; set; }

    /// <summary>geometry(Point,4326)</summary>
    public Point? Location { get; set; }
    public double ConfidenceScore { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public string AnalystNotes { get; set; } = string.Empty;
    public DateTime? ValidatedAt { get; set; }

    public virtual InspectionMedia? Media { get; set; }
    public virtual AIAnalysisRun? AnalysisRun { get; set; }
    public virtual DetectedAnomaly? SupersededBy { get; set; }
    public virtual Tower? Tower { get; set; }
    public virtual DefectCategory? Category { get; set; }
    public virtual User? Analyst { get; set; }

    #region Rich domain methods

    public void Confirm(Guid analystId, string notes)
    {
        AnalystId = analystId;
        AnalystNotes = notes ?? string.Empty;
        ValidationStatus = "Confirmed";
        ValidatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid analystId, string notes)
    {
        AnalystId = analystId;
        AnalystNotes = notes ?? string.Empty;
        ValidationStatus = "Rejected";
        ValidatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Derived "AI source": "Manual" when not produced by an AI run, otherwise the run's model name
    /// (falling back to its execution location).
    /// </summary>
    public string GetAiSource()
    {
        if (AnalysisRunId == null) return ManualSource;
        if (AnalysisRun == null) return string.Empty;
        return string.IsNullOrWhiteSpace(AnalysisRun.ModelName) ? AnalysisRun.ExecutionLocation : AnalysisRun.ModelName;
    }

    #endregion
}
