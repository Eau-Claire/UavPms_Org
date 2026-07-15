using System;
using System.Collections.Generic;
using UavPms.Core.Common;

namespace UavPms.Core.Entities;

public class InspectionMedia : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid? AssetId { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string AiSource { get; set; } = string.Empty;
    public string ValidationStatus { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public virtual Mission? Mission { get; set; }
    public virtual Asset? Asset { get; set; }

    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
