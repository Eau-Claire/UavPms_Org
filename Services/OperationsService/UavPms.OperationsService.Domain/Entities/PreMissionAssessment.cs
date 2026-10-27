using System.ComponentModel.DataAnnotations.Schema;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public class PreMissionAssessment : BaseEntity
{
    public Guid ManagerId { get; set; }
    public Guid RegionId { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public Geometry? ProposedBoundary { get; set; }
    public ReadinessCheckStatus SiteFeasibilityStatus { get; set; } = ReadinessCheckStatus.Pending;

    public PreMissionAssessmentStatus Status { get; set; } = PreMissionAssessmentStatus.Draft;
    public TechnicalHealth OverallTechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public DateTime? ValidUntil { get; set; }

    public uint Version { get; set; } = 1;
    public string Findings { get; set; } = "{}";
    public string? EvaluationPolicyVersion { get; set; }
    public string? IdempotencyKey { get; set; }
    public virtual Region? Region { get; set; }
    public virtual User? Manager { get; set; }

    /// <summary>Nhiệm vụ được tạo từ đánh giá này (Missions.PreMissionAssessmentId).</summary>
    public virtual Mission? Mission { get; set; }
    public ICollection<PreMissionAssessmentTower> Towers { get; set; } = new List<PreMissionAssessmentTower>();
    public ICollection<PreMissionAssessmentPersonnel> PersonnelCandidates { get; set; } = new List<PreMissionAssessmentPersonnel>();
    public ICollection<PreMissionAssessmentDrone> DroneCandidates { get; set; } = new List<PreMissionAssessmentDrone>();
}

public sealed class PreMissionAssessmentTower : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid TowerId { get; set; }
    public int Sequence { get; set; }
    public PreMissionAssessment? Assessment { get; set; }
    public Tower? Tower { get; set; }
}

public sealed class PreMissionAssessmentPersonnel : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public ResourceEligibilityStatus EligibilityStatus { get; set; } = ResourceEligibilityStatus.Unknown;
    public ResourceAvailabilityStatus AvailabilityStatus { get; set; } = ResourceAvailabilityStatus.Unknown;
    public string? ReasonCode { get; set; }
    public DateTime SnapshotAt { get; set; }
    public string Findings { get; set; } = "{}";
    public PreMissionAssessment? Assessment { get; set; }
    public User? User { get; set; }

    /// <summary>Suy ra từ trạng thái: đủ điều kiện và đang rảnh.</summary>
    [NotMapped]
    public bool IsEligible => EligibilityStatus == ResourceEligibilityStatus.Eligible
        && AvailabilityStatus == ResourceAvailabilityStatus.Available;

    [NotMapped]
    public string Role
    {
        get
        {
            var opRole = User?.UserRoles?
                .Select(ur => ur.Role?.RoleName)
                .FirstOrDefault(name => name != null && (
                    name.Equals("Inspector", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Pilot", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Analyst", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Technician", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("MaintenanceTechnician", StringComparison.OrdinalIgnoreCase)
                ));

            if (!string.IsNullOrEmpty(opRole))
            {
                if (opRole.Equals("Inspector", StringComparison.OrdinalIgnoreCase) || opRole.Equals("Pilot", StringComparison.OrdinalIgnoreCase))
                    return "Inspector";
                if (opRole.Equals("Analyst", StringComparison.OrdinalIgnoreCase))
                    return "Analyst";
                if (opRole.Equals("Technician", StringComparison.OrdinalIgnoreCase) || opRole.Equals("MaintenanceTechnician", StringComparison.OrdinalIgnoreCase))
                    return "Technician";
                return opRole;
            }

            try
            {
                if (!string.IsNullOrEmpty(Findings) && Findings.Contains("\"role\""))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(Findings);
                    if (doc.RootElement.TryGetProperty("role", out var el))
                    {
                        var found = el.GetString();
                        if (!string.IsNullOrEmpty(found) &&
                            !found.Equals("SystemAdmin", StringComparison.OrdinalIgnoreCase) &&
                            !found.Equals("Manager", StringComparison.OrdinalIgnoreCase))
                        {
                            return found;
                        }
                    }
                }
            }
            catch { }
            return "Inspector";
        }
    }
}

public sealed class PreMissionAssessmentDrone : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid DroneId { get; set; }
    public ResourceAvailabilityStatus OperationalAvailabilityStatus { get; set; } = ResourceAvailabilityStatus.Unknown;
    public ResourceEligibilityStatus TechnicalEligibilityStatus { get; set; } = ResourceEligibilityStatus.Unknown;
    public UavScopeSuitability Suitability { get; set; } = UavScopeSuitability.Suitable;
    public int EstimatedFlightsNeeded { get; set; } = 1;
    public double CoverageDistanceKm { get; set; }
    public string? SuitabilityReason { get; set; }
    public string? ReasonCode { get; set; }
    public DateTime SnapshotAt { get; set; }
    public TechnicalHealth TechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public Guid? TechnicalInspectionId { get; set; }
    public PreMissionAssessment? Assessment { get; set; }
    public Uav? Drone { get; set; }
    public DroneTechnicalInspection? TechnicalInspection { get; set; }

    /// <summary>Suy ra từ trạng thái: sẵn sàng vận hành, đạt kỹ thuật và phù hợp phạm vi.</summary>
    [NotMapped]
    public bool IsEligible => OperationalAvailabilityStatus == ResourceAvailabilityStatus.Available
        && TechnicalEligibilityStatus == ResourceEligibilityStatus.Eligible
        && Suitability != UavScopeSuitability.Unsuitable;
}

public sealed class DroneTechnicalInspection : BaseEntity
{
    public Guid DroneId { get; set; }
    public Guid? TechnicianId { get; set; }
    public DroneTechnicalInspectionStatus Status { get; set; } = DroneTechnicalInspectionStatus.Pending;
    public TechnicalHealth Health { get; set; } = TechnicalHealth.Unknown;
    public string SourceType { get; set; } = "manual";
    public string SourceVersion { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public string RawSnapshot { get; set; } = "{}";
    public DateTime? CompletedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? PolicyVersion { get; set; }
    public string? Notes { get; set; }
    public uint Version { get; set; } = 1;
    public Uav? Drone { get; set; }
    public User? Technician { get; set; }
    public ICollection<DroneTechnicalMetric> Metrics { get; set; } = new List<DroneTechnicalMetric>();
}

public sealed class DroneTechnicalMetric : BaseEntity
{
    public Guid InspectionId { get; set; }
    public string MetricCode { get; set; } = string.Empty;
    public string Subsystem { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public bool Passed { get; set; }
    public string? ValueText { get; set; }
    public bool? BoolValue { get; set; }
    public string? Unit { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public string? Metadata { get; set; }
    public DroneTechnicalInspection? Inspection { get; set; }
}
