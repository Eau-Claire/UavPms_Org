namespace UavPms.Shared.Contracts.Events;

public static class EventTopologyConstants
{
    public const string Exchange = "uavpms.events";
    public const string LegacyExchange = "identity-exchange";

    public static class RoutingKeys
    {
        public const string InspectionMediaUploaded = "inspection.media.uploaded";
        public const string AiDetectionCompleted = "ai.detection.completed";
        public const string MissionDispatched = "mission.dispatched";
        public const string MaintenanceTicketAssigned = "maintenance.ticket.assigned";

        // Legacy routing keys
        public const string LegacyInspectionMediaUploaded = "identity.event.inspectionmediauploadedevent";
        public const string LegacyDefectDetected = "identity.event.defectdetectedevent";
        public const string LegacyMissionCreated = "identity.event.missioncreatedevent";
        public const string LegacyMissionCreatedFromAssessment = "identity.event.missioncreatedfromassessmentevent";
    }

    public static class Queues
    {
        public const string AiInspectionMediaProcess = "ai-inspection.media-process";
        public const string OperationsDefectRecord = "operations.defect-record";
        public const string NotificationsDefectAlert = "notifications.defect-alert";
        public const string NotificationsMissionDispatch = "notifications.mission-dispatch";
        public const string NotificationsTicketAssign = "notifications.ticket-assign";

        // Legacy queue names
        public const string LegacyAiInspectionMedia = "ai.inspection-media.uploaded";
        public const string LegacyNotificationDefect = "notification.defect-detected";
        public const string LegacyNotificationMissionCreated = "notification.mission-created";
    }
}
