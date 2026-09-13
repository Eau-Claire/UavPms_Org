namespace UavPms.OperationsService.Domain.Enums;

public enum PreMissionAssessmentStatus { Draft, Evaluating, Ready, NotReady, Incomplete, Consumed, Expired, Cancelled }
public enum DroneOperationalStatus { Available, Reserved, InUse, Maintenance, Unavailable, Lost }
public enum TechnicalHealth { Unknown, Healthy, Warning, Critical }
public enum DroneTechnicalInspectionStatus { Pending, InProgress, Passed, Failed, Incomplete }
public enum MissionAssignmentResponse { Pending, Accepted, Postponed, Replaced, Cancelled }
