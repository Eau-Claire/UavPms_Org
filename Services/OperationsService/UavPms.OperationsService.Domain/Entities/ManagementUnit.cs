using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

public class ManagementUnit : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public string Status { get; set; } = "Active";
    public virtual ManagementUnit? Parent { get; set; }
    public virtual ICollection<ManagementUnit> Children { get; set; } = new List<ManagementUnit>();
    public virtual ICollection<TransmissionLine> PowerLines { get; set; } = new List<TransmissionLine>();
    public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
}
