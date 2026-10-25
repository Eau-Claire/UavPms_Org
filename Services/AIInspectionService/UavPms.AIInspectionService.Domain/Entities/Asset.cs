using System;
using System.Collections.Generic;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class Asset : BaseEntity
{
    public Guid TowerId { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string AssetCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double CurrentHealthScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime? LastInspectedAt { get; set; }

    public virtual Tower? Tower { get; set; }
    public virtual ICollection<InspectionMedia> InspectionMedias { get; set; } = new List<InspectionMedia>();
    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
