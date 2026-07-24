using System.Collections.Generic;

namespace UavPms.NotificationService.Domain.Entities;

public class DefectCategory
{
    public int Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public double SeverityWeight { get; set; }
    public bool IsEmergencyClass { get; set; }
    public string Description { get; set; } = string.Empty;

    public virtual ICollection<DetectedAnomaly> DetectedAnomalies { get; set; } = new List<DetectedAnomaly>();
}
