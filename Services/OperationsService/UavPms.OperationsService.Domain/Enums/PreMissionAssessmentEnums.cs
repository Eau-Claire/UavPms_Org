namespace UavPms.OperationsService.Domain.Enums;

public enum PreMissionAssessmentStatus
{
    Draft,
    Evaluating,
    Ready,
    NotReady,
    Expired,
    Completed,
    Cancelled,

    [Obsolete("Use Completed instead.")]
    Consumed = Completed,
    [Obsolete("Use NotReady instead.")]
    Incomplete = NotReady
}
public enum DroneOperationalStatus { Available, Reserved, InUse, Maintenance, Unavailable, Lost }
public enum TechnicalHealth { Unknown, Healthy, Warning, Critical }
public enum DroneTechnicalInspectionStatus { Pending, InProgress, Passed, Failed, Incomplete }
public enum MissionAssignmentResponse { Pending, Accepted, Postponed, Replaced, Cancelled }
public enum ReadinessCheckStatus { Pending, Passed, Failed, Incomplete, Warning }
public enum ResourceAvailabilityStatus { Unknown, Available, Conflict, Unavailable }
public enum ResourceEligibilityStatus { Unknown, Eligible, Ineligible, RequiresInspection }
public enum ResourceBookingStatus { Active, Released, Cancelled }
