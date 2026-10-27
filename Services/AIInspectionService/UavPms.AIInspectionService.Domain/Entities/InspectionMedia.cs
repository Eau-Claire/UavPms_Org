using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class InspectionMedia : BaseEntity
{
    public Guid MissionId { get; set; }

    /// <summary>NULL = corridor video spanning towers / tower unknown.</summary>
    public Guid? TowerId { get; set; }
    public Guid? UploadedBy { get; set; }
    public Geometry? CaptureLocation { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>SHA-256 hex (64 chars), unique when not null.</summary>
    public string? Checksum { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }

    /// <summary>Video only.</summary>
    public double? DurationSeconds { get; set; }

    /// <summary>Video only.</summary>
    public double? Fps { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
    public virtual Tower? Tower { get; set; }
    public virtual User? Uploader { get; set; }

    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
    public virtual ICollection<AIAnalysisRun> AnalysisRuns { get; set; } = new List<AIAnalysisRun>();
}
