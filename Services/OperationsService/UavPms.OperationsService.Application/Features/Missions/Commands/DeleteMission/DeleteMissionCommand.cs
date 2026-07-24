using MediatR;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.DeleteMission;

public record DeleteMissionCommand(Guid Id) : IRequest;