using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Domain.Entities;
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

        // Check duplicates for (Subsystem, MetricCode)
        var duplicate = requestedMetrics
            .GroupBy(m => $"{m.Subsystem}:{m.MetricCode}", StringComparer.OrdinalIgnoreCase)
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

        foreach (var m in requestedMetrics)
        {
            var isKnownCritical = KnownCriticalCodes.Contains(m.MetricCode) || m.Critical;
            var passed = true;
            var critical = isKnownCritical;
            var severity = m.Severity ?? "Medium";

            if (m.MetricCode.Equals("BATTERY_HEALTH", StringComparison.OrdinalIgnoreCase) ||
                m.MetricCode.Equals("BATTERY", StringComparison.OrdinalIgnoreCase))
            {
                var batteryVal = m.NumericValue ?? (decimal)droneBatteryLevel;
                if (batteryVal < criticalBatteryThreshold)
                {
                    passed = false;
                    critical = true;
                    severity = "Critical";
                }
                else if (batteryVal < warningBatteryThreshold)
                {
                    passed = true;
                    critical = false;
                    severity = "Medium";
                }
                else
                {
                    passed = true;
                    critical = false;
                    severity = "Low";
                }

                if (m.Passed.HasValue && !m.Passed.Value)
                {
                    passed = false;
                    critical = true;
                }
            }
            else if (m.MetricCode.Equals("IMU_CALIBRATION", StringComparison.OrdinalIgnoreCase) ||
                     m.MetricCode.Equals("IMU", StringComparison.OrdinalIgnoreCase) ||
                     m.MetricCode.Equals("GPS_RTK_FIX", StringComparison.OrdinalIgnoreCase) ||
                     m.MetricCode.Equals("GPS_FIX", StringComparison.OrdinalIgnoreCase) ||
                     m.MetricCode.Equals("ESC_PROPULSION", StringComparison.OrdinalIgnoreCase) ||
                     m.MetricCode.Equals("PROP_INTEGRITY", StringComparison.OrdinalIgnoreCase))
            {
                critical = true;
                if (m.Passed.HasValue)
                {
                    passed = m.Passed.Value;
                }
                else if (m.BoolValue.HasValue)
                {
                    passed = m.BoolValue.Value;
                }
                else
                {
                    passed = true;
                }

                if (!passed)
                {
                    severity = "Critical";
                }
            }
            else if (m.MetricCode.Equals("RF_LINK_TELEMETRY", StringComparison.OrdinalIgnoreCase) ||
                     m.MetricCode.Equals("TELEMETRY", StringComparison.OrdinalIgnoreCase))
            {
                critical = true;
                if (m.NumericValue.HasValue && m.NumericValue.Value < minRfLinkThreshold)
                {
                    passed = false;
                    severity = "Critical";
                }
                else if (m.Passed.HasValue)
                {
                    passed = m.Passed.Value;
                }
                else if (m.BoolValue.HasValue)
                {
                    passed = m.BoolValue.Value;
                }

                if (!passed)
                {
                    severity = "Critical";
                }
            }
            else
            {
                // General or custom metric
                if (m.Passed.HasValue)
                {
                    passed = m.Passed.Value;
                }
                else if (m.BoolValue.HasValue)
                {
                    passed = m.BoolValue.Value;
                }
                else if (m.NumericValue.HasValue)
                {
                    passed = m.NumericValue.Value >= 0;
                }

                critical = m.Critical;
            }

            if (!passed)
            {
                anyFailed = true;
                if (critical)
                {
                    anyCriticalFailed = true;
                }
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

        var health = anyCriticalFailed
            ? TechnicalHealth.Critical
            : (anyFailed ? TechnicalHealth.Warning : TechnicalHealth.Healthy);

        return new DroneTechnicalEvaluationResult(
            status,
            health,
            evaluatedMetrics,
            anyCriticalFailed,
            anyFailed && !anyCriticalFailed
        );
    }
}
