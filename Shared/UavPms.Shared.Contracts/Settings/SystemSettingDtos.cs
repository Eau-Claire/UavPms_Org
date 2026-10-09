using System;

namespace UavPms.Shared.Contracts.Settings;

public record OtpSettingsDto(
    int ExpiryMinutes = 15,
    int CooldownSeconds = 30,
    int MaxAttempts = 5,
    string? BypassCode = "123456"
);

public record TokenSettingsDto(
    int AccessTokenExpiryMinutes = 60,
    int RefreshTokenExpiryDays = 7,
    int DeviceTrustDays = 30,
    int StepUpTokenExpiryMinutes = 5,
    int VerificationTokenExpiryMinutes = 10
);

public record AiEvaluationSettingsDto(
    double EmergencyConfidenceThreshold = 0.80,
    int CriticalScoreThreshold = 90,
    int HighScoreThreshold = 75,
    int MediumScoreThreshold = 50,
    int EmergencyWeight = 45,
    int MaxConfidenceWeight = 55,
    string DefaultPreferredModel = "SERVER",
    int TimeoutSeconds = 60
);

public record DroneThresholdSettingsDto(
    double CriticalBatteryThreshold = 30.0,
    double WarningBatteryThreshold = 50.0,
    double MinRfLinkThreshold = 50.0,
    int InspectionValidityDays = 7
);

public record SystemSettingDto(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    string? Category,
    string? DataType,
    DateTime? UpdatedAt
);

public record UpdateSystemSettingRequest(
    string Value
);
