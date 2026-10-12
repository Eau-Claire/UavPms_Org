using System.Text.Json.Serialization;

namespace UavPms.OperationsService.Domain.Enums;

/// <summary>
/// Mức độ ưu tiên của nhiệm vụ bay (BR-01)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MissionPriority
{
    Normal = 0,      // Thường: Kiểm tra định kỳ, tái kiểm tra tiêu chuẩn
    High = 1,        // Cao: Tái kiểm tra khuyết tật tái diễn, tài sản cảnh báo sớm
    Emergency = 2    // Khẩn cấp: Báo cáo sự cố lưới điện, nguy cơ an toàn tức thì
}

/// <summary>
/// Mục tiêu kiểm tra của nhiệm vụ (độc lập với mức ưu tiên)
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InspectionObjective
{
    PeriodicInspection = 0,       // Kiểm tra định kỳ đường dây / cột điện
    TargetedReinspection = 1,     // Tái kiểm tra khuyết tật / điểm nghi vấn
    IncidentFollowUp = 2,         // Theo dõi sau sự cố lưới điện
    EmergencyInspection = 3       // Kiểm tra khẩn cấp phục vụ khắc phục sự cố
}

/// <summary>
/// Các nhóm khuyết tật ưu tiên tiêu chuẩn (Priority Defect Categories)
/// </summary>
public static class PriorityDefectCategories
{
    public const string Insulator = "INSULATOR";
    public const string Conductor = "CONDUCTOR";
    public const string SurgeArrester = "SURGE_ARRESTER";
    public const string ForeignObject = "FOREIGN_OBJECT";
    public const string TowerCorrosion = "TOWER_CORROSION";
    public const string Other = "OTHER";
}
