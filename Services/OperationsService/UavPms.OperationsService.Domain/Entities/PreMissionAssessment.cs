using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Common;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Domain.Entities;

public sealed class PreMissionAssessment : BaseEntity
{
    public Guid ManagerId { get; set; }
    public Guid RegionId { get; set; }
    public DateTime PlannedStart { get; set; }
    public DateTime PlannedEnd { get; set; }
    public Geometry? ProposedBoundary { get; set; }
    public PreMissionAssessmentStatus Status { get; set; } = PreMissionAssessmentStatus.Draft;
    public TechnicalHealth OverallTechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public DateTime? ValidUntil { get; set; }
    public Guid? ConsumedByMissionId { get; set; }
    public uint Version { get; set; } = 1;
    public string Findings { get; set; } = "{}";
    public virtual Region? Region { get; set; }
    public virtual User? Manager { get; set; }
    public ICollection<PreMissionAssessmentAsset> Assets { get; set; } = new List<PreMissionAssessmentAsset>();
    public ICollection<PreMissionAssessmentPersonnel> PersonnelCandidates { get; set; } = new List<PreMissionAssessmentPersonnel>();
    public ICollection<PreMissionAssessmentDrone> DroneCandidates { get; set; } = new List<PreMissionAssessmentDrone>();
}

public sealed class PreMissionAssessmentAsset : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid AssetId { get; set; }
    public int Sequence { get; set; }
    public PreMissionAssessment? Assessment { get; set; }
    public Asset? Asset { get; set; }
}

public sealed class PreMissionAssessmentPersonnel : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid UserId { get; set; }
    public bool IsEligible { get; set; }
    public string Findings { get; set; } = "{}";
    public PreMissionAssessment? Assessment { get; set; }
    public User? User { get; set; }
}

public sealed class PreMissionAssessmentDrone : BaseEntity
{
    public Guid AssessmentId { get; set; }
    public Guid DroneId { get; set; }
    public bool IsEligible { get; set; }
    public TechnicalHealth TechnicalHealth { get; set; } = TechnicalHealth.Unknown;
    public Guid? TechnicalInspectionId { get; set; }
    public PreMissionAssessment? Assessment { get; set; }
    public Uav? Drone { get; set; }
    public DroneTechnicalInspection? TechnicalInspection { get; set; }
}

public sealed class DroneTechnicalInspection : BaseEntity
{
    public Guid DroneId { get; set; }
    public DroneTechnicalInspectionStatus Status { get; set; } = DroneTechnicalInspectionStatus.Pending;
    public TechnicalHealth Health { get; set; } = TechnicalHealth.Unknown;
    public string SourceType { get; set; } = "manual";
    public string SourceVersion { get; set; } = string.Empty;
    public string RawSnapshot { get; set; } = "{}";
    public DateTime? CompletedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public Uav? Drone { get; set; }
    public ICollection<DroneTechnicalMetric> Metrics { get; set; } = new List<DroneTechnicalMetric>();
}

public sealed class DroneTechnicalMetric : BaseEntity
{
    public Guid InspectionId { get; set; }
    public string MetricCode { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }
    public bool Passed { get; set; }
    public bool Critical { get; set; }
    public string? ValueText { get; set; }
    public DroneTechnicalInspection? Inspection { get; set; }
}
