using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class Tower : BaseEntity
{
    public Guid? LineAssetId { get; set; }
    public string TowerCode { get; set; } = string.Empty;
    public string? LineCode { get; set; }
    public Geometry? Geom { get; set; }

    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
