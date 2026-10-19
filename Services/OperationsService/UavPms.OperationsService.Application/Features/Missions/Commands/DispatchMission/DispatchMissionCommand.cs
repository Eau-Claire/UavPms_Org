using System;
using MediatR;
using UavPms.OperationsService.Application.Features.Missions.DTOs;

namespace UavPms.OperationsService.Application.Features.Missions.Commands.DispatchMission;

public record DispatchMissionCommand(Guid MissionId) : IRequest<MissionOperationResultDto>;
