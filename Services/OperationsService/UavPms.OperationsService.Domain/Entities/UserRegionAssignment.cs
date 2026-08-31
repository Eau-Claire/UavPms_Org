namespace UavPms.OperationsService.Domain.Entities;

public class UserRegionAssignment
{
    public Guid UserId { get; set; }
    public Guid RegionId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }
    public Guid AssignedBy { get; set; }

    public virtual Region Region { get; set; } = null!;
}
