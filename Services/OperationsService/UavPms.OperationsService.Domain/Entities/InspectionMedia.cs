using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class InspectionMedia : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public Guid? TowerId { get; set; }
    public Geometry? CaptureLocation { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
    public virtual Asset? Asset { get; set; }
    public virtual Tower? Tower { get; set; }

    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
