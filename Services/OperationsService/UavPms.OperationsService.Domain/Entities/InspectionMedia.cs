using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class InspectionMedia : BaseEntity
{
    public Guid MissionId { get; set; }

    /// <summary>NULL = video quét hành lang đi qua nhiều cột</summary>
    public Guid? TowerId { get; set; }
    public Guid? UploadedBy { get; set; }
    public Geometry? CaptureLocation { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) của tệp, dùng để chống trùng khi đồng bộ từ edge</summary>
    public string? Checksum { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? WidthPx { get; set; }
    public int? HeightPx { get; set; }
    public double? DurationSeconds { get; set; }
    public double? Fps { get; set; }
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
    public virtual User? Uploader { get; set; }
    public virtual Tower? Tower { get; set; }
    public virtual ICollection<AIAnalysisRun> AnalysisRuns { get; set; } = new List<AIAnalysisRun>();
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
