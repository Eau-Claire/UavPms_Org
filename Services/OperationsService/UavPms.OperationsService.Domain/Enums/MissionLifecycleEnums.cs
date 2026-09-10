namespace UavPms.OperationsService.Domain.Enums;

public enum MissionType { Scheduled = 0, AdHoc = 1 }
public enum MissionAssignmentStatus { Active = 0, Unavailable = 1, Revoked = 2 }
public enum MissionCheckInStatus { CheckedIn = 0, Revoked = 1 }
public enum DroneHandoverStatus { Accepted = 0, Rejected = 1, Returned = 2 }
