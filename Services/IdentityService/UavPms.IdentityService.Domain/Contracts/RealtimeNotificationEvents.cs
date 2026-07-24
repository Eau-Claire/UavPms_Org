namespace UavPms.IdentityService.Domain.Contracts;

public static class RealtimeNotificationEvents
{
    public const string NotificationReceived = "NotificationReceived";
    public const string NotificationUpdated = "NotificationUpdated";
    public const string NotificationDeleted = "NotificationDeleted";
    public const string UnreadCountChanged = "UnreadCountChanged";
    public const string AiAnalysisStatusChanged = "AiAnalysisStatusChanged";
}
