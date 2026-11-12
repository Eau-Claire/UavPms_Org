using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class DetectedAnomaly : BaseEntity
{
    public const string ManualSource = "Manual";

    // State machine: InReview (AI result received, awaiting analyst) -> Confirmed | Rejected.
    // "Pending" is kept only for rows created before InReview existed and is treated as awaiting review.
    public const string StatusPending = "Pending";
    public const string StatusInReview = "InReview";
    public const string StatusConfirmed = "Confirmed";
    public const string StatusRejected = "Rejected";
    public const string StatusResolved = "Resolved";

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

    #region Rich domain methods

    public bool IsAwaitingReview =>
        ValidationStatus is StatusInReview or StatusPending || string.IsNullOrWhiteSpace(ValidationStatus);

    public void Confirm(Guid analystId, string? notes, string? feedbackJson = null)
    {
        AnalystId = analystId;
        AnalystNotes = notes ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(feedbackJson))
        {
            AnalystFeedback = feedbackJson;
        }
        else if (string.IsNullOrWhiteSpace(AnalystFeedback))
        {
            AnalystFeedback = "{}";
        }
        ValidationStatus = StatusConfirmed;
        ValidatedAt = DateTime.UtcNow;
    }

    public void Reject(Guid analystId, string? notes, string? feedbackJson = null)
    {
        AnalystId = analystId;
        AnalystNotes = notes ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(feedbackJson))
        {
            AnalystFeedback = feedbackJson;
        }
        else if (string.IsNullOrWhiteSpace(AnalystFeedback))
        {
            AnalystFeedback = "{}";
        }
        ValidationStatus = StatusRejected;
        ValidatedAt = DateTime.UtcNow;
    }

    public bool IsEmergencySeverity(string? overrideSeverity = null)
    {
        if (string.Equals(overrideSeverity, "Critical", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(overrideSeverity, "Critical Risk", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(overrideSeverity, "Emergency", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(overrideSeverity, "Urgent", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Category != null && (Category.IsEmergencyClass || Category.SeverityWeight >= 4);
    }

    public static bool TryParseReviewDecision(string? decisionOrStatus, out bool isApproved)
    {
        isApproved = false;
        if (string.IsNullOrWhiteSpace(decisionOrStatus))
            return false;

        var normalized = decisionOrStatus.Trim();
        if (string.Equals(normalized, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Confirm", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Approve", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Accepted", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Accept", StringComparison.OrdinalIgnoreCase))
        {
            isApproved = true;
            return true;
        }

        if (string.Equals(normalized, "Rejected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Reject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Denied", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "Dismissed", StringComparison.OrdinalIgnoreCase))
        {
            isApproved = false;
            return true;
        }

        return false;
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
