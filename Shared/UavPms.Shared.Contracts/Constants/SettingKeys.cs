namespace UavPms.Shared.Contracts.Constants;

public static class SettingKeys
{
    // OTP
    public const string OtpExpiryMinutes = "Otp:ExpiryMinutes";
    public const string OtpCooldownSeconds = "Otp:CooldownSeconds";
    public const string OtpMaxAttempts = "Otp:MaxAttempts";
    public const string OtpBypassCode = "Otp:BypassCode";

    // Authentication & Tokens
    public const string AuthAccessTokenExpiryMinutes = "Auth:AccessTokenExpiryMinutes";
    public const string AuthRefreshTokenExpiryDays = "Auth:RefreshTokenExpiryDays";
    public const string AuthDeviceTrustDays = "Auth:DeviceTrustDays";
    public const string AuthStepUpTokenExpiryMinutes = "Auth:StepUpTokenExpiryMinutes";
    public const string AuthVerificationTokenExpiryMinutes = "Auth:VerificationTokenExpiryMinutes";

    // AI & Evaluation
    public const string AiEmergencyConfidenceThreshold = "Ai:EmergencyConfidenceThreshold";
    public const string AiCriticalScoreThreshold = "Ai:CriticalScoreThreshold";
    public const string AiHighScoreThreshold = "Ai:HighScoreThreshold";
    public const string AiMediumScoreThreshold = "Ai:MediumScoreThreshold";
    public const string AiEmergencyWeight = "Ai:EmergencyWeight";
    public const string AiMaxConfidenceWeight = "Ai:MaxConfidenceWeight";
    public const string AiDefaultPreferredModel = "Ai:DefaultPreferredModel";
    public const string AiTimeoutSeconds = "Ai:TimeoutSeconds";

    // Drone & Assessments
    public const string DroneCriticalBatteryThreshold = "Drone:CriticalBatteryThreshold";
    public const string DroneWarningBatteryThreshold = "Drone:WarningBatteryThreshold";
    public const string DroneMinRfLinkThreshold = "Drone:MinRfLinkThreshold";
    public const string DroneInspectionValidityDays = "Drone:InspectionValidityDays";

    // Missions (MF02/MF03)
    /// <summary>Default confirmation deadline after dispatch, in hours (BR-31); capped at the planned start.</summary>
    public const string MissionConfirmationDeadlineHours = "Mission:ConfirmationDeadlineHours";
    /// <summary>How long before the planned start a mission may be started, in minutes (BR-38).</summary>
    public const string MissionStartToleranceMinutes = "Mission:StartToleranceMinutes";
}
