using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// A user's durable, administrative GIS scope.  Assignment access is deliberately
/// not stored here: it is derived from active missions and tickets at query time.
/// </summary>
public class UserGeographicScope : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? SubstationId { get; set; }
    public Guid? TransmissionLineId { get; set; }
    public Guid? ManagementUnitId { get; set; }

    public virtual User? User { get; set; }
}
