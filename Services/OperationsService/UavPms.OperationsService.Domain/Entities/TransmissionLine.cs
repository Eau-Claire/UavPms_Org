using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class TransmissionLine : BaseEntity
{
    public Guid SubstationAssetId { get; set; }
    public string LineName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string VoltageLevel { get; set; } = string.Empty;
    public Guid? ManagementUnitId { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsCriticalEdge { get; set; }
    public Geometry? Geom { get; set; }

    public virtual Substation? Substation { get; set; }
    public virtual ICollection<Tower> Towers { get; set; } = new List<Tower>();
    public virtual ICollection<MissionTargetLine> MissionTargetLines { get; set; } = new List<MissionTargetLine>();
    public virtual ManagementUnit? ManagementUnit { get; set; }
    public virtual ICollection<LineSegment> LineSegments { get; set; } = new List<LineSegment>();
    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
