using NetTopologySuite.Geometries;
using UavPms.NotificationService.Domain.Common;

namespace UavPms.NotificationService.Domain.Entities;

public class Substation : BaseEntity
{
    public Guid RegionAssetId { get; set; }
    public string SubstationName { get; set; } = string.Empty;
    public string VoltageLevel { get; set; } = string.Empty;
    public Geometry? Geom { get; set; }

    public virtual Region? Region { get; set; }
    public virtual ICollection<TransmissionLine> TransmissionLines { get; set; } = new List<TransmissionLine>();
}
