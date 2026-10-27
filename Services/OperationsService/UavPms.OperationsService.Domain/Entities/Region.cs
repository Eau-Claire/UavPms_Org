using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class Region : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Geometry? Geom { get; set; }

    public virtual Region? Parent { get; set; }
    public virtual ICollection<Region> Children { get; set; } = new List<Region>();
    public virtual ICollection<Tower> Towers { get; set; } = new List<Tower>();
}
