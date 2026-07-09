using MediatR;

namespace UavPms.Application.Features.Missions.Commands.DeleteMission;

public record DeleteMissionCommand(Guid Id) : IRequest;