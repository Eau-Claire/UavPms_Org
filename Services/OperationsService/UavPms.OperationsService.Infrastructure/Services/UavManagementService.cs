using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Common.Interfaces;
using UavPms.OperationsService.Application.Features.Assessments;
using UavPms.OperationsService.Application.Features.Assessments.DTOs;
using UavPms.OperationsService.Application.Features.Assessments.Policies;
using UavPms.OperationsService.Application.Features.Drones;
using UavPms.OperationsService.Application.Features.Drones.DTOs;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Enums;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Infrastructure.Services;

public sealed class UavManagementService : IUavManagementService
{
    private readonly ApplicationDbContext _db;
    private readonly ICurrentUserServices _current;
    private readonly IDroneTechnicalInspectionService _inspectionService;

    public UavManagementService(
        ApplicationDbContext db,
        ICurrentUserServices current,
        IDroneTechnicalInspectionService inspectionService)
    {
        _db = db;
        _current = current;
        _inspectionService = inspectionService;
    }

    public async Task<IReadOnlyList<UavDetailsDto>> GetDronesAsync(
        string? search = null,
        string? operationalStatus = null,
        string? technicalHealth = null,
        Guid? regionId = null,
        bool? availableOnly = null,
        CancellationToken ct = default)
    {
        var query = _db.Uavs
            .Include(x => x.AssignedRegion)
            .Include(x => x.FlightControllers.Where(fc => !fc.IsDeleted))
            .Include(x => x.TechnicalInspections.Where(t => !t.IsDeleted).OrderByDescending(t => t.CompletedAt).Take(1))
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x =>
                x.UavCode.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                x.Model.ToLower().Contains(term) ||
                x.SerialNumber.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(operationalStatus) &&
            Enum.TryParse<DroneOperationalStatus>(operationalStatus, true, out var opStatus))
        {
            query = query.Where(x => x.OperationalStatus == opStatus);
        }

        if (!string.IsNullOrWhiteSpace(technicalHealth) &&
            Enum.TryParse<TechnicalHealth>(technicalHealth, true, out var health))
        {
            query = query.Where(x => x.TechnicalHealth == health);
        }

        if (regionId.HasValue && regionId.Value != Guid.Empty)
        {
            query = query.Where(x => x.AssignedRegionId == regionId.Value);
        }

        if (availableOnly == true)
        {
            query = query.Where(x => x.OperationalStatus == DroneOperationalStatus.Available);
        }

        var drones = await query
            .OrderBy(x => x.UavCode)
            .ToListAsync(ct);

        return drones.Select(MapToDetailsDto).ToList();
    }

    public async Task<UavDetailsDto> GetDroneByIdAsync(Guid droneId, CancellationToken ct = default)
    {
        var drone = await _db.Uavs
            .Include(x => x.AssignedRegion)
            .Include(x => x.FlightControllers.Where(fc => !fc.IsDeleted))
            .Include(x => x.TechnicalInspections.Where(t => !t.IsDeleted).OrderByDescending(t => t.CompletedAt).Take(1))
                .ThenInclude(t => t.Technician)
            .Include(x => x.TechnicalInspections)
                .ThenInclude(t => t.Metrics)
            .SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        return MapToDetailsDto(drone);
    }

    public async Task<UavDetailsDto> CreateDroneAsync(CreateUavRequest request, CancellationToken ct = default)
    {
        EnsureManagerOrAdmin();

        if (string.IsNullOrWhiteSpace(request.Code))
            throw new BusinessRuleException("DRONE_CODE_REQUIRED", "Mã Drone (UAV Code) không được để trống.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new BusinessRuleException("DRONE_NAME_REQUIRED", "Tên hiển thị Drone không được để trống.");

        var cleanCode = request.Code.Trim().ToUpperInvariant();
        var codeExists = await _db.Uavs.AnyAsync(x => x.UavCode == cleanCode && !x.IsDeleted, ct);
        if (codeExists)
            throw new BusinessRuleException("DUPLICATE_DRONE_CODE", $"Mã Drone {cleanCode} đã tồn tại trong hệ thống.");

        string? regionName = request.AssignedRegionName;
        if (request.AssignedRegionId.HasValue && string.IsNullOrWhiteSpace(regionName))
        {
            var region = await _db.Regions.FindAsync(new object[] { request.AssignedRegionId.Value }, ct);
            regionName = region?.Name;
        }

        var drone = new Uav
        {
            Id = Guid.NewGuid(),
            UavCode = cleanCode,
            Name = request.Name.Trim(),
            Model = request.Model.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "Hexacopter" : request.Category.Trim(),
            SerialNumber = request.SerialNumber?.Trim() ?? string.Empty,
            AssignedRegionId = request.AssignedRegionId,
            OperationalStatus = request.OperationalStatus ?? DroneOperationalStatus.Available,
            TechnicalHealth = request.TechnicalHealth ?? TechnicalHealth.Unknown,
            FlightEnduranceMinutes = request.FlightEnduranceMinutes.GetValueOrDefault(30.0),
            OperationalRangeKm = request.OperationalRangeKm.GetValueOrDefault(10.0),
            CameraPayloadInfo = request.CameraPayloadInfo?.Trim(),
            BatteryLevel = request.BatteryLevel.GetValueOrDefault(100.0),
            FlightStatus = DroneStatus.Idle,
            Version = 1,
            CreatedAt = DateTime.UtcNow
        };

        _db.Uavs.Add(drone);
        await _db.SaveChangesAsync(ct);

        return MapToDetailsDto(drone);
    }

    public async Task<UavDetailsDto> UpdateDroneAsync(Guid droneId, UpdateUavRequest request, CancellationToken ct = default)
    {
        EnsureManagerOrAdmin();

        var drone = await _db.Uavs
            .Include(x => x.AssignedRegion)
            .Include(x => x.FlightControllers.Where(fc => !fc.IsDeleted))
            .Include(x => x.TechnicalInspections.Where(t => !t.IsDeleted).OrderByDescending(t => t.CompletedAt).Take(1))
            .SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        if (!string.IsNullOrWhiteSpace(request.Name))
            drone.Name = request.Name.Trim();

        if (!string.IsNullOrWhiteSpace(request.Model))
            drone.Model = request.Model.Trim();

        if (!string.IsNullOrWhiteSpace(request.Category))
            drone.Category = request.Category.Trim();

        if (request.SerialNumber != null)
            drone.SerialNumber = request.SerialNumber.Trim();

        if (request.AssignedRegionId.HasValue)
        {
            drone.AssignedRegionId = request.AssignedRegionId;
        }

        if (request.OperationalStatus.HasValue)
            drone.OperationalStatus = request.OperationalStatus.Value;

        if (request.TechnicalHealth.HasValue)
        {
            drone.TechnicalHealth = request.TechnicalHealth.Value;
        }

        if (request.FlightEnduranceMinutes.HasValue && request.FlightEnduranceMinutes.Value > 0)
            drone.FlightEnduranceMinutes = request.FlightEnduranceMinutes.Value;

        if (request.OperationalRangeKm.HasValue && request.OperationalRangeKm.Value > 0)
            drone.OperationalRangeKm = request.OperationalRangeKm.Value;

        if (request.CameraPayloadInfo != null)
            drone.CameraPayloadInfo = request.CameraPayloadInfo.Trim();

        if (request.BatteryLevel.HasValue)
            drone.UpdateBatteryLevel(request.BatteryLevel.Value);

        drone.UpdatedAt = DateTime.UtcNow;
        drone.Version++;

        await _db.SaveChangesAsync(ct);

        return MapToDetailsDto(drone);
    }

    public async Task<bool> DeleteDroneAsync(Guid droneId, CancellationToken ct = default)
    {
        EnsureManagerOrAdmin();

        var drone = await _db.Uavs
            .Include(x => x.FlightControllers)
            .SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        drone.IsDeleted = true;
        drone.DeletedAt = DateTime.UtcNow;

        // Deactivate active flight controllers
        foreach (var fc in drone.FlightControllers.Where(x => x.UnpairedAt == null))
        {
            fc.UnpairedAt = DateTime.UtcNow;
            fc.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DroneTechnicalInspectionSummaryDto>> GetTechnicalInspectionsForDroneAsync(
        Guid droneId,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var droneExists = await _db.Uavs.AnyAsync(x => x.Id == droneId && !x.IsDeleted, ct);
        if (!droneExists)
            throw new NotFoundException("Drone", droneId);

        var inspections = await _db.DroneTechnicalInspections
            .Include(x => x.Technician)
            .Include(x => x.Metrics)
            .Where(x => x.DroneId == droneId && !x.IsDeleted)
            .OrderByDescending(x => x.CompletedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return inspections.Select(MapInspectionToSummaryDto).ToList();
    }

    public async Task<DroneTechnicalInspectionSummaryDto> SubmitBasicTechnicalCheckAsync(
        Guid droneId,
        BasicTechnicalCheckRequest request,
        CancellationToken ct = default)
    {
        var drone = await _db.Uavs
            .SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        var metrics = new List<DroneMetricSubmitDto>
        {
            new DroneMetricSubmitDto(
                "FRAME_BODY",
                "Airframe",
                null,
                null,
                request.FrameBodyNotes ?? (request.FrameBodyPassed ? "Kết cấu khung vỏ đạt tiêu chuẩn" : "Phát hiện vết nứt/hư hỏng khung vỏ"),
                null,
                request.FrameBodyPassed,
                true,
                true,
                request.FrameBodyPassed ? "Low" : "Critical"
            ),
            new DroneMetricSubmitDto(
                "PROP_INTEGRITY",
                "Propulsion",
                null,
                null,
                request.PropellersNotes ?? (request.PropellersPassed ? "Cánh quạt nguyên vẹn, cân bằng động tốt" : "Cánh quạt bị mẻ/cong vênh"),
                null,
                request.PropellersPassed,
                true,
                true,
                request.PropellersPassed ? "Low" : "Critical"
            ),
            new DroneMetricSubmitDto(
                "MOTORS",
                "Propulsion",
                null,
                null,
                request.MotorsNotes ?? (request.MotorsPassed ? "Động cơ quay êm, nhiệt độ bình thường" : "Động cơ kẹt/phát tiếng ồn bất thường"),
                null,
                request.MotorsPassed,
                true,
                true,
                request.MotorsPassed ? "Low" : "Critical"
            ),
            new DroneMetricSubmitDto(
                "CAMERA_PAYLOAD",
                "Payload",
                null,
                null,
                request.CameraPayloadNotes ?? (request.CameraPayloadPassed ? "Gimbal & cảm biến camera hoạt động tốt" : "Lỗi tín hiệu Gimbal/Camera"),
                null,
                request.CameraPayloadPassed,
                false,
                false,
                request.CameraPayloadPassed ? "Low" : "Medium"
            ),
            new DroneMetricSubmitDto(
                "BATTERY_HEALTH",
                "Power",
                request.BatteryPercent ?? (decimal)drone.BatteryLevel,
                null,
                $"Điện áp: {request.BatteryVoltage ?? 24.8m}V | Cell: {request.BatteryCellCount ?? 6} | Ghi chú: {request.BatteryNotes ?? "Bình thường"}",
                "%",
                request.BatteryPassed,
                true,
                true,
                request.BatteryPassed ? "Low" : "Critical"
            ),
            new DroneMetricSubmitDto(
                "GPS_RTK_FIX",
                "Avionics",
                request.GpsSatelliteCount,
                null,
                $"Trạng thái GPS Fix: {request.GpsFixStatus ?? "3D_FIX_RTK"} | Vệ tinh: {request.GpsSatelliteCount?.ToString() ?? "N/A"}",
                "Sats",
                request.GpsPassed,
                true,
                true,
                request.GpsPassed ? "Low" : "Critical"
            ),
            new DroneMetricSubmitDto(
                "RF_LINK_TELEMETRY",
                "Communication",
                request.CommunicationConnected == false ? 0m : (decimal?)null,
                null,
                $"Tín hiệu: {request.CommunicationSignalInfo ?? "RSSI -62dBm"} | Kết nối: {(request.CommunicationConnected != false ? "Connected" : "Disconnected")}",
                "%",
                request.CommunicationPassed,
                true,
                true,
                request.CommunicationPassed ? "Low" : "Critical"
            ),
            new DroneMetricSubmitDto(
                "FLIGHT_CONTROLLER",
                "Avionics",
                null,
                null,
                $"FC UID: {request.FlightControllerUid ?? "BF-FC"} | Bo mạch: {request.FlightControllerBoard ?? "STM32F405"} | Firmware: {request.FlightControllerFirmware ?? "Betaflight 4.5.1"}",
                null,
                request.FlightControllerPassed,
                true,
                true,
                request.FlightControllerPassed ? "Low" : "Critical"
            )
        };

        var submitReq = new DroneInspectionSubmitRequest(
            drone.Id,
            request.Notes ?? "Kiểm tra kỹ thuật định kỳ cơ bản (Basic Technical Checklist)",
            metrics,
            request.PolicyVersion ?? "v2.0",
            request.FlightControllerUid,
            request.PreMissionAssessmentId
        );

        var inspection = await _inspectionService.SubmitInspectionAsync(submitReq, ct);

        return MapInspectionToSummaryDto(inspection);
    }

    /// <summary>
    /// GIS scope estimate (BR-21). Suitability is judged from the scope and the UAV's endurance/range only
    /// (<see cref="UavScopeSuitabilityPolicy"/>); availability and technical readiness are reported separately in
    /// IsAvailable / IsTechnicallyReady. With an assessment, the corridor comes from the real tower coordinates
    /// in sequence order and availability/readiness use the same evaluator as the assessment (window, bookings,
    /// inspection validity).
    /// </summary>
    public async Task<GisDroneEstimationResult> EstimateGisScopeDronesAsync(
        GisDroneEstimationRequest request,
        CancellationToken ct = default)
    {
        double corridorKm = request.CorridorLengthKm.GetValueOrDefault(0.0);
        int assetCount = request.AssetCount.GetValueOrDefault(0);
        PreMissionAssessment? assessment = null;

        if (request.AssessmentId.HasValue && request.AssessmentId.Value != Guid.Empty)
        {
            assessment = await _db.PreMissionAssessments
                .Include(a => a.Towers)
                .SingleOrDefaultAsync(a => a.Id == request.AssessmentId.Value && !a.IsDeleted, ct);

            if (assessment != null && assessment.Towers.Count > 0)
            {
                var (towerCorridorKm, towerCount) = await DroneReadinessEvaluator.EstimateScopeAsync(_db, assessment.Towers, ct);
                assetCount = Math.Max(assetCount, towerCount);
                if (corridorKm <= 0)
                    corridorKm = towerCorridorKm;
            }
        }

        if (corridorKm <= 0 && assetCount <= 0) corridorKm = 5.0; // Manual estimate without any input: 5 km corridor
        if (assetCount <= 0) assetCount = (int)Math.Max(1, Math.Round(corridorKm / UavScopeSuitabilityPolicy.FallbackTowerSpacingKm));

        var totalMissionMinutes = Math.Round(
            corridorKm / UavScopeSuitabilityPolicy.AverageSpeedKmh * 60.0 + assetCount * UavScopeSuitabilityPolicy.InspectionMinutesPerTower, 1);

        IReadOnlyList<(Uav Drone, DroneEligibilityResult Result)> evaluated;
        if (assessment != null)
        {
            evaluated = await DroneReadinessEvaluator.EvaluateAsync(
                _db, assessment.PlannedStart, assessment.PlannedEnd, corridorKm, assetCount, ct);
        }
        else
        {
            var query = _db.Uavs.Where(x => !x.IsDeleted);
            if (request.RegionId.HasValue && request.RegionId.Value != Guid.Empty)
                query = query.Where(x => x.AssignedRegionId == null || x.AssignedRegionId == request.RegionId.Value);
            var drones = await query.OrderBy(x => x.UavCode).ToListAsync(ct);
            // No planned window: report the stored status and health; suitability is still scope-only.
            evaluated = drones
                .Select(d => (d, new DroneEligibilityResult(
                    d.OperationalStatus == DroneOperationalStatus.Available,
                    false,
                    d.TechnicalHealth is TechnicalHealth.Healthy or TechnicalHealth.Warning
                        ? ResourceEligibilityStatus.Eligible
                        : ResourceEligibilityStatus.Ineligible,
                    d.TechnicalHealth,
                    null,
                    UavScopeSuitabilityPolicy.Evaluate(corridorKm, assetCount, d.FlightEnduranceMinutes, d.OperationalRangeKm),
                    null)))
                .ToList();
        }

        return new GisDroneEstimationResult
        {
            CorridorLengthKm = corridorKm,
            AssetCount = assetCount,
            EstimatedMissionDurationMinutes = totalMissionMinutes,
            AverageSpeedKmh = UavScopeSuitabilityPolicy.AverageSpeedKmh,
            ReserveBatteryPercent = UavScopeSuitabilityPolicy.ReserveBatteryPercent,
            InspectionMinutesPerAsset = UavScopeSuitabilityPolicy.InspectionMinutesPerTower,
            Drones = evaluated.Select(x => ToCapabilityItem(x.Drone, x.Result)).ToList()
        };
    }

    /// <summary>
    /// GET /drones/eligible?assessmentId= — the UAVs that the assessment evaluation would mark eligible right now
    /// (BR-20: Available, no booking conflict in the window, a valid non-failed inspection, scope not Unsuitable).
    /// </summary>
    public async Task<IReadOnlyList<GisDroneCapabilityItemDto>> GetEligibleDronesForAssessmentAsync(
        Guid assessmentId,
        CancellationToken ct = default)
    {
        var assessment = await _db.PreMissionAssessments
            .Include(a => a.Towers)
            .SingleOrDefaultAsync(a => a.Id == assessmentId && !a.IsDeleted, ct)
            ?? throw new NotFoundException("PreMissionAssessment", assessmentId);

        var (corridorKm, towerCount) = await DroneReadinessEvaluator.EstimateScopeAsync(_db, assessment.Towers, ct);
        var evaluated = await DroneReadinessEvaluator.EvaluateAsync(
            _db, assessment.PlannedStart, assessment.PlannedEnd, corridorKm, towerCount, ct);

        return evaluated
            .Where(x => x.Result.IsEligible)
            .Select(x => ToCapabilityItem(x.Drone, x.Result))
            .ToList();
    }

    private static GisDroneCapabilityItemDto ToCapabilityItem(Uav drone, DroneEligibilityResult result)
    {
        var scope = result.Scope;
        return new GisDroneCapabilityItemDto
        {
            DroneId = drone.Id,
            DroneCode = drone.UavCode,
            DroneName = drone.Name,
            Model = drone.Model,
            Category = drone.Category,
            OperationalStatus = drone.OperationalStatus.ToString(),
            TechnicalHealth = result.TechnicalHealth.ToString(),
            FlightEnduranceMinutes = drone.FlightEnduranceMinutes,
            OperationalRangeKm = drone.OperationalRangeKm,
            EffectiveFlightMinutes = scope.EffectiveFlightMinutes,
            MaxCoverageDistanceKm = scope.MaxCoverageDistanceKm,
            EstimatedFlightsNeeded = scope.EstimatedFlightsNeeded,
            EstimatedDronesNeeded = (int)Math.Max(1, Math.Ceiling(scope.EstimatedFlightsNeeded / 2.0)),
            Suitability = scope.Suitability switch
            {
                UavScopeSuitability.Suitable => "SUITABLE",
                UavScopeSuitability.SuitableWithSplit => "SUITABLE_WITH_SPLIT",
                _ => "UNSUITABLE"
            },
            IsAvailable = result.IsOperationallyAvailable,
            IsTechnicallyReady = result.IsTechnicallyEligible,
            Recommendation = scope.Reason
        };
    }

    private static UavDetailsDto MapToDetailsDto(Uav drone)
    {
        var activeFc = drone.FlightControllers
            .Where(fc => fc.UnpairedAt == null && !fc.IsDeleted)
            .OrderByDescending(fc => fc.PairedAt)
            .FirstOrDefault();

        var latestInspection = drone.TechnicalInspections
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.CompletedAt)
            .FirstOrDefault();

        return new UavDetailsDto
        {
            Id = drone.Id,
            Code = drone.UavCode,
            Name = string.IsNullOrWhiteSpace(drone.Name) ? drone.UavCode : drone.Name,
            Model = drone.Model,
            Category = drone.Category,
            SerialNumber = drone.SerialNumber,
            AssignedRegionId = drone.AssignedRegionId,
            AssignedRegionName = drone.AssignedRegion?.Name,
            OperationalStatus = drone.OperationalStatus.ToString().ToUpperInvariant(),
            TechnicalHealth = drone.TechnicalHealth.ToString().ToUpperInvariant(),
            FlightEnduranceMinutes = drone.FlightEnduranceMinutes,
            OperationalRangeKm = drone.OperationalRangeKm,
            CameraPayloadInfo = drone.CameraPayloadInfo,
            BatteryLevel = drone.BatteryLevel,
            LastMaintenanceAt = drone.LastMaintenanceAt,
            ActiveFlightController = activeFc != null ? new FlightControllerBindingDto(
                activeFc.Id,
                activeFc.DroneId,
                activeFc.FlightControllerUid,
                activeFc.BoardIdentifier,
                activeFc.FirmwareName,
                activeFc.FirmwareVersion,
                activeFc.PairedAt,
                activeFc.UnpairedAt,
                activeFc.LastSeenAt,
                activeFc.UnpairedAt == null
            ) : null,
            LastTechnicalInspection = latestInspection != null ? MapInspectionToSummaryDto(latestInspection) : null,
            CreatedAt = drone.CreatedAt,
            UpdatedAt = drone.UpdatedAt
        };
    }

    private static DroneTechnicalInspectionSummaryDto MapInspectionToSummaryDto(DroneTechnicalInspection inspection)
    {
        return new DroneTechnicalInspectionSummaryDto
        {
            Id = inspection.Id,
            Status = inspection.Status.ToString(),
            Health = inspection.Health.ToString(),
            CompletedAt = inspection.CompletedAt,
            ValidUntil = inspection.ValidUntil,
            TechnicianName = inspection.Technician?.FullName ?? inspection.Technician?.Email,
            Notes = inspection.Notes,
            MetricCount = inspection.Metrics?.Count ?? 0
        };
    }

    private void EnsureManagerOrAdmin()
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var isAuthorized = _current.Roles.Any(r =>
            r.Equals(UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.MaintenanceTechnician, StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Technician", StringComparison.OrdinalIgnoreCase));

        if (!isAuthorized)
            throw new ForbiddenException("INSUFFICIENT_PERMISSIONS");
    }
}
