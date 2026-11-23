namespace UavPms.Shared.Contracts.Enums;

/// <summary>
/// Trạng thái chuẩn của EmergencyAlert theo State Machine Diagram.
/// </summary>
public enum EmergencyAlertStatus
{
    Open = 0,
    Acknowledged = 1,
    Resolved = 2
}
