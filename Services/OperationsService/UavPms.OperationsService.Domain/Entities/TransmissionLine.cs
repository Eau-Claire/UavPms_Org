using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class TransmissionLine : BaseEntity
{
    public Guid SubstationAssetId { get; set; }
    public string LineName { get; set; } = string.Empty;
    public bool IsCriticalEdge { get; set; }
    public LineString? Geom { get; set; }

    public virtual Substation? Substation { get; set; }
    public virtual ICollection<Tower> Towers { get; set; } = new List<Tower>();
}
