namespace UavPms.NotificationService.Domain.Enums;

public enum NotificationType
{
    General = 0,
    MissionAssigned = 1,
    DefectDetected = 2,
    EmergencyAlert = 3,
    TicketAssigned = 4,
    EscalationRequest = 5,
}