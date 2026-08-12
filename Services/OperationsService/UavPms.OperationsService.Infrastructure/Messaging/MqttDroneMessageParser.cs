using System.Text.Json;

namespace UavPms.OperationsService.Infrastructure.Messaging;

public static class MqttDroneMessageParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryGetTopicDroneCode(string topic, string suffix, out string droneCode)
    {
        droneCode = string.Empty;
        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 3
            || !string.Equals(segments[0], "uav", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(segments[2], suffix, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(segments[1]))
        {
            return false;
        }

        droneCode = segments[1];
        return true;
    }

    public static bool TryParseStatus(string payload, out MqttDroneStatusPayload? status)
    {
        return TryParse(payload, out status);
    }

    public static bool TryParseTelemetry(string payload, out MqttDroneTelemetryPayload? telemetry)
    {
        return TryParse(payload, out telemetry);
    }

    private static bool TryParse<T>(string payload, out T? message)
    {
        try
        {
            message = JsonSerializer.Deserialize<T>(payload, JsonOptions);
            return message != null;
        }
        catch (JsonException)
        {
            message = default;
            return false;
        }
    }
}

public sealed record MqttDroneStatusPayload(
    string? DroneCode,
    string? Status,
    double? Battery,
    DateTime? Timestamp);

public sealed record MqttDroneTelemetryPayload(
    string? DroneCode,
    DateTime? Timestamp,
    double Latitude,
    double Longitude,
    double? Altitude,
    double? Battery,
    double? Speed,
    double? Heading);
