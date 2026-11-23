using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Application.Features.Telemetry.DTOs;

public sealed record DroneTelemetryDto
{
    public Guid Id { get; init; }
    public Guid UavId { get; init; }

    [JsonPropertyName("drone_id")]
    public string DroneId { get; init; } = string.Empty;

    public Guid? MissionId { get; init; }

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    public DateTime ReceivedAt { get; init; }

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

    public bool IsOnline { get; init; }
}

public sealed record DroneConnectionEventDto(
    string DroneCode,
    Guid UavId,
    Guid? MissionId,
    bool IsOnline,
    DateTime Timestamp);

public sealed record PagedMissionTelemetryResponse(
    Guid MissionId,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<DroneTelemetryDto> Items);

public interface ITelemetryRealtimeNotifier
{
    Task NotifyTelemetryUpdatedAsync(DroneTelemetryDto telemetry, CancellationToken ct = default);
    Task NotifyDroneOnlineAsync(DroneConnectionEventDto evt, CancellationToken ct = default);
    Task NotifyDroneOfflineAsync(DroneConnectionEventDto evt, CancellationToken ct = default);
}

public interface IDroneTelemetryService
{
    Task<DroneTelemetryDto?> IngestAsync(string topic, string rawJsonPayload, CancellationToken ct = default);
    Task<DroneTelemetryDto> GetLatestByDroneCodeAsync(string droneCode, CancellationToken ct = default);
    Task<PagedMissionTelemetryResponse> GetMissionTelemetryAsync(
        Guid missionId,
        DateTime? from,
        DateTime? to,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default);
    Task<int> CheckOfflineDronesAsync(CancellationToken ct = default);
    Task<int> PurgeExpiredTelemetryAsync(CancellationToken ct = default);
}
