namespace UavPms.IdentityService.Domain.Enums;

/// <summary>
/// Trạng thái xử lý của một yêu cầu phân tích AI.
/// </summary>
public enum AIAnalysisStatus
{
    /// <summary>Đang chờ xử lý</summary>
    Pending = 0,

    /// <summary>Đang được AI xử lý</summary>
    Processing = 1,

    /// <summary>Hoàn thành</summary>
    Completed = 2,

    /// <summary>Xử lý thất bại</summary>
    Failed = 3
}
