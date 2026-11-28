using System.Text.Json;

namespace UavPms.OperationsService.Application.Features.Assessments;

/// <summary>Số lượng nhân sự theo role của một hồ sơ MF01 (mỗi role tối thiểu 1).</summary>
public readonly record struct AssessmentHeadcount(int Inspectors, int Analysts, int Technicians);

/// <summary>
/// Khóa trong PreMissionAssessments.Findings (jsonb). Ghi mới luôn dùng khóa chuẩn camelCase;
/// khóa cũ chỉ còn được đọc để bản ghi tạo trước BE-REQ-101 vẫn hiển thị đúng số lượng.
/// </summary>
public static class AssessmentFindings
{
    public const string InspectorCount = "inspectorCount";
    public const string AnalystCount = "analystCount";
    public const string TechnicianCount = "technicianCount";
    public const string DroneIds = "droneIds";

    public static readonly string[] LegacyHeadcountKeys = { "requiredInspectors", "requiredAnalysts", "requiredTechnicians" };

    private const string LegacyInspectors = "requiredInspectors";
    private const string LegacyAnalysts = "requiredAnalysts";
    private const string LegacyTechnicians = "requiredTechnicians";

    /// <summary>Thiếu khóa hoặc JSON hỏng thì mỗi role mặc định 1, đúng mức tối thiểu của MF01.</summary>
    public static AssessmentHeadcount ReadHeadcount(string? findings)
    {
        if (string.IsNullOrWhiteSpace(findings))
            return new AssessmentHeadcount(1, 1, 1);

        try
        {
            using var doc = JsonDocument.Parse(findings);
            var root = doc.RootElement;
            return new AssessmentHeadcount(
                ReadCount(root, InspectorCount, LegacyInspectors),
                ReadCount(root, AnalystCount, LegacyAnalysts),
                ReadCount(root, TechnicianCount, LegacyTechnicians));
        }
        catch (JsonException)
        {
            return new AssessmentHeadcount(1, 1, 1);
        }
    }

    private static int ReadCount(JsonElement root, string key, string legacyKey)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return 1;
        if (root.TryGetProperty(key, out var value) && value.TryGetInt32(out var count))
            return Math.Max(1, count);
        if (root.TryGetProperty(legacyKey, out var legacyValue) && legacyValue.TryGetInt32(out var legacyCount))
            return Math.Max(1, legacyCount);
        return 1;
    }
}
