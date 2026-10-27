using System.Text.Json;
using System.Text.Json.Serialization;

namespace UavPms.OperationsService.Domain.Enums;

public class LenientMissionPriorityConverter : JsonConverter<MissionPriority>
{
    public override MissionPriority Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (MissionPriority)reader.GetInt32();
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString()?.Trim().ToUpperInvariant().Replace("_", "");
            return str switch
            {
                "HIGH" => MissionPriority.High,
                "EMERGENCY" => MissionPriority.Emergency,
                _ => MissionPriority.Normal
            };
        }
        return MissionPriority.Normal;
    }

    public override void Write(Utf8JsonWriter writer, MissionPriority value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

public class LenientInspectionObjectiveConverter : JsonConverter<InspectionObjective>
{
    public override InspectionObjective Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return (InspectionObjective)reader.GetInt32();
        }
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString()?.Trim().ToUpperInvariant().Replace("_", "");
            if (str?.Contains("TARGETED") == true || str?.Contains("REINSPECTION") == true) return InspectionObjective.TargetedReinspection;
            if (str?.Contains("INCIDENT") == true || str?.Contains("FOLLOWUP") == true) return InspectionObjective.IncidentFollowUp;
            if (str?.Contains("EMERGENCY") == true) return InspectionObjective.EmergencyInspection;
            return InspectionObjective.PeriodicInspection;
        }
        return InspectionObjective.PeriodicInspection;
    }

    public override void Write(Utf8JsonWriter writer, InspectionObjective value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}

/// <summary>
/// Mức độ ưu tiên của nhiệm vụ bay (BR-01)
/// </summary>
[JsonConverter(typeof(LenientMissionPriorityConverter))]
public enum MissionPriority
{
    Normal = 0,      // Thường: Kiểm tra định kỳ, tái kiểm tra tiêu chuẩn
    High = 1,        // Cao: Tái kiểm tra khuyết tật tái diễn, tài sản cảnh báo sớm
    Emergency = 2    // Khẩn cấp: Báo cáo sự cố lưới điện, nguy cơ an toàn tức thì
}

/// <summary>
/// Mục tiêu kiểm tra của nhiệm vụ (độc lập với mức ưu tiên)
/// </summary>
[JsonConverter(typeof(LenientInspectionObjectiveConverter))]
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
