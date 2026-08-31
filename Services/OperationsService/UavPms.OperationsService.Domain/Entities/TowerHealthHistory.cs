using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class TowerHealthHistory : BaseEntity
{
    public Guid TowerId { get; set; }
    public double HealthScore { get; set; }
    public int ActiveDefectsCount { get; set; }
    public string CalculationLog { get; set; } = string.Empty; // Will be mapped to jsonb
    public string RiskLevel { get; set; } = string.Empty;
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    public virtual Tower? Tower { get; set; }
}
