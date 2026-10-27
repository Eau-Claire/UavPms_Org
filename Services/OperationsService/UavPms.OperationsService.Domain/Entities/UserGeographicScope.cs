using System.Text.Json.Serialization;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// A user's durable, administrative GIS scope: one row per region the user may access.
/// Assignment access is deliberately not stored here: it is derived from active missions
/// and tickets at query time.
/// </summary>
public class UserGeographicScope : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid RegionId { get; set; }

    [JsonIgnore]
    public virtual User? User { get; set; }
    [JsonIgnore]
    public virtual Region? Region { get; set; }
}
