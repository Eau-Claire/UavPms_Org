using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments;

public static class AssessmentMappingExtensions
{
    public static PreMissionAssessmentDto ToDto(this PreMissionAssessment entity)
    {
        var statusStr = entity.Status.ToString();

        var towers = entity.Towers
            .OrderBy(a => a.Sequence)
            .Select(a => new PreMissionAssessmentTowerDto(
                a.Id,
                a.TowerId,
                a.Sequence,
                a.Tower?.TowerCode,
                a.Tower?.PoleType
            ))
            .ToList();

        var personnel = entity.PersonnelCandidates
            .Select(p => new PreMissionAssessmentPersonnelDto(
                p.Id,
                p.UserId,
                p.User?.FullName ?? string.Empty,
                p.Role,
                p.IsEligible,
                p.EligibilityStatus.ToString(),
                p.AvailabilityStatus.ToString(),
                p.ReasonCode,
                p.Findings
            ))
            .ToList();

        var drones = entity.DroneCandidates
            .Select(d => new PreMissionAssessmentDroneDto(
                d.Id,
                d.DroneId,
                d.Drone?.UavCode ?? d.DroneId.ToString()[..8],
                d.Drone?.Model ?? "UAV",
                d.IsEligible,
                d.OperationalAvailabilityStatus.ToString(),
                d.TechnicalHealth.ToString(),
                d.TechnicalInspectionId,
                d.ReasonCode,
                d.Drone?.BatteryLevel ?? 100,
                d.Suitability.ToString().ToUpperInvariant(),
                d.EstimatedFlightsNeeded,
                d.CoverageDistanceKm,
                d.SuitabilityReason,
                d.OperationalAvailabilityStatus == ResourceAvailabilityStatus.Available,
                d.TechnicalEligibilityStatus.ToString()
            ))
            .ToList();

        var isReadyOrCompleted = entity.Status is PreMissionAssessmentStatus.Ready or PreMissionAssessmentStatus.Completed;

        var siteCheck = new AssessmentCheckResultDto(
            (isReadyOrCompleted || entity.SiteFeasibilityStatus == ReadinessCheckStatus.Passed) ? "PASS" : "FAIL",
            (isReadyOrCompleted || entity.SiteFeasibilityStatus == ReadinessCheckStatus.Passed) ? null : "Mặt bằng hành lang chưa đạt điều kiện an toàn.",
            entity.UpdatedAt ?? entity.CreatedAt
        );

        var uavCheck = new AssessmentCheckResultDto(
            (isReadyOrCompleted || entity.DroneCandidates.Any(d => d.IsEligible)) ? "PASS" : "FAIL",
            (isReadyOrCompleted || entity.DroneCandidates.Any(d => d.IsEligible)) ? null : "Chưa có UAV nào đạt hạn kiểm định hoặc khả dụng trong khung giờ này.",
            entity.UpdatedAt ?? entity.CreatedAt
        );

        var technicalCheck = new AssessmentCheckResultDto(
            (isReadyOrCompleted || ((entity.OverallTechnicalHealth is TechnicalHealth.Healthy or TechnicalHealth.Warning) && entity.DroneCandidates.Any(d => d.IsEligible))) ? "PASS" : "FAIL",
            (isReadyOrCompleted || ((entity.OverallTechnicalHealth is TechnicalHealth.Healthy or TechnicalHealth.Warning) && entity.DroneCandidates.Any(d => d.IsEligible))) ? null : "Chưa có UAV nào ở trạng thái Ready; kiểm tra kỹ thuật được thực hiện tại Chi tiết Drone.",
            entity.UpdatedAt ?? entity.CreatedAt
        );

        var headcount = AssessmentFindings.ReadHeadcount(entity.Findings);
        var reqInsp = headcount.Inspectors;
        var reqAna = headcount.Analysts;
        var reqTech = headcount.Technicians;

        var hasReqPersonnel = isReadyOrCompleted || (
            entity.PersonnelCandidates.Count(p => p.IsEligible && string.Equals(p.Role, "Inspector", StringComparison.OrdinalIgnoreCase)) >= reqInsp &&
            entity.PersonnelCandidates.Count(p => p.IsEligible && string.Equals(p.Role, "Analyst", StringComparison.OrdinalIgnoreCase)) >= reqAna &&
            entity.PersonnelCandidates.Count(p => p.IsEligible && string.Equals(p.Role, "Technician", StringComparison.OrdinalIgnoreCase)) >= reqTech
        );

        var personnelCheck = new AssessmentCheckResultDto(
            hasReqPersonnel ? "PASS" : "FAIL",
            hasReqPersonnel ? null : "Chưa đạt định mức nhân sự đang rảnh.",
            entity.UpdatedAt ?? entity.CreatedAt
        );

        return new PreMissionAssessmentDto(
            entity.Id,
            $"PMA-{entity.Id.ToString()[..8].ToUpperInvariant()}",
            entity.ManagerId,
            entity.RegionId,
            entity.Region?.Name ?? entity.RegionId.ToString(),
            entity.PlannedStart,
            entity.PlannedEnd,
            statusStr,
            entity.OverallTechnicalHealth.ToString(),
            entity.ValidUntil,
            entity.Version,
            entity.Mission?.Id,
            entity.Findings,
            entity.SiteFeasibilityStatus.ToString(),
            entity.EvaluationPolicyVersion,
            entity.IdempotencyKey,
            towers,
            personnel,
            drones,
            drones, // uavCandidates for backward compatibility
            siteCheck,
            uavCheck,
            technicalCheck,
            personnelCheck,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.Manager?.FullName ?? entity.Manager?.Email,
            reqInsp,
            reqAna,
            reqTech
        );
    }

    /// <summary>A metric stores one value column; a note sent with a numeric/bool reading is kept in Metadata.note.</summary>
    private static string? MetadataNote(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata) || !metadata.Contains("\"note\""))
            return null;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(metadata);
            return doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("note", out var note) &&
                   note.ValueKind == System.Text.Json.JsonValueKind.String
                ? note.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    public static DroneTechnicalInspectionDto ToDto(this DroneTechnicalInspection entity)
    {
        var metrics = entity.Metrics
            .Select(m => new DroneTechnicalMetricDto(
                m.Id,
                m.MetricCode,
                m.Subsystem,
                m.NumericValue,
                m.BoolValue,
                m.ValueText ?? MetadataNote(m.Metadata),
                m.Unit,
                m.Passed,
                m.Severity == "Critical",
                m.Severity,
                m.IsRequired
            ))
            .ToList();

        return new DroneTechnicalInspectionDto(
            entity.Id,
            entity.DroneId,
            entity.TechnicianId,
            entity.Technician?.FullName,
            entity.Status.ToString(),
            entity.Health.ToString(),
            entity.PolicyVersion,
            entity.Notes,
            entity.SourceType,
            entity.SourceVersion,
            entity.StartedAt,
            entity.CompletedAt,
            entity.ValidUntil,
            metrics,
            DroneReadiness.FromHealth(entity.Health),
            DroneInspectionSnapshot.ReadMissingMetrics(entity.RawSnapshot)
        );
    }
}
