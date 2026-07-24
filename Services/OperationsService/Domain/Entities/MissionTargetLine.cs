using System;

namespace UavPms.OperationsService.Domain.Entities;

public class MissionTargetLine
{
    public Guid MissionId { get; set; }
    public Guid LineAssetId { get; set; }
    public string Status { get; set; } = string.Empty;

    public virtual Mission? Mission { get; set; }
    public virtual TransmissionLine? TransmissionLine { get; set; }
}
