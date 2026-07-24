namespace UavPms.NotificationService.Domain.Enums;

/// <summary>
/// Loại phân tích AI hỗ trợ cho ad-hoc analysis.
/// </summary>
public enum AnalysisType
{
    /// <summary>Phát hiện khuyết tật trên thiết bị/hạ tầng</summary>
    DefectDetection = 0,

    /// <summary>Phát hiện hành vi bất thường / xâm nhập</summary>
    HumanMotionDetection = 1,

    /// <summary>Phân loại vật thể chung</summary>
    ObjectClassification = 2,

    /// <summary>Đánh giá tình trạng tài sản</summary>
    AssetConditionAssessment = 3,

    /// <summary>Phân tích tổng hợp</summary>
    General = 4
}
