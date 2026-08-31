namespace UavPms.OperationsService.Domain.Entities;

public class MissionTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MissionId { get; set; }
    public Guid TowerId { get; set; }
    public int Sequence { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual Tower? Tower { get; set; }
}
