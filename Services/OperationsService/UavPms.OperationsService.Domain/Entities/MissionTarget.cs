namespace UavPms.OperationsService.Domain.Entities;

public class MissionTarget : UavPms.OperationsService.Domain.Common.BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid AssetId { get; set; }
    public int Sequence { get; set; }
    public UavPms.OperationsService.Domain.Enums.MissionTargetInspectionStatus InspectionStatus { get; set; }
        = UavPms.OperationsService.Domain.Enums.MissionTargetInspectionStatus.Pending;
    public string? Notes { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual Asset? Asset { get; set; }
}
