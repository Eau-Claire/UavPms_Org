using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public static class MqttDroneMessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };

    /// <summary>
    /// Extracts the drone code from topic <c>drone/{droneCode}/telemetry</c> (or legacy <c>uav/{droneCode}/{suffix}</c>).
    /// </summary>
    public static bool TryGetTopicDroneCode(string topic, out string droneCode)
    {
        return TryGetTopicDroneCode(topic, "telemetry", out droneCode);
    }

    public static bool TryGetTopicDroneCode(string topic, string suffix, out string droneCode)
    {
        droneCode = string.Empty;
        if (string.IsNullOrWhiteSpace(topic))
            return false;

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3
            || (!string.Equals(segments[0], "drone", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(segments[0], "uav", StringComparison.OrdinalIgnoreCase))
            || !string.Equals(segments[2], suffix, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[1]))
        {
            return false;
        }

        droneCode = segments[1].Trim();
        return droneCode.Length > 0;
    }

    /// <summary>
    /// Deserializes and validates the 22-field snake_case telemetry payload (TR-03):
    /// - topic segment equals drone_id;
    /// - timestamp is valid ISO 8601 (converted to UTC);
    /// - lat in [-90, 90] and lon in [-180, 180], or both null;
    /// - all numeric values are finite.
    /// </summary>
    public static bool TryParseAndValidateTelemetry(
        string topic,
        string payload,
        out MqttDroneTelemetryPayload? telemetry,
        out DateTime sourceTimestampUtc,
        out string? validationError)
    {
        telemetry = null;
        sourceTimestampUtc = default;
        validationError = null;

        if (!TryGetTopicDroneCode(topic, "telemetry", out var topicDroneCode))
        {
            validationError = $"Invalid telemetry topic '{topic}'. Expected 'drone/{{droneCode}}/telemetry'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            validationError = "Payload is empty.";
            return false;
        }

        MqttDroneTelemetryPayload? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<MqttDroneTelemetryPayload>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            validationError = $"Malformed JSON or non-finite numeric value: {ex.Message}";
            return false;
        }

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.DroneId))
        {
            validationError = "Missing required 'drone_id' field.";
            return false;
        }

        if (!string.Equals(topicDroneCode, parsed.DroneId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            validationError = $"Topic drone segment '{topicDroneCode}' does not match payload drone_id '{parsed.DroneId}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(parsed.Timestamp) ||
            !DateTimeOffset.TryParse(
                parsed.Timestamp,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedDto))
        {
            validationError = $"Invalid ISO 8601 timestamp '{parsed.Timestamp}'.";
            return false;
        }

        sourceTimestampUtc = parsedDto.UtcDateTime;

        if (parsed.Lat.HasValue != parsed.Lon.HasValue)
        {
            validationError = "Latitude and longitude must both be present or both be null.";
            return false;
        }

        if (parsed.Lat.HasValue && parsed.Lon.HasValue)
        {
            var lat = parsed.Lat.Value;
            var lon = parsed.Lon.Value;
            if (!double.IsFinite(lat) || !double.IsFinite(lon) || lat < -90.0 || lat > 90.0 || lon < -180.0 || lon > 180.0)
            {
                validationError = $"Coordinates out of valid range: lat={lat}, lon={lon}.";
                return false;
            }
        }

        if (!AreAllNumbersFinite(parsed))
        {
            validationError = "Payload contains non-finite numeric values (NaN or Infinity).";
            return false;
        }

        telemetry = parsed with { DroneId = parsed.DroneId.Trim() };
        return true;
    }

    private static bool AreAllNumbersFinite(MqttDroneTelemetryPayload p)
    {
        return IsFiniteOrNull(p.AltRelativeM)
            && IsFiniteOrNull(p.AltMslM)
            && IsFiniteOrNull(p.Hdop)
            && IsFiniteOrNull(p.RollDeg)
            && IsFiniteOrNull(p.PitchDeg)
            && IsFiniteOrNull(p.YawDeg)
            && IsFiniteOrNull(p.GroundSpeedMs)
            && IsFiniteOrNull(p.AirSpeedMs)
            && IsFiniteOrNull(p.ClimbRateMs)
            && IsFiniteOrNull(p.HeadingDeg)
            && IsFiniteOrNull(p.ThrottlePct)
            && IsFiniteOrNull(p.BatteryVoltageV)
            && IsFiniteOrNull(p.BatteryCurrentA);
    }

    private static bool IsFiniteOrNull(double? value) => !value.HasValue || double.IsFinite(value.Value);
}

public sealed record MqttDroneTelemetryPayload
{
    [JsonPropertyName("drone_id")]
    public string? DroneId { get; init; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; init; }

    [JsonPropertyName("flight_mode")]
    public string? FlightMode { get; init; }

    [JsonPropertyName("armed")]
    public bool? Armed { get; init; }

    [JsonPropertyName("lat")]
    public double? Lat { get; init; }

    [JsonPropertyName("lon")]
    public double? Lon { get; init; }

    [JsonPropertyName("alt_relative_m")]
    public double? AltRelativeM { get; init; }

    [JsonPropertyName("alt_msl_m")]
    public double? AltMslM { get; init; }

    [JsonPropertyName("satellites")]
    public int? Satellites { get; init; }

    [JsonPropertyName("gps_fix_type")]
    public int? GpsFixType { get; init; }

    [JsonPropertyName("hdop")]
    public double? Hdop { get; init; }

    [JsonPropertyName("roll_deg")]
    public double? RollDeg { get; init; }

    [JsonPropertyName("pitch_deg")]
    public double? PitchDeg { get; init; }

    [JsonPropertyName("yaw_deg")]
    public double? YawDeg { get; init; }

    [JsonPropertyName("ground_speed_ms")]
    public double? GroundSpeedMs { get; init; }

    [JsonPropertyName("air_speed_ms")]
    public double? AirSpeedMs { get; init; }

    [JsonPropertyName("climb_rate_ms")]
    public double? ClimbRateMs { get; init; }

    [JsonPropertyName("heading_deg")]
    public double? HeadingDeg { get; init; }

    [JsonPropertyName("throttle_pct")]
    public double? ThrottlePct { get; init; }

    [JsonPropertyName("battery_voltage_v")]
    public double? BatteryVoltageV { get; init; }

    [JsonPropertyName("battery_current_a")]
    public double? BatteryCurrentA { get; init; }

    [JsonPropertyName("battery_remaining_pct")]
    public int? BatteryRemainingPct { get; init; }
}
