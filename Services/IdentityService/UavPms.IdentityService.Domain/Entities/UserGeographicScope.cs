using System;
using System.Text.Json.Serialization;
using UavPms.IdentityService.Domain.Common;

namespace UavPms.IdentityService.Domain.Entities;

public class UserGeographicScope : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? SubstationId { get; set; }
    public Guid? TransmissionLineId { get; set; }
    public Guid? ManagementUnitId { get; set; }

    [JsonIgnore]
    public virtual User? User { get; set; }
}
