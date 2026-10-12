using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments;

public static class AssessmentMappingExtensions
{
    public static PreMissionAssessmentDto ToDto(this PreMissionAssessment entity)
    {
        var statusStr = entity.Status switch
        {
            PreMissionAssessmentStatus.Draft => "DRAFT",
            PreMissionAssessmentStatus.Evaluating => "EVALUATING",
            PreMissionAssessmentStatus.Ready => "READY",
            PreMissionAssessmentStatus.NotReady => "NOT_READY",
            PreMissionAssessmentStatus.Expired => "EXPIRED",
            PreMissionAssessmentStatus.Completed => "COMPLETED",
            PreMissionAssessmentStatus.Cancelled => "CANCELLED",
            _ => entity.Status.ToString().ToUpperInvariant()
        };

        var assets = entity.Assets
            .OrderBy(a => a.Sequence)
            .Select(a => new PreMissionAssessmentAssetDto(
                a.Id,
                a.AssetId,
                a.Sequence,
                a.Asset?.AssetCode,
                a.Asset?.AssetType,
                a.Asset?.Status
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
                d.Drone?.BatteryLevel ?? 100
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
            (isReadyOrCompleted || ((entity.OverallTechnicalHealth is TechnicalHealth.Healthy or TechnicalHealth.Warning) && entity.DroneCandidates.Any(d => d.IsEligible))) ? null : "Cần thực hiện kiểm định kỹ thuật tự động (BIST/Telemetry) trước khi bay.",
            entity.UpdatedAt ?? entity.CreatedAt
        );

        var personnelCheck = new AssessmentCheckResultDto(
            (isReadyOrCompleted || (
                entity.PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Inspector") &&
                entity.PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Analyst") &&
                entity.PersonnelCandidates.Any(p => p.IsEligible && (p.Role == "Technician" || p.Role == "MaintenanceTechnician"))
            )) ? "PASS" : "FAIL",
            (isReadyOrCompleted || (
                entity.PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Inspector") &&
                entity.PersonnelCandidates.Any(p => p.IsEligible && p.Role == "Analyst") &&
                entity.PersonnelCandidates.Any(p => p.IsEligible && (p.Role == "Technician" || p.Role == "MaintenanceTechnician"))
            )) ? null : "Chưa đạt định mức nhân sự đang rảnh.",
            entity.UpdatedAt ?? entity.CreatedAt
        );

        return new PreMissionAssessmentDto(
            entity.Id,
            $"PMA-{entity.Id.ToString()[..8].ToUpperInvariant()}",
            entity.ManagerId,
            entity.RegionId,
            entity.Region?.RegionName ?? entity.RegionId.ToString(),
            entity.PlannedStart,
            entity.PlannedEnd,
            statusStr,
            entity.OverallTechnicalHealth.ToString(),
            entity.ValidUntil,
            entity.Version,
            entity.ConsumedByMissionId,
            entity.Findings,
            entity.SiteFeasibilityStatus.ToString(),
            entity.EvaluationPolicyVersion,
            entity.IdempotencyKey,
            assets,
            personnel,
            drones,
            drones, // uavCandidates for backward compatibility
            siteCheck,
            uavCheck,
            technicalCheck,
            personnelCheck,
            entity.CreatedAt,
            entity.UpdatedAt
        );
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
                m.ValueText,
                m.Unit,
                m.Passed,
                m.Critical,
                m.Severity,
                m.IsRequired
            ))
            .ToList();

        return new DroneTechnicalInspectionDto(
            entity.Id,
            entity.DroneId,
            entity.TechnicianUserId,
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
            metrics
        );
    }
}
