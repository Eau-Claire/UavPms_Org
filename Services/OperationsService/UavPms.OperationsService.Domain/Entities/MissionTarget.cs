using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class MissionTarget : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid AssetId { get; set; }
    public int Sequence { get; set; }
    public string InspectionStatus { get; set; } = "Pending";
    public string? Notes { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual Asset? Asset { get; set; }
}
