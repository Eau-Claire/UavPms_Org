using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class Tower : BaseEntity
{
    public Guid RegionId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public string? PoleType { get; set; }
    public string? Address { get; set; }
    public Geometry? Geom { get; set; }
}
