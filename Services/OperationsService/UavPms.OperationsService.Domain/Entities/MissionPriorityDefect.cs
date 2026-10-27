namespace UavPms.OperationsService.Domain.Entities;

/// <summary>Junction mission ↔ loại lỗi được ưu tiên kiểm tra (thay cho PriorityDefectsJson).</summary>
public class MissionPriorityDefect
{
    public Guid MissionId { get; set; }
    public int CategoryId { get; set; }

    public virtual Mission? Mission { get; set; }
    public virtual DefectCategory? Category { get; set; }
}
