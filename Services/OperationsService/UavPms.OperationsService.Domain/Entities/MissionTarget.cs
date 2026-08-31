using System;
using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class MissionTarget : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid TowerId { get; set; }
    public int Sequence { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual Tower? Tower { get; set; }
}
