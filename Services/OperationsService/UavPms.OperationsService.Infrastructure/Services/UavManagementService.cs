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
        var drone = await _db.Uavs.SingleOrDefaultAsync(x => x.Id == droneId && !x.IsDeleted, ct)
            ?? throw new NotFoundException("Drone", droneId);

        // Lịch sử kiểm tra chỉ đọc được khi Drone thuộc vùng của người gọi (SystemAdmin được bỏ qua).
        await DroneRegionScope.EnsureAsync(_db, _current, drone, ct);

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

        // Zero Fake Data: chỉ tạo hạng mục khi client gửi kết luận hoặc số đo thật; không điền giá trị mặc định.
        var checklist = new[]
        {
            (Code: "FRAME_BODY", Required: true,
                Metric: BuildChecklistMetric("FRAME_BODY", "Airframe", request.FrameBodyPassed, null, null, null,
                    request.FrameBodyNotes, critical: true)),
            (Code: "PROP_INTEGRITY", Required: true,
                Metric: BuildChecklistMetric("PROP_INTEGRITY", "Propulsion", request.PropellersPassed, null, null, null,
                    request.PropellersNotes, critical: true)),
            (Code: "MOTORS", Required: true,
                Metric: BuildChecklistMetric("MOTORS", "Propulsion", request.MotorsPassed, null, null, null,
                    request.MotorsNotes, critical: true)),
            (Code: "CAMERA_PAYLOAD", Required: false,
                Metric: BuildChecklistMetric("CAMERA_PAYLOAD", "Payload", request.CameraPayloadPassed, null, null, null,
                    request.CameraPayloadNotes, critical: false)),
            (Code: "BATTERY_HEALTH", Required: true,
                Metric: BuildChecklistMetric("BATTERY_HEALTH", "Power", request.BatteryPassed, request.BatteryPercent, null, "%",
                    Detail("Ghi chú", request.BatteryNotes), critical: true)),
            (Code: "GPS_RTK_FIX", Required: true,
                Metric: BuildChecklistMetric("GPS_RTK_FIX", "Avionics", request.GpsPassed, request.GpsSatelliteCount, null, "Sats",
                    JoinDetails(
                        Detail("Trạng thái GPS Fix", request.GpsFixStatus),
                        Detail("Ghi chú", request.GpsNotes)),
                    critical: true)),
            // BRL-04: trạng thái kết nối lưu dạng boolean, không quy đổi thành số đo (không bịa 0%).
            (Code: "RF_LINK_TELEMETRY", Required: true,
                Metric: BuildChecklistMetric("RF_LINK_TELEMETRY", "Communication", request.CommunicationPassed, null,
                    request.CommunicationConnected, null,
                    JoinDetails(
                        Detail("Tín hiệu", request.CommunicationSignalInfo),
                        Detail("Ghi chú", request.CommunicationNotes)),
                    critical: true)),
            (Code: "FLIGHT_CONTROLLER", Required: true,
                Metric: BuildChecklistMetric("FLIGHT_CONTROLLER", "Avionics", request.FlightControllerPassed, null,
                    request.FlightControllerConnected, null,
                    JoinDetails(
                        Detail("FC UID", request.FlightControllerUid),
                        Detail("Bo mạch", request.FlightControllerBoard),
                        Detail("Firmware", request.FlightControllerFirmware),
                        Detail("Ghi chú", request.FlightControllerNotes)),
                    critical: true))
        };

        var metrics = checklist.Select(item => item.Metric).OfType<DroneMetricSubmitDto>().ToList();
        // Điện áp và số cell là số đo thật: luôn được giữ lại, kể cả khi chỉ gửi riêng hai giá trị này.
        AddNumericReading(metrics, "BATTERY_VOLTAGE", "Power", request.BatteryVoltage, "V");
        AddNumericReading(metrics, "BATTERY_CELL_COUNT", "Power", request.BatteryCellCount, "cells");

        if (metrics.Count == 0)
            throw new BusinessRuleException("METRICS_REQUIRED", "Cần gửi ít nhất một số đo hoặc kết luận của hạng mục kiểm tra; hệ thống không tự điền dữ liệu.");

        // Hạng mục bắt buộc chưa gửi: phiếu vẫn được lưu nhưng chưa đạt (MaintenanceRequired) để UI báo đỏ và cho nhập bổ sung.
        var missingMetricCodes = checklist
            .Where(item => item.Required && item.Metric == null)
            .Select(item => item.Code)
            .ToList();

        var submitReq = new DroneInspectionSubmitRequest(
            drone.Id,
            request.Notes,
            metrics,
            request.PolicyVersion ?? "v2.0",
            request.FlightControllerUid,
            MissingMetricCodes: missingMetricCodes
        );

        var inspection = await _inspectionService.SubmitInspectionAsync(submitReq, ct);

        return MapInspectionToSummaryDto(inspection);
    }

    /// <summary>Hạng mục chỉ được tạo khi client gửi kết luận (passed), số đo hoặc trạng thái kết nối; ngược lại trả null.</summary>
    private static DroneMetricSubmitDto? BuildChecklistMetric(
        string metricCode,
        string subsystem,
        bool? passed,
        decimal? numericValue,
        bool? boolValue,
        string? unit,
        string? valueText,
        bool critical)
    {
        if (!passed.HasValue && !numericValue.HasValue && !boolValue.HasValue)
            return null;

        var failedSeverity = critical ? "Critical" : "Medium";
        var severity = passed == false ? failedSeverity : "Low";
        return new DroneMetricSubmitDto(
            metricCode, subsystem, numericValue, boolValue, valueText,
            numericValue.HasValue ? unit : null,
            passed, critical, critical, severity);
    }

    private static void AddNumericReading(List<DroneMetricSubmitDto> metrics, string metricCode, string subsystem, decimal? value, string unit)
    {
        if (!value.HasValue)
            return;

        metrics.Add(new DroneMetricSubmitDto(metricCode, subsystem, value, null, null, unit, null, false, false, "Low"));
    }
    /// <summary>"Nhãn: giá trị" cho dữ liệu client đã gửi; null khi không gửi để không bịa giá trị mặc định.</summary>
    private static string? Detail(string label, object? value)
    {
        if (value is null)
            return null;
        if (value is string text && string.IsNullOrWhiteSpace(text))
            return null;

        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{label}: {value}");
    }

    private static string? JoinDetails(params string?[] details)
    {
        var provided = details.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        return provided.Count == 0 ? null : string.Join(" | ", provided);
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
                _db, assessment.PlannedStart, assessment.PlannedEnd, corridorKm, assetCount, ct,
                onlyRegionId: assessment.RegionId);
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

        // Danh sách Drone chỉ được xem khi vùng của hồ sơ nằm trong scope của người gọi (SystemAdmin bỏ qua).
        await DroneRegionScope.EnsureRegionAsync(_db, _current, assessment.RegionId, ct);

        var (corridorKm, towerCount) = await DroneReadinessEvaluator.EstimateScopeAsync(_db, assessment.Towers, ct);
        var evaluated = await DroneReadinessEvaluator.EvaluateAsync(
            _db, assessment.PlannedStart, assessment.PlannedEnd, corridorKm, towerCount, ct,
            onlyRegionId: assessment.RegionId);

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
            MetricCount = inspection.Metrics?.Count ?? 0,
            Readiness = DroneReadiness.FromHealth(inspection.Health),
            MissingMetrics = DroneInspectionSnapshot.ReadMissingMetrics(inspection.RawSnapshot)
        };
    }

    private void EnsureManagerOrAdmin()
    {
        if (!_current.IsAuthenticated || _current.UserId == Guid.Empty)
            throw new ForbiddenException("AUTHENTICATION_REQUIRED");

        var isAuthorized = _current.Roles.Any(r =>
            r.Equals(UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Manager, StringComparison.OrdinalIgnoreCase) ||
            r.Equals(UserRoles.Technician, StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Technician", StringComparison.OrdinalIgnoreCase));

        if (!isAuthorized)
            throw new ForbiddenException("INSUFFICIENT_PERMISSIONS");
    }
}
