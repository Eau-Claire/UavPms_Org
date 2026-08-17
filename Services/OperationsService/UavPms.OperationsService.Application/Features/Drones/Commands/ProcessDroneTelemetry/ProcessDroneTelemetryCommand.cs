using MediatR;

namespace UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneTelemetry;

public sealed record ProcessDroneTelemetryCommand(
    string TopicDroneCode,
    string? PayloadDroneCode,
    DateTime? Timestamp,
    double Latitude,
    double Longitude,
    double? Altitude,
    double? Battery,
    double? Speed,
    double? Heading) : IRequest<bool>;
