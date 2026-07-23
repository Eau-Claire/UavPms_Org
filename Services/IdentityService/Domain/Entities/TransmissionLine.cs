using NetTopologySuite.Geometries;
using UavPms.IdentityService.Domain.Common;

namespace UavPms.IdentityService.Domain.Entities;

public class TransmissionLine : BaseEntity
{
    public Guid SubstationAssetId { get; set; }
    public string LineName { get; set; } = string.Empty;
    public bool IsCriticalEdge { get; set; }
    public Geometry? Geom { get; set; }

    public virtual Substation? Substation { get; set; }
    public virtual ICollection<Tower> Towers { get; set; } = new List<Tower>();
    public virtual ICollection<MissionTargetLine> MissionTargetLines { get; set; } = new List<MissionTargetLine>();
}
