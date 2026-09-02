using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class Region : BaseEntity
{
    public string RegionName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public Geometry? Geom { get; set; }

    public virtual ICollection<Substation> Substations { get; set; } = new List<Substation>();
    public virtual Region? Parent { get; set; }
    public virtual ICollection<Region> Children { get; set; } = new List<Region>();
}
