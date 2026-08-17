using MediatR;

namespace UavPms.OperationsService.Application.Features.Drones.Commands.ProcessDroneStatus;

public sealed record ProcessDroneStatusCommand(
    string TopicDroneCode,
    string? PayloadDroneCode,
    string? Status,
    double? Battery,
    DateTime? Timestamp) : IRequest<bool>;
