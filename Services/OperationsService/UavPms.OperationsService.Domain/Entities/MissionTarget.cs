using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class MissionTarget : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid TowerId { get; set; }
    public int Sequence { get; set; }
    public MissionTargetInspectionStatus InspectionStatus { get; set; } = MissionTargetInspectionStatus.Pending;
    public string? Notes { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual Tower? Tower { get; set; }
}
