using System;
using System.Collections.Generic;
using UavPms.OperationsService.Application.Features.FlightControllers.DTOs;
using UavPms.OperationsService.Domain.Enums;

namespace UavPms.OperationsService.Application.Features.Drones.DTOs;

public class UavDetailsDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public Guid? AssignedRegionId { get; set; }
    public string? AssignedRegionName { get; set; }
    public string OperationalStatus { get; set; } = string.Empty;
    public string TechnicalHealth { get; set; } = string.Empty;
    public double FlightEnduranceMinutes { get; set; }
    public double OperationalRangeKm { get; set; }
    public string? CameraPayloadInfo { get; set; }
    public double BatteryLevel { get; set; }
    public DateTime? LastMaintenanceAt { get; set; }
    public DateTime? TechnicalHealthUpdatedAt { get; set; }
    public FlightControllerBindingDto? ActiveFlightController { get; set; }
    public DroneTechnicalInspectionSummaryDto? LastTechnicalInspection { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DroneTechnicalInspectionSummaryDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Health { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ValidUntil { get; set; }
    public string? TechnicianName { get; set; }
    public string? Notes { get; set; }
    public int MetricCount { get; set; }
}

public class CreateUavRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Category { get; set; } = "Hexacopter";
    public string? SerialNumber { get; set; }
    public Guid? AssignedRegionId { get; set; }
    public string? AssignedRegionName { get; set; }
    public DroneOperationalStatus? OperationalStatus { get; set; } = DroneOperationalStatus.Available;
    public TechnicalHealth? TechnicalHealth { get; set; } = Domain.Enums.TechnicalHealth.Unknown;
    public double? FlightEnduranceMinutes { get; set; } = 30.0;
    public double? OperationalRangeKm { get; set; } = 10.0;
    public string? CameraPayloadInfo { get; set; }
    public double? BatteryLevel { get; set; } = 100.0;
}

public class UpdateUavRequest
{
    public string? Name { get; set; }
    public string? Model { get; set; }
    public string? Category { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? AssignedRegionId { get; set; }
    public string? AssignedRegionName { get; set; }
    public DroneOperationalStatus? OperationalStatus { get; set; }
    public TechnicalHealth? TechnicalHealth { get; set; }
    public double? FlightEnduranceMinutes { get; set; }
    public double? OperationalRangeKm { get; set; }
    public string? CameraPayloadInfo { get; set; }
    public double? BatteryLevel { get; set; }
}

public class BasicTechnicalCheckRequest
{
    // Checklist items: Frame / Body
    public bool FrameBodyPassed { get; set; } = true;
    public string? FrameBodyNotes { get; set; }

    // Propellers
    public bool PropellersPassed { get; set; } = true;
    public string? PropellersNotes { get; set; }

    // Motors
    public bool MotorsPassed { get; set; } = true;
    public string? MotorsNotes { get; set; }

    // Camera / Payload
    public bool CameraPayloadPassed { get; set; } = true;
    public string? CameraPayloadNotes { get; set; }

    // Battery (Voltage, Battery %, Cell Count)
    public bool BatteryPassed { get; set; } = true;
    public decimal? BatteryVoltage { get; set; }
    public decimal? BatteryPercent { get; set; }
    public int? BatteryCellCount { get; set; }
    public string? BatteryNotes { get; set; }

    // GPS (Fix status, satellite count)
    public bool GpsPassed { get; set; } = true;
    public string? GpsFixStatus { get; set; }
    public int? GpsSatelliteCount { get; set; }
    public string? GpsNotes { get; set; }

    // Communication (Connected, signal info)
    public bool CommunicationPassed { get; set; } = true;
    public bool? CommunicationConnected { get; set; }
    public string? CommunicationSignalInfo { get; set; }
    public string? CommunicationNotes { get; set; }

    // Flight Controller (FC UID / board / firmware + connection status)
    public bool FlightControllerPassed { get; set; } = true;
    public string? FlightControllerUid { get; set; }
    public string? FlightControllerBoard { get; set; }
    public string? FlightControllerFirmware { get; set; }
    public bool? FlightControllerConnected { get; set; }
    public string? FlightControllerNotes { get; set; }

    public string? Notes { get; set; }
    public string? PolicyVersion { get; set; } = "v2.0";
    public Guid? PreMissionAssessmentId { get; set; }
}

public class GisDroneEstimationRequest
{
    public Guid? AssessmentId { get; set; }
    public double? CorridorLengthKm { get; set; }
    public int? AssetCount { get; set; }
    public Guid? RegionId { get; set; }
}

public class GisDroneEstimationResult
{
    public double CorridorLengthKm { get; set; }
    public int AssetCount { get; set; }
    public double EstimatedMissionDurationMinutes { get; set; }
    public double AverageSpeedKmh { get; set; } = 36.0;
    public double ReserveBatteryPercent { get; set; } = 20.0;
    public double InspectionMinutesPerAsset { get; set; } = 3.0;
    public IReadOnlyList<GisDroneCapabilityItemDto> Drones { get; set; } = new List<GisDroneCapabilityItemDto>();
}

public class GisDroneCapabilityItemDto
{
    public Guid DroneId { get; set; }
    public string DroneCode { get; set; } = string.Empty;
    public string DroneName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string OperationalStatus { get; set; } = string.Empty;
    public string TechnicalHealth { get; set; } = string.Empty;
    public double FlightEnduranceMinutes { get; set; }
    public double OperationalRangeKm { get; set; }
    public double EffectiveFlightMinutes { get; set; }
    public double MaxCoverageDistanceKm { get; set; }
    public int EstimatedFlightsNeeded { get; set; }
    public int EstimatedDronesNeeded { get; set; }
    public string Suitability { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public bool IsTechnicallyReady { get; set; }
    public string Recommendation { get; set; } = string.Empty;
}
