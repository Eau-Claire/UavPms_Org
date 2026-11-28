using System.Text.Json;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments;

/// <summary>Nguồn của phiếu kiểm tra kỹ thuật Drone (cột DroneTechnicalInspections.SourceType).</summary>
public static class InspectionSourceTypes
{
    public const string Manual = "manual";
    public const string FlightController = "fc";
    public const string Bist = "bist";

    public static bool IsValid(string value) =>
        value is Manual or FlightController or Bist;
}

/// <summary>Kết luận do Technician chốt khi gửi phiếu kiểm tra.</summary>
public static class InspectionConclusions
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
}

/// <summary>Trạng thái sẵn sàng của Drone suy ra từ TechnicalHealth (không cần thêm cột DB).</summary>
public static class DroneReadiness
{
    public const string Ready = "Ready";
    public const string MaintenanceRequired = "MaintenanceRequired";

    // Chỉ Healthy mới đạt chuẩn; Unknown/Warning/Critical đều phải bảo trì hoặc kiểm tra lại trước khi vào MF01.
    public static string FromHealth(TechnicalHealth health) =>
        health == TechnicalHealth.Healthy ? Ready : MaintenanceRequired;
}

/// <summary>RawSnapshot của phiếu kiểm tra: lưu các hạng mục bắt buộc còn thiếu để UI báo đỏ và cho nhập bổ sung.</summary>
public static class DroneInspectionSnapshot
{
    public const string MissingMetricsKey = "missingMetrics";

    public static IReadOnlyList<string> ReadMissingMetrics(string? rawSnapshot)
    {
        if (string.IsNullOrWhiteSpace(rawSnapshot))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(rawSnapshot);
            if (!doc.RootElement.TryGetProperty(MissingMetricsKey, out var array) || array.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            return array.EnumerateArray()
                .Select(item => item.GetString())
                .OfType<string>()
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}