using System;

namespace UavPms.OperationsService.Application.Features.FlightControllers.DTOs;

public record FlightControllerDroneDto(
    Guid Id,
    string Code,
    string Name,
    string Model,
    string OperationalStatus,
    string TechnicalHealth
);

public record FlightControllerBindingDto(
    Guid Id,
    Guid DroneId,
    string FlightControllerUid,
    string? BoardIdentifier,
    string? FirmwareName,
    string? FirmwareVersion,
    DateTime PairedAt,
    DateTime? UnpairedAt,
    DateTime? LastSeenAt,
    bool IsActive
);

public record FlightControllerResolveResponseDto(
    bool Registered,
    string FlightControllerUid,
    FlightControllerDroneDto? Drone,
    FlightControllerBindingDto? Binding
);

public record PairFlightControllerRequest(
    string FlightControllerUid,
    string? BoardIdentifier = null,
    string? FirmwareName = null,
    string? FirmwareVersion = null
);
