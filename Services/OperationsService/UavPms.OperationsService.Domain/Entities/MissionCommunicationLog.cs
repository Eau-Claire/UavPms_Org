using UavPms.OperationsService.Domain.Common;

namespace UavPms.OperationsService.Domain.Entities;

/// <summary>
/// Tin nhắn/nhật ký trao đổi của nhiệm vụ. Tên và vai trò người gửi suy ra qua SenderId → Users;
/// SenderId NULL = tin hệ thống.
/// </summary>
public class MissionCommunicationLog : BaseEntity
{
    public Guid MissionId { get; set; }
    public Guid? SenderId { get; set; }
    public string Type { get; set; } = "MESSAGE";       // "MESSAGE" | "DISPATCH" | "CONFIRM" | "POSTPONE" | "SUSPEND" | "RESUME" | "CANCEL" | "REMINDER" | "OVERDUE"
    public string Content { get; set; } = string.Empty;

    public virtual Mission? Mission { get; set; }
    public virtual User? Sender { get; set; }
}
