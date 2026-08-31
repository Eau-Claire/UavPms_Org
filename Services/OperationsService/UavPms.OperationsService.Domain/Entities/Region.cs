using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class Region : BaseEntity
{
    public string RegionName { get; set; } = string.Empty;
    public MultiPolygon? Geom { get; set; }

    public virtual ICollection<Substation> Substations { get; set; } = new List<Substation>();
    public virtual ICollection<Mission> Missions { get; set; } = new List<Mission>();
    public virtual ICollection<UserRegionAssignment> UserAssignments { get; set; } = new List<UserRegionAssignment>();
}
