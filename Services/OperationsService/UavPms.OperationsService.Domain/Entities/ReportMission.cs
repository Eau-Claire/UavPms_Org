using System;

namespace UavPms.OperationsService.Domain.Entities;

public class ReportMission
{
    public Guid ReportId { get; set; }
    public Guid MissionId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    public virtual Report? Report { get; set; }
    public virtual Mission? Mission { get; set; }
}
