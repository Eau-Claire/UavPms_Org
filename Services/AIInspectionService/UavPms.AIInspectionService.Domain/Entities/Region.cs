using NetTopologySuite.Geometries;
using UavPms.AIInspectionService.Domain.Common;

namespace UavPms.AIInspectionService.Domain.Entities;

public class Region : BaseEntity
{
    public string RegionName { get; set; } = string.Empty;
    public Geometry? Geom { get; set; }

    public virtual ICollection<Substation> Substations { get; set; } = new List<Substation>();
}
