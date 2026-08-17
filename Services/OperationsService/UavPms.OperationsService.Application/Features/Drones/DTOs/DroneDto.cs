namespace UavPms.OperationsService.Application.Features.Drones.DTOs;

public sealed record DroneDto(
    Guid Id,
    string DroneCode,
    string Name,
    bool Online,
    double? Battery,
    string OperationalStatus,
    DateTime? LastSeenAt,
    double? Latitude,
    double? Longitude,
    double? Altitude);
