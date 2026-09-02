using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

// Electrical topology only; Sequence is not a UAV waypoint or flight instruction.
public class LineSegment : BaseEntity
{
    public Guid PowerLineId { get; set; }
    public Guid FromAssetId { get; set; }
    public Guid ToAssetId { get; set; }
    public int Sequence { get; set; }
    public Geometry? Geometry { get; set; }
    public string Status { get; set; } = "Active";
    public virtual TransmissionLine? PowerLine { get; set; }
    public virtual Asset? FromAsset { get; set; }
    public virtual Asset? ToAsset { get; set; }
}
