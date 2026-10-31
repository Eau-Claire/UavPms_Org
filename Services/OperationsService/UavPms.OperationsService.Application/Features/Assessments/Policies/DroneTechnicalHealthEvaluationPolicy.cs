using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Assessments.Policies;

public record EvaluatedMetricResult(
    string MetricCode,
    string Subsystem,
    decimal? NumericValue,
    bool? BoolValue,
    string? ValueText,
    string? Unit,
    bool Passed,
    bool Critical,
    string Severity,
    bool IsRequired
);

public record DroneTechnicalEvaluationResult(
    DroneTechnicalInspectionStatus Status,
    TechnicalHealth Health,
    IReadOnlyList<EvaluatedMetricResult> Metrics,
    bool AnyCriticalFailed,
    bool AnyWarning
);

/// <summary>
/// BR-22 / BR-23: an inspection is Failed when a critical item fails. Health is Critical when a critical item fails,
/// Warning when a non-critical item fails or a battery reading is in the warning band, otherwise Healthy.
/// A critical or required item without any reading (no Passed, BoolValue or NumericValue) fails.
/// </summary>
public static class DroneTechnicalHealthEvaluationPolicy
{
    private static readonly HashSet<string> KnownCriticalCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BATTERY_HEALTH",
        "BATTERY",
        "IMU_CALIBRATION",
        "IMU",
        "GPS_RTK_FIX",
        "GPS_FIX",
        "GPS",
        "ESC_PROPULSION",
        "PROPULSION",
        "MOTORS",
        "PROP_INTEGRITY",
        "FRAME_BODY",
        "FLIGHT_CONTROLLER",
        "RF_LINK_TELEMETRY",
        "TELEMETRY"
    };

    private static readonly HashSet<string> BatteryCodes = new(StringComparer.OrdinalIgnoreCase) { "BATTERY_HEALTH", "BATTERY" };
    private static readonly HashSet<string> RfLinkCodes = new(StringComparer.OrdinalIgnoreCase) { "RF_LINK_TELEMETRY", "TELEMETRY" };

    public static bool IsKnownCritical(string metricCode) => KnownCriticalCodes.Contains(metricCode);

    public static DroneTechnicalEvaluationResult Evaluate(
        IReadOnlyList<DroneMetricSubmitDto>? requestedMetrics,
        double droneBatteryLevel)
        => Evaluate(requestedMetrics, droneBatteryLevel, new UavPms.Shared.Contracts.Settings.DroneThresholdSettingsDto());

    public static DroneTechnicalEvaluationResult Evaluate(
        IReadOnlyList<DroneMetricSubmitDto>? requestedMetrics,
        double droneBatteryLevel,
        UavPms.Shared.Contracts.Settings.DroneThresholdSettingsDto? settings)
    {
        settings ??= new UavPms.Shared.Contracts.Settings.DroneThresholdSettingsDto();
        var criticalBatteryThreshold = (decimal)settings.CriticalBatteryThreshold;
        var warningBatteryThreshold = (decimal)settings.WarningBatteryThreshold;
        var minRfLinkThreshold = (decimal)settings.MinRfLinkThreshold;

        if (requestedMetrics == null || requestedMetrics.Count == 0)
        {
            throw new BusinessRuleException(
                "METRICS_REQUIRED",
                "Technical inspection requires at least one diagnostic or telemetry metric.");
        }

        if (requestedMetrics.Any(m => m == null || string.IsNullOrWhiteSpace(m.MetricCode) || string.IsNullOrWhiteSpace(m.Subsystem)))
        {
            throw new BusinessRuleException(
                "METRICS_REQUIRED",
                "Every metric needs a metric code and a subsystem.");
        }

        // Check duplicates for (Subsystem, MetricCode)
        var duplicate = requestedMetrics
            .GroupBy(m => $"{m.Subsystem.Trim()}:{m.MetricCode.Trim()}", StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (duplicate != null)
        {
            throw new BusinessRuleException(
                "DUPLICATE_METRIC",
                $"Duplicate metric code '{duplicate.Key}' is not allowed in a single inspection.");
        }

        var evaluatedMetrics = new List<EvaluatedMetricResult>();
        var anyCriticalFailed = false;
        var anyFailed = false;
        var anyBatteryWarning = false;

        foreach (var m in requestedMetrics)
        {
            // A known critical item stays critical even when the client omits or clears the flag (BR-22).
            var critical = KnownCriticalCodes.Contains(m.MetricCode) || m.Critical;
            var mustHaveReading = critical || m.IsRequired;
            bool passed;
            string severity;

            if (BatteryCodes.Contains(m.MetricCode))
            {
                decimal? batteryVal = m.NumericValue ?? (droneBatteryLevel > 0 ? (decimal)droneBatteryLevel : null);
                if (!batteryVal.HasValue)
                {
                    // No level at all: only an explicit verdict counts; otherwise the item has no reading.
                    passed = m.Passed ?? m.BoolValue ?? !mustHaveReading;
                    severity = passed ? "Low" : "Critical";
                }
                else if (batteryVal.Value < criticalBatteryThreshold)
                {
                    passed = false;
                    severity = "Critical";
                }
                else if (batteryVal.Value < warningBatteryThreshold)
                {
                    // BR-23: a low battery passes with a warning.
                    passed = true;
                    anyBatteryWarning = true;
                    severity = "Medium";
                }
                else
                {
                    passed = true;
                    severity = "Low";
                }

                if (m.Passed == false || m.BoolValue == false)
                {
                    passed = false;
                    severity = "Critical";
                }
            }
            else if (RfLinkCodes.Contains(m.MetricCode))
            {
                if (m.NumericValue.HasValue && m.NumericValue.Value < minRfLinkThreshold)
                    passed = false;
                else if (m.Passed.HasValue)
                    passed = m.Passed.Value;
                else if (m.BoolValue.HasValue)
                    passed = m.BoolValue.Value;
                else if (m.NumericValue.HasValue)
                    passed = true;
                else
                    passed = !mustHaveReading;

                severity = passed ? "Low" : (critical ? "Critical" : "Medium");
            }
            else
            {
                if (m.Passed.HasValue)
                    passed = m.Passed.Value;
                else if (m.BoolValue.HasValue)
                    passed = m.BoolValue.Value;
                else if (m.NumericValue.HasValue)
                    passed = m.NumericValue.Value >= 0;
                else
                    passed = !mustHaveReading;

                severity = passed
                    ? "Low"
                    : (critical ? "Critical" : (string.IsNullOrWhiteSpace(m.Severity) || m.Severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ? "Medium" : m.Severity));
            }

            if (!passed)
            {
                anyFailed = true;
                if (critical)
                    anyCriticalFailed = true;
            }

            evaluatedMetrics.Add(new EvaluatedMetricResult(
                m.MetricCode,
                m.Subsystem,
                m.NumericValue,
                m.BoolValue,
                m.ValueText,
                m.Unit,
                passed,
                critical,
                severity,
                m.IsRequired
            ));
        }

        var status = anyCriticalFailed
            ? DroneTechnicalInspectionStatus.Failed
            : DroneTechnicalInspectionStatus.Passed;

        var anyWarning = !anyCriticalFailed && (anyFailed || anyBatteryWarning);
        var health = anyCriticalFailed
            ? TechnicalHealth.Critical
            : (anyWarning ? TechnicalHealth.Warning : TechnicalHealth.Healthy);

        return new DroneTechnicalEvaluationResult(
            status,
            health,
            evaluatedMetrics,
            anyCriticalFailed,
            anyWarning
        );
    }
}
