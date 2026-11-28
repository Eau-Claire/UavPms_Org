using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Npgsql;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Options;
using UavPms.OperationsService.Application.Features.Telemetry.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Messaging;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class DroneTelemetryService : IDroneTelemetryService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _currentUser;
    private readonly InMemoryDroneLiveStateService _liveState;
    private readonly ITelemetryRealtimeNotifier? _notifier;
    private readonly MqttOptions _options;
    private readonly ILogger<DroneTelemetryService> _logger;

    public DroneTelemetryService(
        ApplicationDbContext db,
        ICurrentUserServices currentUser,
        InMemoryDroneLiveStateService liveState,
        IOptions<MqttOptions> options,
        ILogger<DroneTelemetryService> logger,
        ITelemetryRealtimeNotifier? notifier = null)
    {
        _db = db;
        _currentUser = currentUser;
        _liveState = liveState;
        _options = options.Value;
        _logger = logger;
        _notifier = notifier;
    }

    public async Task<DroneTelemetryDto?> IngestAsync(string topic, string rawJsonPayload, CancellationToken ct = default)
    {
        if (!MqttDroneMessageParser.TryParseAndValidateTelemetry(
                topic,
                rawJsonPayload,
                out var payload,
                out var sourceTimestampUtc,
                out var validationError) || payload == null)
        {
            _logger.LogWarning("Dropped invalid MQTT telemetry message on topic {Topic}: {Reason}", topic, validationError);
            return null;
        }

        var droneCode = payload.DroneId!;
        var uav = await _db.Uavs.FirstOrDefaultAsync(u => u.UavCode == droneCode && !u.IsDeleted, ct);
        if (uav == null)
        {
            _logger.LogWarning("Dropped MQTT telemetry message for unknown drone '{DroneCode}' on topic {Topic}.", droneCode, topic);
            return null;
        }

        // TR-04: Find the active mission where Missions.UavId = drone and Status = InProgress.
        // With no active mission, keep MissionId = null.
        var activeMissionId = await _db.Missions
            .AsNoTracking()
            .Where(m => m.UavId == uav.Id && m.Status == MissionStatus.InProgress && !m.IsDeleted)
            .OrderByDescending(m => m.StartedAt ?? m.CreatedAt)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct);

        // TR-05: Deduplication on (UavId, SourceTimestamp)
        var alreadyExists = await _db.DroneTelemetry
            .AsNoTracking()
            .AnyAsync(t => t.UavId == uav.Id && t.SourceTimestamp == sourceTimestampUtc && !t.IsDeleted, ct);
        if (alreadyExists)
        {
            _logger.LogDebug(
                "Ignored duplicate telemetry message for drone '{DroneCode}' at {SourceTimestamp:O}.",
                droneCode, sourceTimestampUtc);
            return null;
        }

        var receivedAtUtc = DateTime.UtcNow;
        Point? location = null;
        if (payload.Lat.HasValue && payload.Lon.HasValue)
        {
            location = new Point(payload.Lon.Value, payload.Lat.Value) { SRID = 4326 };
        }

        var entity = new DroneTelemetry
        {
            Id = Guid.NewGuid(),
            UavId = uav.Id,
            MissionId = activeMissionId,
            SourceTimestamp = sourceTimestampUtc,
            ReceivedAt = receivedAtUtc,
            FlightMode = payload.FlightMode,
            Armed = payload.Armed,
            Location = location,
            AltRelativeM = payload.AltRelativeM,
            AltMslM = payload.AltMslM,
            Satellites = payload.Satellites,
            GpsFixType = payload.GpsFixType,
            Hdop = payload.Hdop,
            RollDeg = payload.RollDeg,
            PitchDeg = payload.PitchDeg,
            YawDeg = payload.YawDeg,
            GroundSpeedMs = payload.GroundSpeedMs,
            AirSpeedMs = payload.AirSpeedMs,
            ClimbRateMs = payload.ClimbRateMs,
            HeadingDeg = payload.HeadingDeg,
            ThrottlePct = payload.ThrottlePct,
            BatteryVoltageV = payload.BatteryVoltageV,
            BatteryCurrentA = payload.BatteryCurrentA,
            BatteryRemainingPct = payload.BatteryRemainingPct,
            CreatedAt = receivedAtUtc
        };

        _db.DroneTelemetry.Add(entity);

        // TR-05: Update latest state on UAV only when SourceTimestamp is strictly newer than LastTelemetryAt.
        // Never overwrite CurrentLocation with null when lat/lon are null.
        if (!uav.LastTelemetryAt.HasValue || sourceTimestampUtc > uav.LastTelemetryAt.Value)
        {
            uav.LastTelemetryAt = sourceTimestampUtc;
            if (location != null)
            {
                uav.CurrentLocation = location;
            }
            if (payload.BatteryRemainingPct.HasValue && payload.BatteryRemainingPct.Value is >= 0 and <= 100)
            {
                uav.BatteryLevel = payload.BatteryRemainingPct.Value;
            }
            uav.UpdatedAt = receivedAtUtc;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _db.ChangeTracker.Clear();
            _logger.LogDebug(
                "Ignored concurrent duplicate telemetry message for drone '{DroneCode}' at {SourceTimestamp:O}.",
                droneCode, sourceTimestampUtc);
            return null;
        }

        var transitionedToOnline = _liveState.MarkMessageReceived(droneCode, uav.Id, activeMissionId, receivedAtUtc);
        await _liveState.UpdateTelemetryAsync(
            new DroneLiveTelemetry(
                uav.Id,
                uav.UavCode,
                sourceTimestampUtc,
                payload.Lat ?? double.NaN,
                payload.Lon ?? double.NaN,
                payload.AltRelativeM ?? payload.AltMslM,
                payload.BatteryRemainingPct,
                payload.GroundSpeedMs,
                payload.HeadingDeg ?? payload.YawDeg),
            TimeSpan.FromSeconds(_options.LiveStateTtlSeconds),
            ct);

        var dto = ToDto(entity, uav.UavCode, isOnline: true);

        if (_notifier != null)
        {
            if (transitionedToOnline)
            {
                await _notifier.NotifyDroneOnlineAsync(
                    new DroneConnectionEventDto(uav.UavCode, uav.Id, activeMissionId, true, receivedAtUtc),
                    ct);
            }

            await _notifier.NotifyTelemetryUpdatedAsync(dto, ct);
        }

        return dto;
    }

    public async Task<DroneTelemetryDto> GetLatestByDroneCodeAsync(string droneCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(droneCode))
            throw new BusinessRuleException("DRONE_CODE_REQUIRED", "Drone code is required.");

        var normalizedCode = droneCode.Trim();
        var uav = await _db.Uavs.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UavCode == normalizedCode && !u.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", normalizedCode);

        await EnsureCanAccessDroneTelemetryAsync(uav.Id, ct);

        var latest = await _db.DroneTelemetry.AsNoTracking()
            .Where(t => t.UavId == uav.Id && !t.IsDeleted)
            .OrderByDescending(t => t.SourceTimestamp)
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("DroneTelemetry", normalizedCode);

        var live = await _liveState.GetAsync(uav.UavCode, ct);
        var isOnline = live != null
            && live.Online
            && (DateTime.UtcNow - live.LastSeenAt) <= TimeSpan.FromSeconds(_options.OfflineTimeoutSeconds);

        // If the latest row has null GPS (no GPS fix), preserve the last known non-null position from UAVs.CurrentLocation
        var lat = latest.Location?.Y ?? uav.CurrentLocation?.Y;
        var lon = latest.Location?.X ?? uav.CurrentLocation?.X;

        return ToDto(latest, uav.UavCode, isOnline) with
        {
            Lat = lat,
            Lon = lon
        };
    }

    public async Task<PagedMissionTelemetryResponse> GetMissionTelemetryAsync(
        Guid missionId,
        DateTime? from,
        DateTime? to,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 100;
        if (pageSize > 1000) pageSize = 1000;

        var mission = await _db.Missions.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == missionId && !m.IsDeleted, ct)
            ?? throw new NotFoundException("Mission", missionId);

        await EnsureCanAccessMissionTelemetryAsync(mission, ct);

        var query = _db.DroneTelemetry.AsNoTracking()
            .Include(t => t.Uav)
            .Where(t => t.MissionId == missionId && !t.IsDeleted);

        if (from.HasValue)
        {
            var fromUtc = NormalizeUtc(from.Value);
            query = query.Where(t => t.SourceTimestamp >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = NormalizeUtc(to.Value);
            query = query.Where(t => t.SourceTimestamp <= toUtc);
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        var rows = await query
            .OrderBy(t => t.SourceTimestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = rows
            .Select(r => ToDto(r, r.Uav?.UavCode ?? string.Empty, isOnline: false))
            .ToList();

        return new PagedMissionTelemetryResponse(
            missionId,
            page,
            pageSize,
            totalCount,
            totalPages,
            items);
    }

    public async Task<int> CheckOfflineDronesAsync(CancellationToken ct = default)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.OfflineTimeoutSeconds));
        var nowUtc = DateTime.UtcNow;
        var timedOut = _liveState.SweepTimedOutDrones(timeout, nowUtc);

        if (timedOut.Count > 0 && _notifier != null)
        {
            foreach (var (droneCode, uavId, missionId, _) in timedOut)
            {
                _logger.LogInformation(
                    "Drone '{DroneCode}' marked Offline after exceeding {TimeoutSeconds}s telemetry timeout.",
                    droneCode, _options.OfflineTimeoutSeconds);

                await _notifier.NotifyDroneOfflineAsync(
                    new DroneConnectionEventDto(droneCode, uavId, missionId, false, nowUtc),
                    ct);
            }
        }

        return timedOut.Count;
    }

    public async Task<int> PurgeExpiredTelemetryAsync(CancellationToken ct = default)
    {
        var retentionDays = Math.Max(1, _options.RetentionDays);
        var thresholdUtc = DateTime.UtcNow.AddDays(-retentionDays);

        var expired = await _db.DroneTelemetry
            .IgnoreQueryFilters()
            .Where(t => t.SourceTimestamp < thresholdUtc)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return 0;

        _db.DroneTelemetry.RemoveRange(expired);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Purged {Count} telemetry history rows older than {RetentionDays} days (before {Threshold:O}).",
            expired.Count, retentionDays, thresholdUtc);

        return expired.Count;
    }

    private async Task EnsureCanAccessMissionTelemetryAsync(Mission mission, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var roles = _currentUser.Roles ?? Array.Empty<string>();
        if (roles.Contains(UserRoles.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            return;

        if (mission.ManagerId == _currentUser.UserId)
            return;

        var hasActiveAssignment = await _db.MissionAssignments.AsNoTracking()
            .AnyAsync(a => a.MissionId == mission.Id
                        && a.UserId == _currentUser.UserId
                        && a.Status == MissionAssignmentStatus.Active
                        && !a.IsDeleted, ct);

        if (!hasActiveAssignment)
        {
            throw new ForbiddenException("MISSION_TELEMETRY_ACCESS_DENIED");
        }
    }

    private async Task EnsureCanAccessDroneTelemetryAsync(Guid uavId, CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        // Cùng luật với danh sách Drone: Drone thuộc vùng người gọi hoặc của nhiệm vụ đang được giao (Admin mọi Drone)
        var canSeeDrone = await DroneScope.Visible(_db, _currentUser).AnyAsync(u => u.Id == uavId, ct);
        if (!canSeeDrone)
            throw new ForbiddenException("DRONE_TELEMETRY_ACCESS_DENIED");
    }

    private static DateTime NormalizeUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    internal static DroneTelemetryDto ToDto(DroneTelemetry e, string droneCode, bool isOnline) => new()
    {
        Id = e.Id,
        UavId = e.UavId,
        DroneId = droneCode,
        MissionId = e.MissionId,
        Timestamp = e.SourceTimestamp,
        ReceivedAt = e.ReceivedAt,
        FlightMode = e.FlightMode,
        Armed = e.Armed,
        Lat = e.Location?.Y,
        Lon = e.Location?.X,
        AltRelativeM = e.AltRelativeM,
        AltMslM = e.AltMslM,
        Satellites = e.Satellites,
        GpsFixType = e.GpsFixType,
        Hdop = e.Hdop,
        RollDeg = e.RollDeg,
        PitchDeg = e.PitchDeg,
        YawDeg = e.YawDeg,
        GroundSpeedMs = e.GroundSpeedMs,
        AirSpeedMs = e.AirSpeedMs,
        ClimbRateMs = e.ClimbRateMs,
        HeadingDeg = e.HeadingDeg,
        ThrottlePct = e.ThrottlePct,
        BatteryVoltageV = e.BatteryVoltageV,
        BatteryCurrentA = e.BatteryCurrentA,
        BatteryRemainingPct = e.BatteryRemainingPct,
        IsOnline = isOnline
    };
}
