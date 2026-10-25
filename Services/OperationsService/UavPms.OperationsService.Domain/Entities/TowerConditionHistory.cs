using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class TowerConditionHistory : BaseEntity
{
    public Guid TowerId { get; set; }
    public string RiskLevel { get; set; } = "Normal";
    public int ActiveDefectsCount { get; set; } = 0;
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;

    public virtual Tower? Tower { get; set; }
}
