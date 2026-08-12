using System.ComponentModel.DataAnnotations;

namespace UavPms.OperationsService.Application.Common.Options;

public class MqttOptions
{
    public const string SectionName = "Mqtt";

    public bool Enabled { get; init; }

    [Required]
    public string Host { get; init; } = "mosquitto";

    [Range(1, 65535)]
    public int Port { get; init; } = 1883;

    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = "uav-pms-operations-service";

    [Required]
    public string StatusTopic { get; init; } = "uav/+/status";

    [Required]
    public string TelemetryTopic { get; init; } = "uav/+/telemetry";

    [Range(1, 3600)]
    public int OfflineTimeoutSeconds { get; init; } = 30;

    [Range(1, 3600)]
    public int LiveStateTtlSeconds { get; init; } = 30;
}
