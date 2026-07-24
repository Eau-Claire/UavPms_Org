using NetTopologySuite.Geometries;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class Region : BaseEntity
{
    public string RegionName { get; set; } = string.Empty;
    public Geometry? Geom { get; set; }

    public virtual ICollection<Substation> Substations { get; set; } = new List<Substation>();
}
